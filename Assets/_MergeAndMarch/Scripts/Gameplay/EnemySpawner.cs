using System.Collections;
using MergeAndMarch.Data;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MergeAndMarch.Gameplay
{
    public class EnemySpawner : MonoBehaviour
    {
        [SerializeField] private GameConfig gameConfig;
        [SerializeField] private BattleGrid battleGrid;
        [SerializeField] private Enemy enemyPrefab;
        [SerializeField] private EnemyData gruntData;
        [SerializeField] private Transform enemyRoot;
        [SerializeField] private bool spawnOnStart = true;

        private bool waveInProgress;
        private bool battleEnded;
        private int activeEnemyCount;
        private int currentWave;

        public int CurrentWave => currentWave;
        public bool BattleEnded => battleEnded;

        private void Awake()
        {
            ResolveReferences();
        }

        private IEnumerator Start()
        {
            if (!spawnOnStart)
            {
                yield break;
            }

            yield return new WaitForSecondsRealtime(gameConfig != null ? gameConfig.startWaveDelay : 0.5f);
            StartNextWave();
        }

        private void Update()
        {
            if (battleEnded)
            {
                return;
            }

            if (battleGrid == null || gameConfig == null || enemyPrefab == null || gruntData == null)
            {
                ResolveReferences();
                return;
            }

            if (!battleGrid.HasAnyTroops())
            {
                battleEnded = true;
                Debug.Log("Run failed: all troops were defeated.", this);
                return;
            }

            if (waveInProgress || activeEnemyCount > 0)
            {
                return;
            }

            StartCoroutine(BeginNextWaveAfterDelay());
        }

        public void NotifyEnemyDefeated(Enemy enemy)
        {
            activeEnemyCount = Mathf.Max(0, activeEnemyCount - 1);
            waveInProgress = false;
        }

        public void NotifyEnemyEscaped(Enemy enemy)
        {
            if (battleEnded)
            {
                return;
            }

            activeEnemyCount = Mathf.Max(0, activeEnemyCount - 1);
            battleEnded = true;
            Debug.Log("Run failed: an enemy passed below the grid.", this);
            if (enemy != null)
            {
                Destroy(enemy.gameObject);
            }
        }

        [ContextMenu("Spawn Next Wave")]
        public void StartNextWave()
        {
            ResolveReferences();
            if (battleEnded || gameConfig == null || battleGrid == null || enemyPrefab == null || gruntData == null)
            {
                return;
            }

            currentWave++;
            waveInProgress = true;

            Bounds gridBounds = battleGrid.GetGridWorldBounds();
            float spawnY = gridBounds.max.y + gameConfig.enemySpawnYOffset;
            float failY = gridBounds.min.y - gameConfig.enemyFailOffset;
            int enemiesThisWave = Mathf.Min(gameConfig.startingEnemyCount + ((currentWave - 1) / 2), gameConfig.maxEnemiesPerWave);

            Transform parent = enemyRoot != null ? enemyRoot : transform;
            for (int i = 0; i < enemiesThisWave; i++)
            {
                int column = Random.Range(0, gameConfig.columns);
                Vector3 lanePosition = battleGrid.GetSlotWorldPosition(column, 0);
                float yOffset = i * (gameConfig.cellSize * 0.9f);
                Vector3 spawnPosition = new(lanePosition.x, spawnY + yOffset, 0f);

                Enemy enemy = Instantiate(enemyPrefab, parent);
                enemy.Initialize(gruntData, gameConfig, battleGrid, this, column, spawnPosition, failY);
                activeEnemyCount++;
            }

            Debug.Log($"Wave {currentWave} started with {enemiesThisWave} grunt(s).", this);
            waveInProgress = false;
        }

        private void ResolveReferences()
        {
            if (battleGrid == null)
            {
                battleGrid = FindFirstObjectByType<BattleGrid>();
            }

            if (gameConfig == null && battleGrid != null)
            {
                gameConfig = battleGrid.Config;
            }

            if (enemyRoot == null)
            {
                GameObject rootObject = GameObject.Find("Enemies");
                if (rootObject != null)
                {
                    enemyRoot = rootObject.transform;
                }
            }

#if UNITY_EDITOR
            if (gruntData == null)
            {
                gruntData = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/_MergeAndMarch/ScriptableObjects/Grunt.asset");
            }

            if (enemyPrefab == null)
            {
                enemyPrefab = AssetDatabase.LoadAssetAtPath<Enemy>("Assets/_MergeAndMarch/Prefabs/EnemyPrefab.prefab");
            }
#endif
        }

        private IEnumerator BeginNextWaveAfterDelay()
        {
            waveInProgress = true;
            yield return new WaitForSecondsRealtime(gameConfig != null ? gameConfig.timeBetweenWaves : 1f);

            if (!battleEnded && activeEnemyCount == 0)
            {
                StartNextWave();
            }
            else
            {
                waveInProgress = false;
            }
        }
    }
}
