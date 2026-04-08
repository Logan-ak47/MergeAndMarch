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

        public bool DeployOneTroop()
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
            TroopData troopData = Random.value < 0.5f ? knightData : archerData;
            SpawnTroop(troopData, slot.x, slot.y);
            return true;
        }

        private void SpawnTroop(TroopData troopData, int column, int row)
        {
            Transform parent = troopRoot != null ? troopRoot : transform;
            Troop troopInstance = Instantiate(troopPrefab, parent);
            troopInstance.Initialize(troopData, column, row, gameConfig, 1);
            battleGrid.RegisterTroop(troopInstance, column, row);
            StartCoroutine(PlayDeployPopIn(troopInstance));
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
