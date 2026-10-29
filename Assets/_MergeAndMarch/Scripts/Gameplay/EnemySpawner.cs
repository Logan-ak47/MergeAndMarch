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
        private const string WaveConfigResourcePath = "MergeAndMarch/WaveConfig";

        [SerializeField] private GameConfig gameConfig;
        [Tooltip("Wave composition and threat curves; loaded from Resources/MergeAndMarch/WaveConfig when empty.")]
        [SerializeField] private WaveConfig waveConfig;
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
        /// <summary>Raised when an enemy walks past the troops into the base.</summary>
        public event Action<Enemy> EnemyEscapedEvent;

        public int CurrentWave => currentWave;

        /// <summary>Wave balance data (never null; falls back to defaults).</summary>
        public WaveConfig Waves
        {
            get
            {
                if (waveConfig == null)
                {
                    waveConfig = Resources.Load<WaveConfig>(WaveConfigResourcePath);
                }

                if (waveConfig == null)
                {
                    waveConfig = ScriptableObject.CreateInstance<WaveConfig>();
                }

                return waveConfig;
            }
        }

        /// <summary>The boss wave, which is also the last wave of a run.</summary>
        public int BossWave => Waves.bossWave;
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
            EnemyEscapedEvent?.Invoke(enemy);

            // An escaped enemy can be the last of the wave; without this the wave never clears.
            EvaluateWaveCleared();
        }

        private IEnumerator SpawnWaveRoutine(int waveNumber)
        {
            isSpawningWave = true;

            Bounds gridBounds = battleGrid.GetGridWorldBounds();
            float spawnY = gridBounds.max.y + gameConfig.enemySpawnYOffset;
            float failY = gridBounds.min.y - gameConfig.enemyFailOffset;
            Transform parent = enemyRoot != null ? enemyRoot : transform;

            if (waveNumber >= BossWave)
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
                        // Scaled time, so spawn pacing follows slow-mo and the balance harness's time scale.
                        float delay = Waves.SpawnInterval(waveNumber) * UnityEngine.Random.Range(0.9f, 1.1f);
                        yield return new WaitForSeconds(delay);
                    }
                }
            }

            isSpawningWave = false;
            spawnRoutine = null;
            EvaluateWaveCleared();
        }

        private void SpawnEnemy(EnemyData data, int waveNumber, float spawnY, float failY, Transform parent)
        {
            // The boss always comes down a lane that holds a troop, so the finale is a fight rather
            // than a coin flip on whether its random lane is empty.
            int column = waveNumber >= BossWave ? PickOccupiedColumn() : UnityEngine.Random.Range(0, Mathf.Max(1, gameConfig.columns));
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

            WaveConfig waves = Waves;
            bool isBoss = waveNumber >= waves.bossWave;
            float healthMultiplier = isBoss ? waves.bossHpMultiplier : waves.HpMultiplier(waveNumber);
            float attackMultiplier = isBoss ? waves.bossAttackMultiplier : waves.AttackMultiplier(waveNumber);
            float scaleMultiplier = isBoss ? data.sizeScale * waves.bossScaleMultiplier : data.sizeScale;
            float variantDampener = waveNumber <= waves.earlyVariantWaves && data.enemyType != EnemyType.Grunt ? waves.earlyVariantHpMultiplier : 1f;
            float speedMultiplier = 1f;
            if (data.enemyType == EnemyType.Rusher && waveNumber <= waves.rusherSlowWaves)
            {
                speedMultiplier = waves.rusherEarlySpeed;
            }
            else if (data.enemyType == EnemyType.Flyer && waveNumber <= waves.flyerSlowWaves)
            {
                speedMultiplier = waves.flyerEarlySpeed;
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

        /// <summary>A random lane containing a troop, preferring lanes with a front-row troop.</summary>
        private int PickOccupiedColumn()
        {
            int columns = Mathf.Max(1, gameConfig.columns);
            List<int> front = new();
            List<int> any = new();
            for (int column = 0; column < columns; column++)
            {
                for (int row = 0; row < gameConfig.rows; row++)
                {
                    Troop troop = battleGrid.GetTroopAt(column, row);
                    if (troop == null || !troop.IsAlive)
                    {
                        continue;
                    }

                    if (!any.Contains(column))
                    {
                        any.Add(column);
                    }

                    if (row == 0 && !front.Contains(column))
                    {
                        front.Add(column);
                    }
                }
            }

            List<int> pool = front.Count > 0 ? front : any;
            return pool.Count > 0 ? pool[UnityEngine.Random.Range(0, pool.Count)] : UnityEngine.Random.Range(0, columns);
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
            WaveConfig.WaveDefinition definition = Waves.GetWave(wave);
            List<EnemyData> enemies = new(definition.Total);
            AddEnemies(enemies, gruntData, definition.grunts);
            AddEnemies(enemies, r, definition.rushers);
            AddEnemies(enemies, t, definition.tanks);
            AddEnemies(enemies, f, definition.flyers);
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
