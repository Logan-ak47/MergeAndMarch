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
        [SerializeField] private Transform enemyRoot;

        private Coroutine spawnRoutine;
        private int activeEnemyCount;
        private int spawnedThisWave;
        private int defeatedThisWave;
        private bool isSpawningWave;
        private bool waveClearedRaised;
        private int currentWave;

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
            int enemiesThisWave = GetEnemyCountForWave(waveNumber);
            Transform parent = enemyRoot != null ? enemyRoot : transform;

            for (int i = 0; i < enemiesThisWave; i++)
            {
                SpawnEnemyForWave(waveNumber, spawnY, failY, parent);
                if (i < enemiesThisWave - 1)
                {
                    float delay = UnityEngine.Random.Range(gameConfig.enemySpawnIntervalMin, gameConfig.enemySpawnIntervalMax);
                    yield return new WaitForSecondsRealtime(delay);
                }
            }

            isSpawningWave = false;
            spawnRoutine = null;
            EvaluateWaveCleared();
        }

        private void SpawnEnemyForWave(int waveNumber, float spawnY, float failY, Transform parent)
        {
            int column = UnityEngine.Random.Range(0, gameConfig.columns);
            Vector3 lanePosition = battleGrid.GetSlotWorldPosition(column, 0);
            float xOffset = UnityEngine.Random.Range(-gameConfig.cellSize * 0.18f, gameConfig.cellSize * 0.18f);
            Vector3 spawnPosition = new(lanePosition.x + xOffset, spawnY, 0f);

            float healthMultiplier = waveNumber == 16 ? 5f : 1f + ((waveNumber - 1) * 0.12f);
            float attackMultiplier = waveNumber == 16 ? 2f : 1f;
            float scaleMultiplier = waveNumber == 16 ? 1.45f : 1f;

            Enemy enemy = Instantiate(enemyPrefab, parent);
            enemy.Initialize(gruntData, gameConfig, battleGrid, this, column, spawnPosition, failY, healthMultiplier, attackMultiplier, scaleMultiplier);
            spawnedThisWave++;
            activeEnemyCount++;
        }

        private int GetEnemyCountForWave(int waveNumber)
        {
            if (waveNumber == 16)
            {
                return 1;
            }

            int index = Mathf.Clamp(waveNumber - 1, 0, RegularWaveCounts.Length - 1);
            return RegularWaveCounts[index];
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
    }
}

