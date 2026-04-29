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
        private const float HealerPulseDuration = 0.4f;

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
            }

            target.ApplyDamage(damage);
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
            StartCoroutine(PlayHealerPulseRoutine(healTarget.transform.position));
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

                    if (verticalDistance < -0.05f || verticalDistance > gameConfig.knightMeleeReach)
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
            GameObject wave = new("MageWave");
            float waveWidth = GetCameraWorldWidth();
            float waveHeight = Mathf.Max(0.18f, gameConfig != null ? gameConfig.cellSize * 0.2f : 0.2f);
            Color waveColor = troop != null && troop.Data != null
                ? troop.Data.troopColor
                : new Color(0.7333f, 0.4196f, 1f, 1f);
            waveColor.a = 0.9f;

            wave.transform.position = new Vector3(GetCameraCenterX(troop), targetY, 0f);
            wave.transform.localScale = new Vector3(0.25f, waveHeight, 1f);

            SpriteRenderer renderer = wave.AddComponent<SpriteRenderer>();
            renderer.sprite = GetRuntimeSquareSprite();
            renderer.color = waveColor;
            renderer.sortingLayerName = "Effects";
            renderer.sortingOrder = 6;

            bool didApplyDamage = false;
            float elapsed = 0f;

            while (elapsed < MageWaveDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / MageWaveDuration);
                float widthT = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.35f));
                float currentWidth = Mathf.Lerp(0.25f, waveWidth, widthT);
                float currentHeight = Mathf.Lerp(waveHeight * 0.8f, waveHeight * 1.2f, t);
                wave.transform.localScale = new Vector3(currentWidth, currentHeight, 1f);

                Color color = waveColor;
                color.a = Mathf.Lerp(waveColor.a, 0f, t);
                renderer.color = color;

                if (!didApplyDamage && t >= 0.2f)
                {
                    ApplyMageBandDamage(targetY, damage);
                    didApplyDamage = true;
                }

                yield return null;
            }

            if (!didApplyDamage)
            {
                ApplyMageBandDamage(targetY, damage);
            }

            Destroy(wave);
        }

        private IEnumerator FireProjectileRoutine(Troop troop, Enemy target, float damage)
        {
            if (troop == null || target == null)
            {
                yield break;
            }

            GameObject projectile = new("ArcherProjectile");
            projectile.transform.position = troop.transform.position;
            projectile.transform.localScale = Vector3.one * 0.15f;

            SpriteRenderer renderer = projectile.AddComponent<SpriteRenderer>();
            renderer.sprite = GetRuntimeSquareSprite();
            renderer.color = troop.Data != null ? troop.Data.troopColor : new Color(0.266f, 1f, 0.533f, 1f);
            renderer.sortingLayerName = "Effects";
            renderer.sortingOrder = 5;

            Vector3 start = projectile.transform.position;
            float duration = 0.18f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                Vector3 targetPosition = target != null ? target.transform.position : start;
                projectile.transform.position = Vector3.Lerp(start, targetPosition, t);
                yield return null;
            }

            if (target != null && target.IsAlive)
            {
                target.ApplyDamage(damage);
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

        private void ApplyMageBandDamage(float targetY, float damage)
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

                enemy.ApplyDamage(damage);
            }
        }

        private IEnumerator PlayHealerPulseRoutine(Vector3 targetPosition)
        {
            GameObject plusRoot = new("HealerPulse");
            plusRoot.transform.position = targetPosition;
            plusRoot.transform.localScale = Vector3.one * 0.45f;

            SpriteRenderer vertical = CreateEffectSprite(plusRoot.transform, "Vertical", GetRuntimeSquareSprite(), new Color(0.55f, 1f, 0.62f, 0.95f), 6);
            vertical.transform.localScale = new Vector3(0.14f, 0.5f, 1f);

            SpriteRenderer horizontal = CreateEffectSprite(plusRoot.transform, "Horizontal", GetRuntimeSquareSprite(), new Color(0.55f, 1f, 0.62f, 0.95f), 6);
            horizontal.transform.localScale = new Vector3(0.5f, 0.14f, 1f);

            float elapsed = 0f;
            while (elapsed < HealerPulseDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / HealerPulseDuration);
                plusRoot.transform.position = targetPosition + (Vector3.up * Mathf.Lerp(0f, 0.18f, t));
                plusRoot.transform.localScale = Vector3.one * Mathf.Lerp(0.45f, 0.8f, t);

                float alpha = Mathf.Lerp(0.95f, 0f, t);
                vertical.color = new Color(0.55f, 1f, 0.62f, alpha);
                horizontal.color = new Color(0.55f, 1f, 0.62f, alpha);
                yield return null;
            }

            Destroy(plusRoot);
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
