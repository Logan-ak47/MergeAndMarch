using System.Collections.Generic;
using System.Collections;
using MergeAndMarch.Data;
using UnityEngine;

namespace MergeAndMarch.Gameplay
{
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(BoxCollider2D))]
    public class Enemy : MonoBehaviour
    {
        private static readonly List<Enemy> activeEnemyBuffer = new();
        private static Sprite fallbackSprite;

        private readonly List<Troop> troopBuffer = new();

        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private BoxCollider2D boxCollider;

        private GameConfig gameConfig;
        private BattleGrid battleGrid;
        private EnemySpawner owner;
        private float currentHP;
        private float attackTimer;
        private float failY;
        private bool hasEscaped;
        private Vector2 baseSpriteSize = Vector2.one * 0.16f;
        private Coroutine flashRoutine;
        private Coroutine attackPulseRoutine;
        private Vector3 lanePosition;

        public EnemyData Data { get; private set; }
        public int LaneColumn { get; private set; }
        public bool IsAlive => currentHP > 0.01f;
        public static IReadOnlyList<Enemy> ActiveEnemies => activeEnemyBuffer;

        private void Reset()
        {
            CacheComponents();
            ConfigureCollider();
        }

        private void Awake()
        {
            CacheComponents();
            CaptureBaseSpriteSize();
            ConfigureCollider();
        }

        private void OnEnable()
        {
            if (!activeEnemyBuffer.Contains(this))
            {
                activeEnemyBuffer.Add(this);
            }
        }

        private void OnDisable()
        {
            activeEnemyBuffer.Remove(this);
        }

        private void Update()
        {
            if (Data == null || battleGrid == null || gameConfig == null || !IsAlive)
            {
                return;
            }

            Troop blockingTroop = FindBlockingTroop();
            if (blockingTroop == null)
            {
                MoveForward();
                return;
            }

            attackTimer -= Time.deltaTime;
            if (attackTimer > 0f)
            {
                return;
            }

            attackTimer = Data.attackInterval;
            StartFlash(Color.Lerp(Data.tintColor, Color.white, 0.35f), 0.08f);
            StartAttackPulse();
            bool troopDied = blockingTroop.ApplyDamage(Data.baseAttack);
            if (troopDied)
            {
                battleGrid.RemoveTroop(blockingTroop);
                Destroy(blockingTroop.gameObject);
            }
        }

        public void Initialize(
            EnemyData enemyData,
            GameConfig config,
            BattleGrid grid,
            EnemySpawner enemySpawner,
            int laneColumn,
            Vector3 spawnPosition,
            float failLineY)
        {
            Data = enemyData;
            gameConfig = config;
            battleGrid = grid;
            owner = enemySpawner;
            LaneColumn = laneColumn;
            failY = failLineY;
            currentHP = Data != null ? Data.baseHP : 0f;
            attackTimer = Random.Range(0f, Mathf.Max(0.05f, Data != null ? Data.attackInterval * 0.5f : 0.25f));
            hasEscaped = false;
            lanePosition = spawnPosition;

            name = Data != null ? $"{Data.displayName}_{laneColumn}" : "Enemy";
            transform.position = spawnPosition;

            if (spriteRenderer != null && Data != null)
            {
                spriteRenderer.sprite = Data.sprite != null ? Data.sprite : GetFallbackSprite();
                spriteRenderer.color = Data.tintColor;
                spriteRenderer.drawMode = SpriteDrawMode.Simple;
                spriteRenderer.size = Vector2.one;
                spriteRenderer.sortingLayerName = "Enemies";
                spriteRenderer.sortingOrder = 0;
                CaptureBaseSpriteSize();
            }

            transform.localScale = Vector3.one * gameConfig.enemyBaseScale;
            ConfigureCollider();
        }

        public bool ApplyDamage(float damage)
        {
            if (!IsAlive)
            {
                return true;
            }

            currentHP = Mathf.Max(0f, currentHP - Mathf.Max(0f, damage));
            StartFlash(Color.white, 0.1f);
            if (!IsAlive)
            {
                owner?.NotifyEnemyDefeated(this);
                Destroy(gameObject);
                return true;
            }

            return false;
        }

        private void MoveForward()
        {
            transform.position += Vector3.down * (Data.moveSpeed * Time.deltaTime);
            lanePosition = transform.position;
            if (!hasEscaped && transform.position.y < failY)
            {
                hasEscaped = true;
                owner?.NotifyEnemyEscaped(this);
            }
        }

        private Troop FindBlockingTroop()
        {
            battleGrid.GetTroops(troopBuffer);

            Troop best = null;
            float bestY = float.NegativeInfinity;
            float engageY = transform.position.y - gameConfig.enemyEngageDistance;

            for (int i = 0; i < troopBuffer.Count; i++)
            {
                Troop troop = troopBuffer[i];
                if (troop == null || !troop.IsAlive || troop.Column != LaneColumn)
                {
                    continue;
                }

                float troopY = troop.transform.position.y;
                if (troopY > engageY)
                {
                    continue;
                }

                if (troopY > bestY)
                {
                    best = troop;
                    bestY = troopY;
                }
            }

            return best;
        }

        private void StartFlash(Color flashColor, float duration)
        {
            if (spriteRenderer == null)
            {
                return;
            }

            if (flashRoutine != null)
            {
                StopCoroutine(flashRoutine);
            }

            flashRoutine = StartCoroutine(FlashRoutine(flashColor, duration));
        }

        private void StartAttackPulse()
        {
            if (attackPulseRoutine != null)
            {
                StopCoroutine(attackPulseRoutine);
            }

            attackPulseRoutine = StartCoroutine(AttackPulseRoutine());
        }

        private IEnumerator FlashRoutine(Color flashColor, float duration)
        {
            Color original = spriteRenderer.color;
            spriteRenderer.color = flashColor;

            float elapsed = 0f;
            duration = Mathf.Max(0.01f, duration);
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                spriteRenderer.color = Color.Lerp(flashColor, original, t);
                yield return null;
            }

            spriteRenderer.color = original;
            flashRoutine = null;
        }

        private IEnumerator AttackPulseRoutine()
        {
            Vector3 start = lanePosition;
            Vector3 pulseTarget = start + (Vector3.down * 0.14f);
            float forwardDuration = 0.06f;
            float returnDuration = 0.08f;
            float elapsed = 0f;

            while (elapsed < forwardDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / forwardDuration);
                transform.position = Vector3.Lerp(start, pulseTarget, t);
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < returnDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / returnDuration);
                transform.position = Vector3.Lerp(pulseTarget, start, t);
                yield return null;
            }

            transform.position = start;
            attackPulseRoutine = null;
        }

        private static Sprite GetFallbackSprite()
        {
            if (fallbackSprite != null)
            {
                return fallbackSprite;
            }

            Texture2D texture = new(16, 16, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[16 * 16];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.white;
            }

            texture.SetPixels(pixels);
            texture.Apply();
            fallbackSprite = Sprite.Create(texture, new Rect(0f, 0f, 16f, 16f), new Vector2(0.5f, 0.5f), 100f);
            fallbackSprite.name = "EnemyFallbackSprite";
            return fallbackSprite;
        }

        private void CacheComponents()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            if (boxCollider == null)
            {
                boxCollider = GetComponent<BoxCollider2D>();
            }
        }

        private void CaptureBaseSpriteSize()
        {
            if (spriteRenderer != null && spriteRenderer.sprite != null)
            {
                baseSpriteSize = spriteRenderer.sprite.bounds.size;
            }
        }

        private void ConfigureCollider()
        {
            if (boxCollider == null)
            {
                return;
            }

            boxCollider.isTrigger = true;
            boxCollider.offset = Vector2.zero;
            boxCollider.size = baseSpriteSize * 0.9f;
        }
    }
}
