using System.Collections;
using System.Collections.Generic;
using MergeAndMarch.Data;
using UnityEngine;

namespace MergeAndMarch.Gameplay
{
    public class AutoCombat : MonoBehaviour
    {
        private static Sprite runtimeSquareSprite;
        private const float MageBandHalfHeight = 0.85f;
        private const float MageWaveDuration = 0.3f;

        private readonly List<Troop> troopBuffer = new();
        private readonly Dictionary<Troop, float> attackCooldowns = new();
        private readonly List<Troop> cooldownCleanupBuffer = new();

        [SerializeField] private GameConfig gameConfig;
        [SerializeField] private BattleGrid battleGrid;

        private bool combatEnabled = true;

        private void Awake()
        {
            ResolveReferences();
        }

        private void Update()
        {
            if (!combatEnabled)
            {
                return;
            }

            if (battleGrid == null || gameConfig == null)
            {
                ResolveReferences();
                if (battleGrid == null || gameConfig == null)
                {
                    return;
                }
            }

            battleGrid.GetTroops(troopBuffer);
            CleanupMissingTroops();

            for (int i = 0; i < troopBuffer.Count; i++)
            {
                Troop troop = troopBuffer[i];
                if (troop == null || troop.Data == null || !troop.IsCombatActive)
                {
                    continue;
                }

                if (!attackCooldowns.ContainsKey(troop))
                {
                    attackCooldowns[troop] = Random.Range(0f, Mathf.Max(0.05f, GetAttackIntervalFor(troop) * 0.5f));
                }

                float cooldown = attackCooldowns[troop] - Time.deltaTime;
                if (cooldown > 0f)
                {
                    attackCooldowns[troop] = cooldown;
                    continue;
                }

                if (!TryResolveTroopAction(troop))
                {
                    attackCooldowns[troop] = 0.05f;
                    continue;
                }
                attackCooldowns[troop] = GetAttackIntervalFor(troop);
            }
        }

        public void SetCombatEnabled(bool isEnabled)
        {
            combatEnabled = isEnabled;
        }

        public void ResetAttackTimers()
        {
            if (battleGrid == null)
            {
                ResolveReferences();
            }

            battleGrid?.GetTroops(troopBuffer);
            CleanupMissingTroops();

            for (int i = 0; i < troopBuffer.Count; i++)
            {
                Troop troop = troopBuffer[i];
                if (troop == null || !troop.IsCombatActive)
                {
                    continue;
                }

                attackCooldowns[troop] = Random.Range(0.05f, Mathf.Max(0.1f, GetAttackIntervalFor(troop) * 0.5f));
            }
        }

        private bool TryResolveTroopAction(Troop troop)
        {
            return troop.Data.targeting switch
            {
                TroopTargeting.AoEBand => TryResolveAttack(troop),
                TroopTargeting.HealLowestHP => TryResolveHealerPulse(troop),
                TroopTargeting.OnContact => false,
                _ => TryResolveAttack(troop)
            };
        }

        private bool TryResolveAttack(Troop troop)
        {
            Enemy target = FindTargetFor(troop);
            if (target == null)
            {
                return false;
            }

            troop.PlayAttackFeedback(target.transform.position);
            ResolveAttack(troop, target);
            return true;
        }

        private void ResolveAttack(Troop troop, Enemy target)
        {
            float damage = GetAttackDamageFor(troop);
            if (troop.Data.troopType == TroopType.Archer)
            {
                if (target.Data != null && target.Data.enemyType == EnemyType.Tank)
                {
                    RunBuffs buffs = CardSystem.Instance != null ? CardSystem.Instance.runBuffs : null;
                    if (buffs != null)
                    {
                        damage *= buffs.archerVsTankMultiplier;
                    }
                }
                StartCoroutine(FireProjectileRoutine(troop, target, damage));
                return;
            }

            if (troop.Data.troopType == TroopType.Mage)
            {
                StartCoroutine(PlayMageWaveRoutine(troop, target.transform.position.y, damage));
                return;
            }

            if (troop.Data.troopType == TroopType.Knight)
            {
                target.PlayImpactFeedback();
                JuiceFx.Instance?.KnightSlash(target.transform.position, troop.transform.position, troop.Data.troopColor);
            }

            target.ApplyDamage(damage, troop);
        }

