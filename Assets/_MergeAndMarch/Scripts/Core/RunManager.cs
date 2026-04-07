using MergeAndMarch.Data;
using MergeAndMarch.Gameplay;
using UnityEngine;

namespace MergeAndMarch.Core
{
    public class RunManager : MonoBehaviour
    {
        [SerializeField] private GameConfig gameConfig;
        [SerializeField] private BattleGrid battleGrid;
        [SerializeField] private Troop troopPrefab;
        [SerializeField] private TroopData knightData;
        [SerializeField] private TroopData archerData;
        [SerializeField] private Transform troopRoot;
        [SerializeField] private bool spawnOnStart = true;

        private WaveManager waveManager;

        public GameConfig Config => gameConfig;
        public Transform TroopRoot => troopRoot != null ? troopRoot : transform;
        public Troop TroopPrefab => troopPrefab;
        public TroopData KnightData => knightData;
        public TroopData ArcherData => archerData;

        private void Awake()
        {
            EnsureWaveManager();
        }

        private void Start()
        {
            if (spawnOnStart)
            {
                StartRun();
            }
        }

        public void StartRun()
        {
            SetupOpeningLineup();
            TimeScaleManager.Instance?.ResetTimeScale();
            EnsureWaveManager().BeginRun();
        }

        public void RestartRun()
        {
            StartRun();
        }

        [ContextMenu("Setup Opening Lineup")]
        public void SetupOpeningLineup()
        {
            if (battleGrid == null || troopPrefab == null || knightData == null || archerData == null || gameConfig == null)
            {
                Debug.LogWarning("RunManager is missing one or more starting-lineup references.", this);
                return;
            }

            Transform parent = TroopRoot;
            battleGrid.ClearOccupants();

            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                Destroy(child.gameObject);
            }

            int leftCenterColumn = Mathf.Max(0, (gameConfig.columns / 2) - 1);
            int rightCenterColumn = Mathf.Min(gameConfig.columns - 1, leftCenterColumn + 1);

            SpawnTroop(knightData, leftCenterColumn, 0);
            SpawnTroop(knightData, rightCenterColumn, 0);
            SpawnTroop(archerData, leftCenterColumn, 1);
            SpawnTroop(archerData, rightCenterColumn, 1);
        }

        public Troop SpawnTroop(TroopData troopData, int column, int row, int tier = 1)
        {
            if (troopData == null || troopPrefab == null || battleGrid == null || gameConfig == null)
            {
                return null;
            }

            Troop troopInstance = Instantiate(troopPrefab, TroopRoot);
            troopInstance.Initialize(troopData, column, row, gameConfig, tier);
            battleGrid.RegisterTroop(troopInstance, column, row);
            return troopInstance;
        }

        private WaveManager EnsureWaveManager()
        {
            if (waveManager == null)
            {
                waveManager = GetComponent<WaveManager>();
            }

            if (waveManager == null)
            {
                waveManager = gameObject.AddComponent<WaveManager>();
            }

            return waveManager;
        }
    }
}
