using System.Collections.Generic;
using MergeAndMarch.Data;
using UnityEngine;

namespace MergeAndMarch.Gameplay
{
    public class AutoCombat : MonoBehaviour
    {
        private readonly List<Troop> troopBuffer = new();
        private readonly Dictionary<Troop, float> attackCooldowns = new();
        private readonly List<Troop> cooldownCleanupBuffer = new();

        [SerializeField] private GameConfig gameConfig;
        [SerializeField] private BattleGrid battleGrid;

        private void Awake()
        {
            ResolveReferences();
        }

        private void Update()
        {
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
                target.ApplyDamage(troop.GetAttackDamage());
                attackCooldowns[troop] = troop.GetAttackInterval();
            }
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
    }
}
