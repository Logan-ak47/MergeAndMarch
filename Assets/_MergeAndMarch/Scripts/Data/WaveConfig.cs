using System;
using UnityEngine;

namespace MergeAndMarch.Data
{
    /// <summary>
    /// Enemy-side balance: per-wave composition and the threat curves. Replaces the values that
    /// were hardcoded in EnemySpawner and WaveManager so they can be tuned against the balance
    /// harness (Editor/AutomatedRunSimulationTool).
    /// Curves (systems-designer model, 2026-10-27):
    ///   enemy HP  = baseHP * hpBase * hpGrowth^(wave-1)
    ///   enemy ATK = baseAttack * (1 + attackGrowthPerWave * (wave-1))
    ///   spawn interval = clamp(spawnIntervalStart - spawnIntervalStep * (wave-1), spawnIntervalMin, spawnIntervalStart)
    /// </summary>
    [CreateAssetMenu(fileName = "WaveConfig", menuName = "Merge And March/Wave Config")]
    public class WaveConfig : ScriptableObject
    {
        /// <summary>Enemy counts by type for one regular wave.</summary>
        [Serializable]
        public struct WaveDefinition
        {
            [Min(0)] public int grunts;
            [Min(0)] public int rushers;
            [Min(0)] public int tanks;
            [Min(0)] public int flyers;

            public WaveDefinition(int grunts, int rushers, int tanks, int flyers)
            {
                this.grunts = grunts;
                this.rushers = rushers;
                this.tanks = tanks;
                this.flyers = flyers;
            }

            public int Total => grunts + rushers + tanks + flyers;
        }

        [Header("Waves (index 0 = wave 1; waves past the table repeat the last row)")]
        public WaveDefinition[] waves =
        {
            new(4, 0, 0, 0),
            new(4, 1, 0, 0),
            new(4, 0, 1, 0),
            new(4, 2, 0, 0),
            new(4, 0, 1, 1),
            new(4, 2, 1, 0),
            new(4, 2, 1, 1),
            new(3, 2, 1, 2),
            new(4, 2, 2, 1),
            new(3, 2, 2, 2),
            new(4, 3, 2, 1),
            new(3, 3, 2, 3),
            new(3, 3, 3, 2),
            new(3, 3, 3, 3),
            new(3, 3, 3, 3)
        };

        [Min(2)] public int bossWave = 16;

        [Header("Enemy HP: baseHP * hpBase * hpGrowth^(wave-1)")]
        [Min(0.1f)] public float hpBase = 2f;
        [Min(1f)] public float hpGrowth = 1.12f;

        [Header("Enemy attack: baseAttack * (1 + attackGrowthPerWave * (wave-1))")]
        [Min(0f)] public float attackGrowthPerWave = 0.06f;

        [Header("Spawn interval (seconds between enemies, +/-10% jitter)")]
        [Min(0.05f)] public float spawnIntervalStart = 0.9f;
        [Min(0f)] public float spawnIntervalStep = 0.03f;
        [Min(0.05f)] public float spawnIntervalMin = 0.45f;

        [Header("Early-wave softening")]
        [Min(0)] public int earlyVariantWaves = 5;
        [Range(0.1f, 1f)] public float earlyVariantHpMultiplier = 0.75f;
        [Min(0)] public int rusherSlowWaves = 3;
        [Range(0.1f, 1f)] public float rusherEarlySpeed = 0.8f;
        [Min(0)] public int flyerSlowWaves = 5;
        [Range(0.1f, 1f)] public float flyerEarlySpeed = 0.7f;

        [Header("Boss (applied to the Boss EnemyData base stats; no wave curve)")]
        [Min(0.1f)] public float bossHpMultiplier = 5f;
        [Min(0.1f)] public float bossAttackMultiplier = 2f;
        [Min(0.1f)] public float bossScaleMultiplier = 1.45f;

        /// <summary>Composition for a regular wave (1-based).</summary>
        public WaveDefinition GetWave(int wave)
        {
            if (waves == null || waves.Length == 0)
            {
                return new WaveDefinition(3, 0, 0, 0);
            }

            return waves[Mathf.Clamp(wave - 1, 0, waves.Length - 1)];
        }

        /// <summary>Enemy HP multiplier for a regular wave.</summary>
        public float HpMultiplier(int wave)
        {
            return hpBase * Mathf.Pow(hpGrowth, Mathf.Max(0, wave - 1));
        }

        /// <summary>Enemy attack multiplier for a regular wave.</summary>
        public float AttackMultiplier(int wave)
        {
            return 1f + (attackGrowthPerWave * Mathf.Max(0, wave - 1));
        }

        /// <summary>Mean seconds between spawns in a wave.</summary>
        public float SpawnInterval(int wave)
        {
            float interval = spawnIntervalStart - (spawnIntervalStep * Mathf.Max(0, wave - 1));
            return Mathf.Clamp(interval, spawnIntervalMin, Mathf.Max(spawnIntervalMin, spawnIntervalStart));
        }
    }
}
