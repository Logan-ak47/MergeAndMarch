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
        [Min(0.1f)] public float troopBaseScale = 3.6f;
        [Min(0.1f)] public float tierTwoScale = 4.3f;
        [Min(0.1f)] public float tierThreeScale = 5.0f;
        [Min(0.1f)] public float slotVisualScale = 0.88f;
        public Color slotTint = new(1f, 1f, 1f, 0.14f);
        public Color tierTwoTint = new(1f, 1f, 1f, 1f);
        public Color tierThreeTint = new(1f, 0.95f, 0.95f, 1f);

        [Header("Lane Visuals")]
        [Min(0.1f)] public float laneGuideHeight = 5.8f;
        [Range(0.02f, 0.5f)] public float laneGuideWidthScale = 0.1f;
        [Range(0.02f, 0.5f)] public float laneGuideMarkerScale = 0.16f;
        public Color laneGuideTint = new(1f, 1f, 1f, 0.08f);
        public Color laneGuideMarkerTint = new(1f, 1f, 1f, 0.14f);

        [Header("Merge")]
        [Range(0.01f, 1f)] public float tacticalSlowTimeScale = 0.2f;
        [Min(0.01f)] public float mergeSlideDuration = 0.15f;
        [Min(0.01f)] public float mergeFlashDuration = 0.1f;
        [Min(0.01f)] public float mergePopDuration = 0.2f;
        [Min(1f)] public float mergeOvershootScale = 1.3f;
        [Min(0.01f)] public float dragReturnDuration = 0.12f;
        [Min(0.01f)] public float swapMoveDuration = 0.12f;

        [Header("Combat")]
        [Min(0.1f)] public float enemyBaseScale = 3.2f;
        [Min(0.1f)] public float enemySpawnYOffset = 2.2f;
        [Min(0.1f)] public float enemyFailOffset = 0.8f;
        [Min(0.05f)] public float enemyEngageDistance = 0.3f;
        [Min(0.1f)] public float knightMeleeReach = 1.35f;
        [Min(0.1f)] public float enemySpawnIntervalMin = 0.3f;
        [Min(0.1f)] public float enemySpawnIntervalMax = 0.5f;

        [Header("Waves")]
        [Min(0f)] public float startWaveDelay = 0f;
        [Min(0.1f)] public float timeBetweenWaves = 1.5f;
        [Min(0.1f)] public float waveClearedBannerDuration = 1f;
        [Min(0.1f)] public float cardPickPlaceholderDuration = 1f;
        [Min(0.1f)] public float runEndRestartDelay = 2f;
    }
}
