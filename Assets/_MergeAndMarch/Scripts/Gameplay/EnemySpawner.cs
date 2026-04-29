using System;
using System.Collections;
using System.Collections.Generic;
using MergeAndMarch.Data;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MergeAndMarch.Gameplay
{
    public class EnemySpawner : MonoBehaviour
    {
        private static readonly int[] RegularWaveCounts = { 3, 3, 4, 5, 6, 6, 7, 8, 8, 9, 10, 10, 11, 12, 12 };

        [SerializeField] private GameConfig gameConfig;
        [SerializeField] private BattleGrid battleGrid;
        [SerializeField] private Enemy enemyPrefab;
        [SerializeField] private EnemyData gruntData;
        [SerializeField] private EnemyData rusherData;
        [SerializeField] private EnemyData tankData;
        [SerializeField] private EnemyData flyerData;
        [SerializeField] private EnemyData bossData;
        [SerializeField] private Transform enemyRoot;

        private Coroutine spawnRoutine;
        private int activeEnemyCount;
        private int spawnedThisWave;
        private int defeatedThisWave;
        private bool isSpawningWave;
        private bool waveClearedRaised;
        private int currentWave;
        private readonly List<int> waveSpawnColumns = new();

        public event Action<int> WaveClearedEvent;
        public event Action EnemyEscapedEvent;

        public int CurrentWave => currentWave;
        public int ActiveEnemyCount => activeEnemyCount;
        public int SpawnedThisWave => spawnedThisWave;
        public int DefeatedThisWave => defeatedThisWave;
        public bool WaveCleared => !isSpawningWave && spawnedThisWave > 0 && activeEnemyCount <= 0;

        private void Awake()
        {
            ResolveReferences();
        }

        public void SpawnWave(int waveNumber)
        {
            ResolveReferences();
            if (battleGrid == null || gameConfig == null || enemyPrefab == null || gruntData == null)
            {
                Debug.LogWarning("EnemySpawner is missing references for SpawnWave.", this);
                return;
            }

            if (spawnRoutine != null)
            {
                StopCoroutine(spawnRoutine);
            }

            ResetWaveCounters();
            currentWave = waveNumber;
            spawnRoutine = StartCoroutine(SpawnWaveRoutine(waveNumber));
        }

        public void ResetSpawner()
        {
            if (spawnRoutine != null)
            {
                StopCoroutine(spawnRoutine);
                spawnRoutine = null;
            }

            ResetWaveCounters();
            currentWave = 0;

            List<Enemy> enemies = new(Enemy.ActiveEnemies.Count);
            for (int i = 0; i < Enemy.ActiveEnemies.Count; i++)
            {
                Enemy enemy = Enemy.ActiveEnemies[i];
                if (enemy != null)
                {
                    enemies.Add(enemy);
                }
            }

            for (int i = 0; i < enemies.Count; i++)
            {
                Destroy(enemies[i].gameObject);
            }

            if (enemyRoot != null)
            {
                for (int i = enemyRoot.childCount - 1; i >= 0; i--)
                {
                    Destroy(enemyRoot.GetChild(i).gameObject);
                }
            }
        }

        public void NotifyEnemyDefeated(Enemy enemy)
        {
            defeatedThisWave++;
            activeEnemyCount = Mathf.Max(0, activeEnemyCount - 1);
            EvaluateWaveCleared();
        }

        public void NotifyEnemyEscaped(Enemy enemy)
        {
            activeEnemyCount = Mathf.Max(0, activeEnemyCount - 1);
            EnemyEscapedEvent?.Invoke();
        }

        private IEnumerator SpawnWaveRoutine(int waveNumber)
        {
            isSpawningWave = true;

            Bounds gridBounds = battleGrid.GetGridWorldBounds();
            float spawnY = gridBounds.max.y + gameConfig.enemySpawnYOffset;
            float failY = gridBounds.min.y - gameConfig.enemyFailOffset;
            Transform parent = enemyRoot != null ? enemyRoot : transform;

            if (waveNumber == 16)
            {
                SpawnEnemy(bossData != null ? bossData : gruntData, waveNumber, spawnY, failY, parent);
            }
            else
            {
                List<EnemyData> waveEnemies = GetWaveEnemies(waveNumber);
                BuildWaveSpawnColumns(waveEnemies.Count);
                for (int i = 0; i < waveEnemies.Count; i++)
                {
                    SpawnEnemyAtColumn(waveEnemies[i], waveNumber, spawnY, failY, parent, waveSpawnColumns[i], i);
                    if (i < waveEnemies.Count - 1)
                    {
                        float delay = UnityEngine.Random.Range(gameConfig.enemySpawnIntervalMin, gameConfig.enemySpawnIntervalMax);
                        yield return new WaitForSecondsRealtime(delay);
                    }
                }
            }

            isSpawningWave = false;
            spawnRoutine = null;
            EvaluateWaveCleared();
        }

        private void SpawnEnemy(EnemyData data, int waveNumber, float spawnY, float failY, Transform parent)
        {
            int column = UnityEngine.Random.Range(0, Mathf.Max(1, gameConfig.columns));
            SpawnEnemyAtColumn(data, waveNumber, spawnY, failY, parent, column, spawnedThisWave);
        }

        private void SpawnEnemyAtColumn(EnemyData data, int waveNumber, float spawnY, float failY, Transform parent, int column, int spawnIndex)
        {
            if (data == null)
            {
                data = gruntData;
            }

            column = Mathf.Clamp(column, 0, Mathf.Max(0, gameConfig.columns - 1));
            Vector3 lanePos = battleGrid.GetSlotWorldPosition(column, 0);
            float xOffset = GetSpawnXOffset(spawnIndex);
            Vector3 spawnPosition = new(lanePos.x + xOffset, spawnY, 0f);

            float healthMultiplier = waveNumber == 16 ? 5f : 1f + ((waveNumber - 1) * 0.12f);
            float attackMultiplier = waveNumber == 16 ? 2f : 1f;
            float scaleMultiplier = waveNumber == 16 ? data.sizeScale * 1.45f : data.sizeScale;
            float variantDampener = waveNumber <= 5 && data.enemyType != EnemyType.Grunt ? 0.75f : 1f;
            float speedMultiplier = 1f;
            if (data.enemyType == EnemyType.Rusher && waveNumber <= 3)
            {
                speedMultiplier = 0.8f;
            }
            else if (data.enemyType == EnemyType.Flyer && waveNumber <= 5)
            {
                speedMultiplier = 0.7f;
            }

            Enemy enemy = Instantiate(enemyPrefab, parent);
            enemy.Initialize(
                data,
                gameConfig,
                battleGrid,
                this,
                column,
                spawnPosition,
                failY,
                healthMultiplier * variantDampener,
                attackMultiplier,
                scaleMultiplier,
                speedMultiplier);
            spawnedThisWave++;
            activeEnemyCount++;
        }

        private float GetSpawnXOffset(int spawnIndex)
        {
            float randomOffset = UnityEngine.Random.Range(-gameConfig.cellSize * 0.12f, gameConfig.cellSize * 0.12f);
            float deterministicNudge = ((spawnIndex % 5) - 2) * gameConfig.cellSize * 0.01f;
            return Mathf.Clamp(randomOffset + deterministicNudge, -gameConfig.cellSize * 0.18f, gameConfig.cellSize * 0.18f);
        }

        private List<EnemyData> GetWaveEnemies(int wave)
        {
            EnemyData r = rusherData != null ? rusherData : gruntData;
            EnemyData t = tankData != null ? tankData : gruntData;
            EnemyData f = flyerData != null ? flyerData : gruntData;
            List<EnemyData> enemies = new(RegularWaveCounts[Mathf.Clamp(wave - 1, 0, RegularWaveCounts.Length - 1)]);

            if (wave == 1)
            {
                AddEnemies(enemies, gruntData, 3);
            }
            else if (wave == 2)
            {
                AddEnemies(enemies, gruntData, 3);
                AddEnemies(enemies, r, 1);
            }
            else if (wave == 3)
            {
                AddEnemies(enemies, gruntData, 4);
                AddEnemies(enemies, t, 1);
            }
            else if (wave == 4)
            {
                AddEnemies(enemies, gruntData, 4);
                AddEnemies(enemies, r, 2);
            }
            else if (wave == 5)
            {
                AddEnemies(enemies, gruntData, 4);
                AddEnemies(enemies, t, 1);
                AddEnemies(enemies, f, 1);
            }
            else if (wave == 6)
            {
                AddEnemies(enemies, gruntData, 3);
                AddEnemies(enemies, r, 2);
                AddEnemies(enemies, t, 1);
            }
            else if (wave == 7)
            {
                AddEnemies(enemies, gruntData, 3);
                AddEnemies(enemies, r, 1);
                AddEnemies(enemies, t, 2);
                AddEnemies(enemies, f, 1);
            }
            else if (wave == 8)
            {
                AddEnemies(enemies, gruntData, 2);
                AddEnemies(enemies, r, 2);
                AddEnemies(enemies, t, 1);
                AddEnemies(enemies, f, 2);
            }
            else if (wave <= 10)
            {
                AddEnemies(enemies, gruntData, 3);
                AddEnemies(enemies, r, 2);
                AddEnemies(enemies, t, 2);
                AddEnemies(enemies, f, 2);
            }
            else if (wave <= 12)
            {
                AddEnemies(enemies, gruntData, 2);
                AddEnemies(enemies, r, 3);
                AddEnemies(enemies, t, 2);
                AddEnemies(enemies, f, 3);
            }
            else if (wave <= 15)
            {
                AddEnemies(enemies, gruntData, 2);
                AddEnemies(enemies, r, 3);
                AddEnemies(enemies, t, 3);
                AddEnemies(enemies, f, 4);
            }
            else
            {
                AddEnemies(enemies, gruntData, RegularWaveCounts[Mathf.Clamp(wave - 1, 0, RegularWaveCounts.Length - 1)]);
            }

            return enemies;
        }

        private static void AddEnemies(List<EnemyData> enemies, EnemyData data, int count)
        {
            for (int i = 0; i < count; i++)
            {
                enemies.Add(data);
            }
        }

        private void BuildWaveSpawnColumns(int enemyCount)
        {
            int columnCount = Mathf.Max(1, gameConfig.columns);
            waveSpawnColumns.Clear();

            int[] columnOrder = new int[columnCount];
            for (int i = 0; i < columnOrder.Length; i++)
            {
                columnOrder[i] = i;
            }

            ShuffleColumns(columnOrder);
            for (int i = 0; i < enemyCount; i++)
            {
                waveSpawnColumns.Add(columnOrder[i % columnOrder.Length]);
            }
        }

        private static void ShuffleColumns(int[] columns)
        {
            for (int i = columns.Length - 1; i > 0; i--)
            {
                int swapIndex = UnityEngine.Random.Range(0, i + 1);
                (columns[i], columns[swapIndex]) = (columns[swapIndex], columns[i]);
            }
        }

        private void EvaluateWaveCleared()
        {
            if (!WaveCleared || waveClearedRaised)
            {
                return;
            }

            waveClearedRaised = true;
            WaveClearedEvent?.Invoke(currentWave);
        }

        private void ResetWaveCounters()
        {
            activeEnemyCount = 0;
            spawnedThisWave = 0;
            defeatedThisWave = 0;
            isSpawningWave = false;
            waveClearedRaised = false;
            waveSpawnColumns.Clear();
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

            if (rusherData == null)
            {
                rusherData = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/_MergeAndMarch/ScriptableObjects/Rusher.asset");
            }

            if (tankData == null)
            {
                tankData = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/_MergeAndMarch/ScriptableObjects/Tank.asset");
            }

            if (flyerData == null)
            {
                flyerData = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/_MergeAndMarch/ScriptableObjects/Flyer.asset");
            }

            if (bossData == null)
            {
                bossData = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/_MergeAndMarch/ScriptableObjects/Boss.asset");
            }

            if (enemyPrefab == null)
            {
                enemyPrefab = AssetDatabase.LoadAssetAtPath<Enemy>("Assets/_MergeAndMarch/Prefabs/EnemyPrefab.prefab");
            }
#endif
        }
    }
}
