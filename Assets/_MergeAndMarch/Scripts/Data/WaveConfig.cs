using System;
using UnityEngine;

namespace MergeAndMarch.Data
{
    /// <summary>
    /// Enemy-side balance: per-wave composition and the threat curves. Replaces the values that
    /// were hardcoded in EnemySpawner and WaveManager so they can be tuned against the balance
    /// harness (Editor/AutomatedRunSimulationTool).
    /// A run is one 8-wave chapter with a mid-boss and a boss (design/specs/bench-draft-chapters.md);
    /// the 2026-10-29 rows and growth rates are the 15-wave tables rescaled, not retuned.
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
            new(4, 0, 1, 0),
            new(4, 0, 1, 1),
            new(4, 2, 1, 0),
            new(3, 2, 1, 2),
            new(3, 2, 2, 2),
            new(3, 3, 2, 3)
        };

        [Min(2)] public int bossWave = 8;

        [Header("Mid-boss (Boss EnemyData scaled down; spawned alongside its wave's regular enemies)")]
        [Tooltip("0 disables the mid-boss.")]
        [Min(0)] public int midBossWave = 4;
        [Min(0.01f)] public float midBossHpScale = 0.15f;
        [Min(0.01f)] public float midBossAttackScale = 0.35f;
        [Min(0.1f)] public float midBossScaleMultiplier = 1.15f;
        [Tooltip("Base damage if the mid-boss leaks (replaces the boss's instant-loss value).")]
        [Min(0f)] public float midBossBaseDamage = 40f;

        [Header("Enemy HP: baseHP * hpBase * hpGrowth^(wave-1)")]
        [Min(0.1f)] public float hpBase = 2f;
        [Min(1f)] public float hpGrowth = 1.25f;

        [Header("Enemy attack: baseAttack * (1 + attackGrowthPerWave * (wave-1))")]
        [Min(0f)] public float attackGrowthPerWave = 0.12f;

        [Header("Horde density (direction 2026-11-03): more, weaker enemies with the same wave totals")]
        [Tooltip("Regular-wave enemy counts are multiplied by this; each enemy's HP, attack, base damage and kill energy, and the spawn interval, are divided by it.")]
        [Min(1f)] public float hordeMultiplier = 2f;

        [Header("Spawn interval (seconds between enemies, +/-10% jitter)")]
        [Min(0.05f)] public float spawnIntervalStart = 0.9f;
        [Min(0f)] public float spawnIntervalStep = 0.06f;
        [Min(0.05f)] public float spawnIntervalMin = 0.45f;

        [Header("Early-wave softening")]
        [Min(0)] public int earlyVariantWaves = 3;
        [Range(0.1f, 1f)] public float earlyVariantHpMultiplier = 0.75f;
        [Min(0)] public int rusherSlowWaves = 2;
        [Range(0.1f, 1f)] public float rusherEarlySpeed = 0.8f;
        [Min(0)] public int flyerSlowWaves = 3;
        [Range(0.1f, 1f)] public float flyerEarlySpeed = 0.7f;

        [Header("Boss (applied to the Boss EnemyData base stats; no wave curve)")]
        [Min(0.1f)] public float bossHpMultiplier = 5f;
        [Min(0.1f)] public float bossAttackMultiplier = 2f;
        [Min(0.1f)] public float bossScaleMultiplier = 1.45f;

        /// <summary>True when the given wave spawns the mid-boss.</summary>
        public bool IsMidBossWave(int wave)
        {
            return midBossWave > 0 && midBossWave < bossWave && wave == midBossWave;
        }

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

        /// <summary>Mean seconds between spawns in a wave (already divided by the horde multiplier).</summary>
        public float SpawnInterval(int wave)
        {
            float interval = spawnIntervalStart - (spawnIntervalStep * Mathf.Max(0, wave - 1));
            return Mathf.Clamp(interval, spawnIntervalMin, Mathf.Max(spawnIntervalMin, spawnIntervalStart)) / Mathf.Max(1f, hordeMultiplier);
        }
    }
}
