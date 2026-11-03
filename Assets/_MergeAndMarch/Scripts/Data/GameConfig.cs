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
        [Range(0.2f, 1f)] public float laneStripWidthScale = 0.86f;
        [Tooltip("Spawn gate sits this far above the enemy spawn line.")]
        public float laneGateOffset = 0.35f;
        [Min(0.05f)] public float laneGateHeight = 0.5f;
        public Color laneGateTint = new(0.114f, 0.078f, 0.259f, 0.85f);
        public Color laneGateMouthTint = new(0.941f, 0.157f, 0.247f, 0.75f);

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
        [Min(0.1f)] public float enemySpawnYOffset = 3.3f;
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

        [Header("Lane defence (design/specs/lane-defence.md)")]
        [Tooltip("Front-row Knights pull enemies from lanes within this many columns into their own lane.")]
        [Min(0)] public int zocLanes = 1;
        [Tooltip("Most enemies one Knight can have pulled in at once; extras walk past.")]
        [Min(0)] public int zocCapacity = 2;
        [Tooltip("Extra vertical reach (units) beyond contact distance at which a Knight starts pulling.")]
        [Min(0f)] public float zocReachExtra = 0.15f;
        [Min(0.01f)] public float zocPullDuration = 0.25f;
        [Tooltip("Last stand applies when this many or fewer lanes still hold a troop.")]
        [Min(0)] public int lastStandOccupiedLanes = 1;
        [Tooltip("Where enemies decide to turn: fraction of the way from the spawn line down to the grid top.")]
        [Range(0f, 1f)] public float lastStandLineFraction = 0.5f;
        [Min(0.1f)] public float lastStandLateralSpeedMult = 1.5f;
        public Color laneThreatTint = new(0.941f, 0.157f, 0.247f, 0.32f);
        [Min(0.05f)] public float laneThreatPulsePeriod = 0.6f;

        [Header("Base")]
        [Tooltip("Base health; enemies that get past the troops damage it. The run is lost at 0.")]
        [Min(1f)] public float baseMaxHP = 100f;
        [Tooltip("Distance from the bottom edge of the grid to the egg's centre (direction 2026-11-03).")]
        [Min(0f)] public float eggOffset = 0.5f;
        [Min(0.1f)] public float eggSize = 0.95f;
        [Tooltip("HP fractions at which the egg shows crack state 1 and 2.")]
        public float[] eggCrackRatios = { 0.67f, 0.34f };

        [Header("Offer tray (design/specs/offer-tray.md)")]
        [Tooltip("Distance from the bottom edge of the grid to the centre of the tray cards.")]
        [Min(0.1f)] public float trayOffset = 1.85f;
        [Min(0.1f)] public float trayCardWidth = 1.2f;
        [Min(0.1f)] public float trayCardHeight = 0.95f;
        [Min(1)] public int draftOfferCount = 3;
        [Tooltip("DraftThreshold(n) = min(energyBase + energyStep * n, energyCap), n = drafts taken this run.")]
        [Min(1f)] public float energyBase = 6f;
        [Min(0f)] public float energyStep = 1f;
        [Min(1f)] public float energyCap = 14f;
        [Tooltip("Bonus kill energy on each wave clear, as a fraction of the current draft threshold.")]
        [Range(0f, 1f)] public float waveClearEnergyFraction = 0.5f;
        [Tooltip("Fraction of the threshold refunded when a card replaces a troop on a full grid.")]
        [Range(0f, 1f)] public float replaceRefundFraction = 0.5f;
        [Tooltip("Time scale of the short slow-mo pulse when an offer appears.")]
        [Range(0.01f, 1f)] public float offerSlowMoScale = 0.3f;
        [Min(0f)] public float offerSlowMoDuration = 1f;
        public bool freeDraftAtRunStart = true;
        [Min(0f)] public float baseHealOnWaveClear = 10f;
        [Tooltip("Card picks happen after these waves.")]
        public int[] cardPickWaves = { };

        /// <summary>Kill energy needed for the next draft (design/specs/bench-draft-chapters.md, Formulas).</summary>
        public float DraftThreshold(int draftsTaken)
        {
            return Mathf.Min(energyBase + (energyStep * Mathf.Max(0, draftsTaken)), Mathf.Max(energyBase, energyCap));
        }

        [Header("Waves")]
        [Min(0f)] public float startWaveDelay = 0f;
        [Min(0.1f)] public float timeBetweenWaves = 1.5f;
        [Min(0.1f)] public float waveClearedBannerDuration = 1f;
        [Min(0.1f)] public float cardPickPlaceholderDuration = 1f;
        [Min(0.1f)] public float runEndRestartDelay = 2f;
    }
}