        private bool TryResolveHealerPulse(Troop healer)
        {
            Troop healTarget = FindHealTargetFor(healer);
            if (healTarget == null)
            {
                return false;
            }

            healer.PlayAttackFeedback(healTarget.transform.position);
            healTarget.HealFlat(GetHealerPowerFor(healer));
            JuiceFx.Instance?.HealRing(healer.transform.position, healTarget.transform.position);
            return true;
        }

        private Enemy FindTargetFor(Troop troop)
        {
            IReadOnlyList<Enemy> enemies = Enemy.ActiveEnemies;
            Enemy best = null;
            float bestDistance = float.MaxValue;
            int bestColumnOffset = int.MaxValue;
            RunBuffs buffs = CardSystem.Instance != null ? CardSystem.Instance.runBuffs : null;

            for (int i = 0; i < enemies.Count; i++)
            {
                Enemy enemy = enemies[i];
                if (enemy == null || !enemy.IsAlive)
                {
                    continue;
                }

                float verticalDistance = troop.transform.position.y - enemy.transform.position.y;
                if (troop.Data.troopType == TroopType.Archer
                    && enemy.transform.position.y - troop.transform.position.y > gameConfig.archerRange)
                {
                    continue;
                }

                int columnOffset = 0;
                if (troop.Data.troopType == TroopType.Knight)
                {
                    columnOffset = Mathf.Abs(enemy.LaneColumn - troop.Column);
                    if (columnOffset > 1)
                    {
                        continue;
                    }

                    bool isFlyer = enemy.Data != null && enemy.Data.skipsFrontline;
                    if (isFlyer && (buffs == null || !buffs.knightCanHitFlyers))
                    {
                        continue;
                    }

                    // Enemies stand above the Knight, so measure enemy minus troop (the old sign skipped
                    // every enemy in front of it). Reach always covers the body-contact stop distance.
                    float distanceAhead = enemy.transform.position.y - troop.transform.position.y;
                    float reach = Mathf.Max(gameConfig.knightMeleeReach, enemy.GetContactDistance(troop) + 0.15f);
                    if (distanceAhead < -0.05f || distanceAhead > reach)
                    {
                        continue;
                    }
                }

                float absDistance = Mathf.Abs(verticalDistance);
                bool isBetterTarget = absDistance < bestDistance;
                if (troop.Data.troopType == TroopType.Knight
                    && Mathf.Approximately(absDistance, bestDistance)
                    && columnOffset < bestColumnOffset)
                {
                    isBetterTarget = true;
                }

                if (isBetterTarget)
                {
                    best = enemy;
                    bestDistance = absDistance;
                    bestColumnOffset = columnOffset;
                }
            }

            return best;
        }

        private Troop FindHealTargetFor(Troop healer)
        {
            if (battleGrid == null || healer == null)
            {
                return null;
            }

            Troop best = null;
            float bestRatio = 1f;

            EvaluateHealCandidate(healer.Column - 1, healer.Row, healer, ref best, ref bestRatio);
            EvaluateHealCandidate(healer.Column + 1, healer.Row, healer, ref best, ref bestRatio);
            EvaluateHealCandidate(healer.Column, healer.Row - 1, healer, ref best, ref bestRatio);
            EvaluateHealCandidate(healer.Column, healer.Row + 1, healer, ref best, ref bestRatio);

            return best;
        }

        private void EvaluateHealCandidate(int column, int row, Troop healer, ref Troop best, ref float bestRatio)
        {
            Troop candidate = battleGrid.GetTroopAt(column, row);
            if (candidate == null || candidate == healer || !candidate.IsCombatActive || candidate.MaxHP <= 0.01f)
            {
                return;
            }

            float hpRatio = candidate.CurrentHP / candidate.MaxHP;
            if (hpRatio >= 0.999f)
            {
                return;
            }

            if (hpRatio < bestRatio)
            {
                best = candidate;
                bestRatio = hpRatio;
            }
        }

