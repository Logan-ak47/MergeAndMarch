using System.Collections;
using System.Collections.Generic;
using MergeAndMarch.Data;
using UnityEngine;

namespace MergeAndMarch.Gameplay
{
    public class AutoCombat : MonoBehaviour
    {
        private static Sprite projectileSprite;

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
                if (troop == null || troop.Data == null || !troop.IsAlive)
                {
                    continue;
                }

                if (!attackCooldowns.ContainsKey(troop))
                {
                    attackCooldowns[troop] = Random.Range(0f, Mathf.Max(0.05f, troop.GetAttackInterval() * 0.5f));
                }

                float cooldown = attackCooldowns[troop] - Time.deltaTime;
                if (cooldown > 0f)
                {
                    attackCooldowns[troop] = cooldown;
                    continue;
                }

                Enemy target = FindTargetFor(troop);
                if (target == null)
                {
                    attackCooldowns[troop] = 0.05f;
                    continue;
                }

                troop.PlayAttackFeedback(target.transform.position);
                ResolveAttack(troop, target);
                attackCooldowns[troop] = troop.GetAttackInterval();
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
                if (troop == null || !troop.IsAlive)
                {
                    continue;
                }

                attackCooldowns[troop] = Random.Range(0.05f, Mathf.Max(0.1f, troop.GetAttackInterval() * 0.5f));
            }
        }

        private void ResolveAttack(Troop troop, Enemy target)
        {
            float damage = troop.GetAttackDamage();
            if (troop.Data.troopType == TroopType.Archer)
            {
                StartCoroutine(FireProjectileRoutine(troop, target, damage));
                return;
            }

            if (troop.Data.troopType == TroopType.Knight)
            {
                target.PlayImpactFeedback();
            }

            target.ApplyDamage(damage);
        }

        private Enemy FindTargetFor(Troop troop)
        {
            IReadOnlyList<Enemy> enemies = Enemy.ActiveEnemies;
            Enemy best = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < enemies.Count; i++)
            {
                Enemy enemy = enemies[i];
                if (enemy == null || !enemy.IsAlive)
                {
                    continue;
                }

                float verticalDistance = troop.transform.position.y - enemy.transform.position.y;
                if (troop.Data.troopType == TroopType.Knight)
                {
                    if (enemy.LaneColumn != troop.Column)
                    {
                        continue;
                    }

                    if (verticalDistance < -0.05f || verticalDistance > gameConfig.knightMeleeReach)
                    {
                        continue;
                    }
                }

                float absDistance = Mathf.Abs(verticalDistance);
                if (absDistance < bestDistance)
                {
                    best = enemy;
                    bestDistance = absDistance;
                }
            }

            return best;
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
            renderer.sprite = GetProjectileSprite();
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

        private static Sprite GetProjectileSprite()
        {
            if (projectileSprite != null)
            {
                return projectileSprite;
            }

            Texture2D texture = new(16, 16, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[16 * 16];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.white;
            }

            texture.SetPixels(pixels);
            texture.Apply();
            projectileSprite = Sprite.Create(texture, new Rect(0f, 0f, 16f, 16f), new Vector2(0.5f, 0.5f), 100f);
            projectileSprite.name = "ArcherProjectileSprite";
            return projectileSprite;
        }
    }
}
