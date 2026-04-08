using System.Collections;
using System.Collections.Generic;
using MergeAndMarch.Core;
using MergeAndMarch.Data;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MergeAndMarch.Gameplay
{
    public class DeploymentSystem : MonoBehaviour
    {
        private readonly List<Vector2Int> emptySlots = new();

        [SerializeField] private GameConfig gameConfig;
        [SerializeField] private BattleGrid battleGrid;
        [SerializeField] private Troop troopPrefab;
        [SerializeField] private TroopData knightData;
        [SerializeField] private TroopData archerData;
        [SerializeField] private Transform troopRoot;

        public int DeployTroopsForNextWave()
        {
            ResolveReferences();
            int deployCount = 1;
            if (CardSystem.Instance != null)
            {
                deployCount += Mathf.Max(0, CardSystem.Instance.runBuffs.extraDeployCount);
                CardSystem.Instance.runBuffs.extraDeployCount = 0;
            }

            int deployed = 0;
            for (int i = 0; i < deployCount; i++)
            {
                if (!SpawnTroopInEmptySlot(null))
                {
                    break;
                }

                deployed++;
            }

            return deployed;
        }

        public bool DeployOneTroop()
        {
            return SpawnTroopInEmptySlot(null);
        }

        public bool SpawnTroopInEmptySlot(TroopType? forcedType, bool animate = true)
        {
            ResolveReferences();
            if (battleGrid == null || troopPrefab == null || gameConfig == null || knightData == null || archerData == null)
            {
                Debug.LogWarning("DeploymentSystem is missing one or more references.", this);
                return false;
            }

            battleGrid.GetEmptySlots(emptySlots);
            if (emptySlots.Count == 0)
            {
                return false;
            }

            Vector2Int slot = emptySlots[Random.Range(0, emptySlots.Count)];
            TroopData troopData = GetTroopData(forcedType);
            if (troopData == null)
            {
                return false;
            }

            SpawnTroop(troopData, slot.x, slot.y, animate);
            return true;
        }

        private void SpawnTroop(TroopData troopData, int column, int row, bool animate)
        {
            Transform parent = troopRoot != null ? troopRoot : transform;
            Troop troopInstance = Instantiate(troopPrefab, parent);
            troopInstance.Initialize(troopData, column, row, gameConfig, 1);
            battleGrid.RegisterTroop(troopInstance, column, row);

            if (animate)
            {
                StartCoroutine(PlayDeployPopIn(troopInstance));
            }
        }

        private TroopData GetTroopData(TroopType? forcedType)
        {
            if (!forcedType.HasValue)
            {
                return Random.value < 0.5f ? knightData : archerData;
            }

            return forcedType.Value switch
            {
                TroopType.Knight => knightData,
                TroopType.Archer => archerData,
                _ => Random.value < 0.5f ? knightData : archerData
            };
        }

        private IEnumerator PlayDeployPopIn(Troop troop)
        {
            if (troop == null)
            {
                yield break;
            }

            Vector3 targetScale = troop.transform.localScale;
            float duration = 0.2f;
            float elapsed = 0f;
            troop.transform.localScale = Vector3.zero;

            while (elapsed < duration)
            {
                if (troop == null)
                {
                    yield break;
                }

                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                troop.transform.localScale = Vector3.Lerp(Vector3.zero, targetScale, t);
                yield return null;
            }

            if (troop != null)
            {
                troop.transform.localScale = targetScale;
            }
        }

        private void ResolveReferences()
        {
            RunManager runManager = FindFirstObjectByType<RunManager>();

            if (battleGrid == null)
            {
                battleGrid = FindFirstObjectByType<BattleGrid>();
            }

            if (gameConfig == null)
            {
                gameConfig = battleGrid != null ? battleGrid.Config : (runManager != null ? runManager.Config : null);
            }

            if (troopPrefab == null && runManager != null)
            {
                troopPrefab = runManager.TroopPrefab;
            }

            if (knightData == null && runManager != null)
            {
                knightData = runManager.KnightData;
            }

            if (archerData == null && runManager != null)
            {
                archerData = runManager.ArcherData;
            }

            if (troopRoot == null && runManager != null)
            {
                troopRoot = runManager.TroopRoot;
            }

#if UNITY_EDITOR
            if (troopPrefab == null)
            {
                troopPrefab = AssetDatabase.LoadAssetAtPath<Troop>("Assets/_MergeAndMarch/Prefabs/TroopPrefab.prefab");
            }

            if (knightData == null)
            {
                knightData = AssetDatabase.LoadAssetAtPath<TroopData>("Assets/_MergeAndMarch/ScriptableObjects/Knight.asset");
            }

            if (archerData == null)
            {
                archerData = AssetDatabase.LoadAssetAtPath<TroopData>("Assets/_MergeAndMarch/ScriptableObjects/Archer.asset");
            }
#endif
        }
    }
}