        private IEnumerator PlayMageWaveRoutine(Troop troop, float targetY, float damage)
        {
            JuiceFx fx = JuiceFx.Instance;
            JuiceConfig juice = fx != null ? fx.Config : null;
            float waveDuration = juice != null ? juice.mageWaveDuration : MageWaveDuration;
            GameObject wave = new("MageWave");
            float waveWidth = GetCameraWorldWidth();
            float waveHeight = juice != null
                ? juice.mageWaveHeight
                : Mathf.Max(0.18f, gameConfig != null ? gameConfig.cellSize * 0.2f : 0.2f);
            Color waveColor = troop != null && troop.Data != null
                ? troop.Data.troopColor
                : new Color(0.7333f, 0.4196f, 1f, 1f);
            waveColor.a = juice != null ? 0.6f : 0.9f;

            wave.transform.position = new Vector3(GetCameraCenterX(troop), targetY, 0f);
            wave.transform.localScale = new Vector3(0.25f, waveHeight, 1f);

            SpriteRenderer renderer = wave.AddComponent<SpriteRenderer>();
            renderer.sprite = GetRuntimeSquareSprite();
            renderer.color = waveColor;
            renderer.sortingLayerName = "Effects";
            renderer.sortingOrder = 6;

            // White core band so the wave reads as energy, not a coloured bar (Mage's identity moment).
            SpriteRenderer core = CreateEffectSprite(wave.transform, "Core", GetRuntimeSquareSprite(), new Color(1f, 1f, 1f, 0.9f), 7);
            core.transform.localScale = new Vector3(1f, 0.28f, 1f);
            if (fx != null && fx.Art != null)
            {
                for (int i = 0; i < 6; i++)
                {
                    Vector3 sparkPosition = new(wave.transform.position.x + Random.Range(-waveWidth * 0.45f, waveWidth * 0.45f), targetY + Random.Range(-waveHeight * 0.3f, waveHeight * 0.3f), 0f);
                    fx.Spawn(fx.Art.fxSparkle, sparkPosition, Color.white, 0.22f, 0.05f, waveDuration, 0f, default, 0f, Random.Range(-360f, 360f), false, "Effects", 8, 0.4f);
                }
            }

            bool didApplyDamage = false;
            float elapsed = 0f;

            while (elapsed < waveDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / waveDuration);
                float widthT = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.35f));
                float currentWidth = Mathf.Lerp(0.25f, waveWidth, widthT);
                float currentHeight = Mathf.Lerp(waveHeight * 0.8f, waveHeight * 1.2f, t);
                wave.transform.localScale = new Vector3(currentWidth, currentHeight, 1f);

