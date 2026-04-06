using MergeAndMarch.Data;
using System.Collections;
using UnityEngine;

namespace MergeAndMarch.Gameplay
{
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(BoxCollider2D))]
    public class Troop : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private BoxCollider2D boxCollider;

        public TroopData Data { get; private set; }
        public int Tier { get; private set; } = 1;
        public int Column { get; private set; }
        public int Row { get; private set; }
        public SpriteRenderer Renderer => spriteRenderer;
        public BoxCollider2D Collider => boxCollider;
        public float CurrentHP => currentHP;
        public float MaxHP => Data == null ? 0f : Data.baseHP * GetTierStatMultiplier();
        public bool IsAlive => currentHP > 0.01f;

        private Color baseColor;
        private int defaultSortingOrder;
        private GameConfig currentConfig;
        private Vector2 baseSpriteSize = Vector2.one * 0.16f;
        private float currentSizeMultiplier = 1f;
        private float visualSizeBoost = 1f;
        private float currentHP;
        private Coroutine flashRoutine;
        private Coroutine attackMotionRoutine;
        private bool isDragging;

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

        public void Initialize(TroopData troopData, int column, int row, GameConfig config, int tier = 1)
        {
            Data = troopData;
            Column = column;
            Row = row;
            Tier = Mathf.Clamp(tier, 1, 3);
            baseColor = troopData.troopColor;
            currentConfig = config;
            visualSizeBoost = 1f;

            name = $"{troopData.displayName}_{column}_{row}";

            if (troopData.sprite != null)
            {
                spriteRenderer.sprite = troopData.sprite;
            }

            CaptureBaseSpriteSize();
            spriteRenderer.color = baseColor;
            spriteRenderer.drawMode = SpriteDrawMode.Sliced;
            defaultSortingOrder = spriteRenderer.sortingOrder;

            transform.localScale = Vector3.one;
            ApplyTierVisuals(config);
            ConfigureCollider();
            currentHP = MaxHP;
        }

        public void SetGridPosition(Vector3 worldPosition, int column, int row)
        {
            Column = column;
            Row = row;
            transform.position = worldPosition;
        }

        public void UpgradeTier(GameConfig config)
        {
            currentConfig = config;
            Tier = Mathf.Clamp(Tier + 1, 1, 3);
            visualSizeBoost = 1f;
            ApplyTierVisuals(config);
            ConfigureCollider();
            currentHP = MaxHP;
        }

        public bool CanMergeWith(Troop other)
        {
            return other != null && other != this && Data != null && other.Data != null && Data.troopType == other.Data.troopType && Tier == other.Tier && Tier < 3;
        }

        public void SetDragging(bool isDraggingNow)
        {
            isDragging = isDraggingNow;
            if (spriteRenderer == null)
            {
                return;
            }

            spriteRenderer.sortingOrder = isDraggingNow ? 20 : defaultSortingOrder;
        }

        public void SetVisualSizeBoost(float boost)
        {
            visualSizeBoost = Mathf.Max(0.01f, boost);
            RefreshVisualSize();
        }

        public void ResetVisualSizeBoost()
        {
            visualSizeBoost = 1f;
            RefreshVisualSize();
        }

        public float GetTierStatMultiplier()
        {
            return Tier switch
            {
                2 => 3f,
                3 => 9f,
                _ => 1f
            };
        }

        public float GetAttackIntervalMultiplier()
        {
            return Tier switch
            {
                2 => 0.9f,
                3 => 0.8f,
                _ => 1f
            };
        }

        public float GetAttackDamage()
        {
            return Data == null ? 0f : Data.baseAttack * GetTierStatMultiplier();
        }

        public float GetAttackInterval()
        {
            return Data == null ? 1f : Data.attackInterval * GetAttackIntervalMultiplier();
        }

        public void PlayAttackFeedback(Vector3 targetWorldPosition)
        {
            StartFlash(Color.Lerp(baseColor, Color.white, 0.45f), 0.08f);

            if (isDragging)
            {
                return;
            }

            if (attackMotionRoutine != null)
            {
                StopCoroutine(attackMotionRoutine);
            }

            attackMotionRoutine = StartCoroutine(AttackMotionRoutine(targetWorldPosition));
        }

        public bool ApplyDamage(float damage)
        {
            if (!IsAlive)
            {
                return true;
            }

            currentHP = Mathf.Max(0f, currentHP - Mathf.Max(0f, damage));
            StartFlash(Color.white, 0.1f);
            return !IsAlive;
        }

        private void ApplyTierVisuals(GameConfig config)
        {
            if (config == null || spriteRenderer == null || Data == null)
            {
                return;
            }

            currentSizeMultiplier = config.troopBaseScale;
            Color tint = baseColor;

            if (Tier == 2)
            {
                currentSizeMultiplier = config.tierTwoScale;
                tint = Color.Lerp(baseColor, config.tierTwoTint, 0.25f);
            }
            else if (Tier == 3)
            {
                currentSizeMultiplier = config.tierThreeScale;
                tint = Color.Lerp(baseColor, config.tierThreeTint, 0.4f);
            }

            transform.localScale = Vector3.one;
            spriteRenderer.color = tint;
            RefreshVisualSize();
        }

        private void RefreshVisualSize()
        {
            if (spriteRenderer == null)
            {
                return;
            }

            spriteRenderer.size = baseSpriteSize * currentSizeMultiplier * visualSizeBoost;
            ConfigureCollider();
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

        private IEnumerator AttackMotionRoutine(Vector3 targetWorldPosition)
        {
            Vector3 start = transform.position;
            Vector3 direction = (targetWorldPosition - start).normalized;
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = Vector3.up;
            }

            Vector3 firstTarget;
            float firstDuration;
            float secondDuration;

            switch (Data.troopType)
            {
                case TroopType.Knight:
                    firstTarget = start + (direction * 0.16f);
                    firstDuration = 0.07f;
                    secondDuration = 0.09f;
                    break;
                case TroopType.Archer:
                    firstTarget = start - (direction * 0.08f);
                    firstDuration = 0.05f;
                    secondDuration = 0.1f;
                    break;
                default:
                    firstTarget = start + (direction * 0.08f);
                    firstDuration = 0.05f;
                    secondDuration = 0.08f;
                    break;
            }

            float elapsed = 0f;
            while (elapsed < firstDuration)
            {
                if (isDragging)
                {
                    transform.position = start;
                    attackMotionRoutine = null;
                    yield break;
                }

                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / firstDuration);
                transform.position = Vector3.Lerp(start, firstTarget, t);
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < secondDuration)
            {
                if (isDragging)
                {
                    transform.position = start;
                    attackMotionRoutine = null;
                    yield break;
                }

                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / secondDuration);
                transform.position = Vector3.Lerp(firstTarget, start, t);
                yield return null;
            }

            transform.position = start;
            attackMotionRoutine = null;
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

            if (boxCollider == null)
            {
                boxCollider = gameObject.AddComponent<BoxCollider2D>();
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
            boxCollider.size = baseSpriteSize * currentSizeMultiplier * visualSizeBoost * 0.85f;
        }
    }
}
