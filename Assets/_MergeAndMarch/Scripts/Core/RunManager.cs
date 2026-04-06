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

        private void Start()
        {
            if (spawnOnStart)
            {
                SetupOpeningLineup();
            }
        }

        [ContextMenu("Setup Opening Lineup")]
        public void SetupOpeningLineup()
        {
            if (battleGrid == null || troopPrefab == null || knightData == null || archerData == null || gameConfig == null)
            {
                Debug.LogWarning("RunManager is missing one or more Session 1 references.", this);
                return;
            }

            Transform parent = troopRoot != null ? troopRoot : transform;
            battleGrid.ClearOccupants();

            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);

                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }

            int leftCenterColumn = Mathf.Max(0, (gameConfig.columns / 2) - 1);
            int rightCenterColumn = Mathf.Min(gameConfig.columns - 1, leftCenterColumn + 1);

            SpawnTroop(knightData, leftCenterColumn, 0, parent);
            SpawnTroop(knightData, rightCenterColumn, 0, parent);
            SpawnTroop(archerData, leftCenterColumn, 1, parent);
            SpawnTroop(archerData, rightCenterColumn, 1, parent);
        }

        private void SpawnTroop(TroopData troopData, int column, int row, Transform parent)
        {
            Troop troopInstance = Instantiate(troopPrefab, parent);
            troopInstance.Initialize(troopData, column, row, gameConfig, 1);
            battleGrid.RegisterTroop(troopInstance, column, row);
        }
    }
}