                Color color = waveColor;
                color.a = Mathf.Lerp(waveColor.a, 0f, t);
                renderer.color = color;
                core.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.9f, 0f, t));

                if (!didApplyDamage && t >= 0.2f)
                {
                    ApplyMageBandDamage(troop, targetY, damage);
                    didApplyDamage = true;
                }

                yield return null;
            }

            if (!didApplyDamage)
            {
                ApplyMageBandDamage(troop, targetY, damage);
            }

            Destroy(wave);
        }

        private IEnumerator FireProjectileRoutine(Troop troop, Enemy target, float damage)
        {
            if (troop == null || target == null)
            {
                yield break;
            }

            JuiceFx fx = JuiceFx.Instance;
            JuiceConfig juice = fx != null ? fx.Config : null;
            Sprite arrowSprite = fx != null && fx.Art != null ? fx.Art.fxArrow : null;
            Color troopColor = troop.Data != null ? troop.Data.troopColor : new Color(0.266f, 1f, 0.533f, 1f);

            GameObject projectile = new("ArcherProjectile");
            projectile.transform.position = troop.transform.position;

            SpriteRenderer renderer = projectile.AddComponent<SpriteRenderer>();
            renderer.sortingLayerName = "Effects";
            renderer.sortingOrder = 5;
            if (arrowSprite != null)
            {
                // Bean arrow sprite (art bible Section 2: type-coloured projectile with a white core).
                renderer.sprite = arrowSprite;
                renderer.color = Color.white;
                float arrowScale = juice.arrowSize / Mathf.Max(0.0001f, arrowSprite.bounds.size.x);
                projectile.transform.localScale = Vector3.one * arrowScale;
            }
            else
            {
                renderer.sprite = GetRuntimeSquareSprite();
                renderer.color = troopColor;
                projectile.transform.localScale = Vector3.one * 0.15f;
            }

            Vector3 start = projectile.transform.position;
            fx?.MuzzlePop(start, troopColor);
            float duration = juice != null ? juice.arrowFlightDuration : 0.18f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                Vector3 targetPosition = target != null ? target.transform.position : start;
                Vector3 previous = projectile.transform.position;
                projectile.transform.position = Vector3.Lerp(start, targetPosition, t);
                Vector3 travel = targetPosition - start;
                if (travel.sqrMagnitude > 0.0001f)
                {
                    projectile.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(travel.y, travel.x) * Mathf.Rad2Deg);
                }

                fx?.ProjectileTrail(previous, troopColor);
                yield return null;
            }

            if (target != null && target.IsAlive)
            {
                target.ApplyDamage(damage, troop);
            }

            Destroy(projectile);
        }

        private float GetAttackDamageFor(Troop troop)
        {
            float damage = troop.GetAttackDamage();
            RunBuffs buffs = CardSystem.Instance != null ? CardSystem.Instance.runBuffs : null;
            if (buffs == null)
            {
                return damage;
            }

            damage *= buffs.attackMultiplier;
            if (troop.Data.troopType == TroopType.Knight)
            {
                damage *= buffs.knightAttackMultiplier;
            }
            else if (troop.Data.troopType == TroopType.Archer)
            {
                damage *= buffs.archerAttackMultiplier;
            }
            else if (troop.Data.troopType == TroopType.Mage)
            {
                damage *= buffs.mageAttackMultiplier;
            }
            return damage;
        }

        private float GetAttackIntervalFor(Troop troop)
        {
            float interval = troop.GetAttackInterval();
            RunBuffs buffs = CardSystem.Instance != null ? CardSystem.Instance.runBuffs : null;
            if (buffs != null && troop.Data != null && troop.Data.troopType == TroopType.Archer)
            {
                interval *= buffs.archerSpeedMultiplier;
            }

            return interval;
        }

        private float GetHealerPowerFor(Troop troop)
        {
            float power = troop.GetSupportPower();
            RunBuffs buffs = CardSystem.Instance != null ? CardSystem.Instance.runBuffs : null;
            if (buffs != null)
            {
                power *= buffs.healerPowerMultiplier;
            }

            return power;
        }

        private void ResolveReferences()
        {
            if (battleGrid == null)
            {
                battleGrid = FindFirstObjectByType<BattleGrid>();
            }

            if (gameConfig == null && battleGrid != null)
            {
                gameConfig = battleGrid.Config;
            }
        }

        private void CleanupMissingTroops()
        {
            cooldownCleanupBuffer.Clear();

            foreach (Troop troop in attackCooldowns.Keys)
            {
                if (troop == null || !troopBuffer.Contains(troop))
                {
                    cooldownCleanupBuffer.Add(troop);
                }
            }

            for (int i = 0; i < cooldownCleanupBuffer.Count; i++)
            {
                attackCooldowns.Remove(cooldownCleanupBuffer[i]);
            }
        }

        private void ApplyMageBandDamage(Troop source, float targetY, float damage)
        {
            IReadOnlyList<Enemy> enemies = Enemy.ActiveEnemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                Enemy enemy = enemies[i];
                if (enemy == null || !enemy.IsAlive)
                {
                    continue;
                }

                if (Mathf.Abs(enemy.transform.position.y - targetY) > MageBandHalfHeight)
                {
                    continue;
                }

                enemy.ApplyDamage(damage, source);
            }
        }

        private SpriteRenderer CreateEffectSprite(Transform parent, string name, Sprite sprite, Color color, int sortingOrder)
        {
            GameObject effectObject = new(name);
            effectObject.transform.SetParent(parent, false);

            SpriteRenderer renderer = effectObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingLayerName = "Effects";
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private float GetCameraWorldWidth()
        {
            Camera activeCamera = Camera.main;
            if (activeCamera == null)
            {
                return 12f;
            }

            return (activeCamera.orthographicSize * 2f * activeCamera.aspect) + 1f;
        }

        private float GetCameraCenterX(Troop troop)
        {
            Camera activeCamera = Camera.main;
            if (activeCamera != null)
            {
                return activeCamera.transform.position.x;
            }

            return troop != null ? troop.transform.position.x : 0f;
        }

        private static Sprite GetRuntimeSquareSprite()
        {
            if (runtimeSquareSprite != null)
            {
                return runtimeSquareSprite;
            }

            Texture2D texture = new(16, 16, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[16 * 16];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.white;
            }

            texture.SetPixels(pixels);
            texture.Apply();
            runtimeSquareSprite = Sprite.Create(texture, new Rect(0f, 0f, 16f, 16f), new Vector2(0.5f, 0.5f), 100f);
            runtimeSquareSprite.name = "AutoCombatRuntimeSquare";
            return runtimeSquareSprite;
        }

    }
}
