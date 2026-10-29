using UnityEngine;

namespace MergeAndMarch.Data
{
    [CreateAssetMenu(fileName = "GameConfig", menuName = "Merge And March/Game Config")]
    public class GameConfig : ScriptableObject
    {
        [Header("Grid")]
        [Min(1)] public int columns = 4;
        [Min(1)] public int rows = 2;
        [Min(0.1f)] public float cellSize = 1.15f;
        public Vector2 gridOffset = new(-1.725f, -3.6f);

        [Header("Troop Visuals")]
        [Min(0.01f)] public float troopBaseScale = 1f;
        [Min(0.01f)] public float tierTwoScale = 1.15f;
        [Min(0.01f)] public float tierThreeScale = 1.3f;
        [Min(0.1f)] public float slotVisualScale = 0.61f;
        public Color slotTint = new(1f, 1f, 1f, 0.3f);
        public Color tierTwoTint = new(1f, 1f, 1f, 1f);
        public Color tierThreeTint = new(1f, 0.95f, 0.95f, 1f);

        [Header("Arena (art bible Sections 4 and 6)")]
        public Color arenaTopColor = new(0.169f, 0.549f, 1f, 1f);
        public Color arenaBottomColor = new(0.416f, 0.239f, 1f, 1f);

        [Header("Troop tiers (index 0 = tier 1). Design: x1 / x2.5 / x6, 2026-10-27")]
        public float[] tierStatMultipliers = { 1f, 2.5f, 6f };
        public float[] tierAttackIntervalMultipliers = { 1f, 0.9f, 0.85f };
        public float[] bomberTierRadius = { 2f, 2.5f, 3f };

        [Header("Run start")]
        [Tooltip("Knights and Bombers start in the front row, others in the back. One adjacent pair teaches merging in the first seconds.")]
        public TroopType[] startingLineup = { TroopType.Knight, TroopType.Knight, TroopType.Archer, TroopType.Mage };

        [Header("Platform")]
        [Min(15)] public int targetFrameRate = 60;

        /// <summary>Reads a per-tier value (tier 1–3) from a table, falling back when it is missing.</summary>
        public static float TierValue(float[] table, int tier, float fallback)
        {
            int index = Mathf.Clamp(tier, 1, 3) - 1;
            return table != null && index < table.Length ? table[index] : fallback;
        }

        [Header("Lane Visuals")]
        [Min(0.1f)] public float laneGuideHeight = 5.8f;
        [Range(0.02f, 0.5f)] public float laneGuideWidthScale = 0.1f;
        [Range(0.02f, 0.5f)] public float laneGuideMarkerScale = 0.16f;
        public Color laneGuideTint = new(1f, 1f, 1f, 0.16f);
        public Color laneGuideMarkerTint = new(1f, 1f, 1f, 0.18f);

        [Header("Merge")]
        [Range(0.01f, 1f)] public float tacticalSlowTimeScale = 0.2f;
        [Min(0.01f)] public float mergeSlideDuration = 0.15f;
        [Min(0.01f)] public float mergeFlashDuration = 0.1f;
        [Min(0.01f)] public float mergePopDuration = 0.2f;
        [Min(1f)] public float mergeOvershootScale = 1.3f;
        [Min(0.01f)] public float dragReturnDuration = 0.12f;
        [Min(0.01f)] public float swapMoveDuration = 0.12f;

        [Header("Combat")]
        [Min(0.01f)] public float enemyBaseScale = 1f;
        [Min(0.1f)] public float enemySpawnYOffset = 2.2f;
        [Min(0.1f)] public float enemyFailOffset = 0.8f;
        [Tooltip("Minimum centre-to-centre stop distance; the real stop distance also scales with body size.")]
        [Min(0.05f)] public float enemyEngageDistance = 0.3f;
        [Tooltip("Enemies stop when centres are this fraction of the two half-heights apart (1 = just touching, lower = more overlap).")]
        [Range(0.3f, 1.2f)] public float enemyContactOverlap = 0.8f;
        [Min(0.1f)] public float knightMeleeReach = 1.35f;
        [Tooltip("Archers only fire at enemies within this vertical distance (world units).")]
        [Min(0.5f)] public float archerRange = 3f;
        [Min(0.1f)] public float mageBandHalfHeight = 0.55f;
        [Min(0.1f)] public float bomberTriggerReach = 0.45f;
        [Min(0.1f)] public float bomberExplosionRadius = 0.95f;
        [Min(0.1f)] public float enemySpawnIntervalMin = 0.3f;
        [Min(0.1f)] public float enemySpawnIntervalMax = 0.5f;

        [Header("Base")]
        [Tooltip("Base health; enemies that get past the troops damage it. The run is lost at 0.")]
        [Min(1f)] public float baseMaxHP = 100f;

        [Header("Waves")]
        [Min(0f)] public float startWaveDelay = 0f;
        [Min(0.1f)] public float timeBetweenWaves = 1.5f;
        [Min(0.1f)] public float waveClearedBannerDuration = 1f;
        [Min(0.1f)] public float cardPickPlaceholderDuration = 1f;
        [Min(0.1f)] public float runEndRestartDelay = 2f;
    }
}
