using System.Collections.Generic;
using MergeAndMarch.Data;
using MergeAndMarch.Gameplay;
using UnityEngine;

namespace MergeAndMarch.Core
{
    public class RunManager : MonoBehaviour
    {
        private enum StartingLineupPreset
        {
            DefaultBalanced = 0,
            AllKnights = 1,
            Experimental = 2
        }

        [SerializeField] private GameConfig gameConfig;
        [SerializeField] private BattleGrid battleGrid;
        [SerializeField] private Troop troopPrefab;
        [SerializeField] private TroopData knightData;
        [SerializeField] private TroopData archerData;
        [SerializeField] private TroopData mageData;
        [SerializeField] private TroopData healerData;
        [SerializeField] private TroopData bomberData;
        [SerializeField] private Transform troopRoot;
        [SerializeField] private bool spawnOnStart = true;
        [SerializeField] private bool enableDebugLineupHotkeys = true;
        [SerializeField] private StartingLineupPreset debugLineupPreset = StartingLineupPreset.DefaultBalanced;

        private WaveManager waveManager;
        private readonly TroopType[] startingLineupComposition = new TroopType[4];

        public GameConfig Config => gameConfig;
        public Transform TroopRoot => troopRoot != null ? troopRoot : transform;
        public Troop TroopPrefab => troopPrefab;
        public TroopData KnightData => knightData;
        public TroopData ArcherData => archerData;
        public TroopData MageData => mageData;
        public TroopData HealerData => healerData;
        public TroopData BomberData => bomberData;

        private void Update()
        {
            if (!enableDebugLineupHotkeys)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                debugLineupPreset = StartingLineupPreset.AllKnights;
                RestartRun();
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                debugLineupPreset = StartingLineupPreset.DefaultBalanced;
                RestartRun();
            }
            else if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                debugLineupPreset = StartingLineupPreset.Experimental;
                RestartRun();
            }
        }

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

            ConfigureStartingComposition();
            SpawnTroop(GetTroopData(startingLineupComposition[0]), leftCenterColumn, 0);
            SpawnTroop(GetTroopData(startingLineupComposition[1]), rightCenterColumn, 0);
            SpawnTroop(GetTroopData(startingLineupComposition[2]), leftCenterColumn, 1);
            SpawnTroop(GetTroopData(startingLineupComposition[3]), rightCenterColumn, 1);
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

        public TroopData GetTroopData(TroopType troopType)
        {
            return troopType switch
            {
                TroopType.Knight => knightData,
                TroopType.Archer => archerData,
                TroopType.Mage => mageData,
                TroopType.Healer => healerData,
                TroopType.Bomber => bomberData,
                _ => null
            };
        }

        public IReadOnlyList<TroopType> GetStartingLineupComposition()
        {
            ConfigureStartingComposition();
            return startingLineupComposition;
        }

        private void ConfigureStartingComposition()
        {
            switch (debugLineupPreset)
            {
                case StartingLineupPreset.AllKnights:
                    startingLineupComposition[0] = TroopType.Knight;
                    startingLineupComposition[1] = TroopType.Knight;
                    startingLineupComposition[2] = TroopType.Knight;
                    startingLineupComposition[3] = TroopType.Knight;
                    break;
                case StartingLineupPreset.Experimental:
                    startingLineupComposition[0] = TroopType.Mage;
                    startingLineupComposition[1] = TroopType.Mage;
                    startingLineupComposition[2] = TroopType.Healer;
                    startingLineupComposition[3] = TroopType.Bomber;
                    break;
                default:
                    startingLineupComposition[0] = TroopType.Knight;
                    startingLineupComposition[1] = TroopType.Knight;
                    startingLineupComposition[2] = TroopType.Archer;
                    startingLineupComposition[3] = TroopType.Archer;
                    break;
            }
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
