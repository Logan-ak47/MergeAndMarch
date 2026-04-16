using System.Collections.Generic;
using MergeAndMarch.Data;
using System.Collections;
using UnityEngine;

namespace MergeAndMarch.Gameplay
{
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(BoxCollider2D))]
    public class Troop : MonoBehaviour
    {
        private static Sprite runtimeCircleSprite;

        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private BoxCollider2D boxCollider;

        public TroopData Data { get; private set; }
        public int Tier { get; private set; } = 1;
        public int Column { get; private set; }
        public int Row { get; private set; }
        public SpriteRenderer Renderer => spriteRenderer;
        public BoxCollider2D Collider => boxCollider;
        public float CurrentHP => currentHP;
        public float MaxHP => Data == null ? 0f : Data.baseHP * GetTierStatMultiplier() * GetHpMultiplier();
        public bool IsAlive => currentHP > 0.01f;
        public bool IsCombatActive => IsAlive && !hasExplodedThisWave;
        public bool IsInteractable => IsAlive && !hasExplodedThisWave;
        public bool IsSingleUse => Data != null && Data.troopType == TroopType.Bomber;
        public bool HasExplodedThisWave => hasExplodedThisWave;

        private Color baseColor;
        private int defaultSortingOrder;
        private GameConfig currentConfig;
        private Vector2 baseSpriteSize = Vector2.one * 0.16f;
        private float currentSizeMultiplier = 1f;
        private float visualSizeBoost = 1f;
        private float currentHP;
        private BattleGrid assignedBattleGrid;
        private Coroutine flashRoutine;
        private Coroutine attackMotionRoutine;
        private Coroutine bomberFadeRoutine;
        private bool isDragging;
        private bool hasExplodedThisWave;

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

        private void OnDestroy()
        {
            assignedBattleGrid?.RemoveTroop(this);
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
            hasExplodedThisWave = false;

            name = $"{troopData.displayName}_{column}_{row}";

            if (troopData.sprite != null)
            {
                spriteRenderer.sprite = troopData.sprite;
            }

            CaptureBaseSpriteSize();
            spriteRenderer.color = baseColor;
            spriteRenderer.drawMode = SpriteDrawMode.Simple;
            defaultSortingOrder = spriteRenderer.sortingOrder;
            ApplyTierVisuals(config);
            ConfigureCollider();
            currentHP = MaxHP;
            SetBomberState(false);
        }

        public void SetGridPosition(Vector3 worldPosition, int column, int row)
        {
            Column = column;
            Row = row;
            transform.position = worldPosition;
        }

        public void SetBattleGrid(BattleGrid grid)
        {
            assignedBattleGrid = grid;
        }

        public void UpgradeTier(GameConfig config)
        {
            float oldMaxHp = MaxHP;
            currentConfig = config;
            Tier = Mathf.Clamp(Tier + 1, 1, 3);
            visualSizeBoost = 1f;
            ApplyTierVisuals(config);
            ConfigureCollider();
            RefreshCurrentHpForMaxHpChange(oldMaxHp, true);
        }

        public bool CanMergeWith(Troop other)
        {
            return other != null
                && other != this
                && Data != null
                && other.Data != null
                && IsInteractable
                && other.IsInteractable
                && Data.troopType == other.Data.troopType
                && Tier == other.Tier
                && Tier < 3;
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

        public float GetSupportPower()
        {
            return Data == null ? 0f : Data.supportPower * GetTierStatMultiplier();
        }

        public float GetAttackInterval()
        {
            return Data == null ? 1f : Data.attackInterval * GetAttackIntervalMultiplier();
        }

        public void RefreshCurrentHpForMaxHpChange(float oldMaxHp, bool healDifference)
        {
            float newMaxHp = MaxHP;
            if (healDifference)
            {
                currentHP += Mathf.Max(0f, newMaxHp - oldMaxHp);
            }

            currentHP = Mathf.Clamp(currentHP, 0f, newMaxHp);
        }

        public void HealPercent(float percent)
        {
            if (!IsAlive)
            {
                return;
            }

            HealFlat(MaxHP * Mathf.Max(0f, percent));
        }

        public void HealFlat(float amount)
        {
            if (!IsAlive)
            {
                return;
            }

            currentHP = Mathf.Clamp(currentHP + Mathf.Max(0f, amount), 0f, MaxHP);
            StartFlash(Color.Lerp(baseColor, Color.white, 0.55f), 0.12f);
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
            if (!IsCombatActive)
            {
                return true;
            }

            currentHP = Mathf.Max(0f, currentHP - Mathf.Max(0f, damage));
            StartFlash(Color.white, 0.1f);
            return !IsAlive;
        }

        public void ResetForWaveStart()
        {
            RespawnBomber();
        }

        public void RespawnBomber()
        {
            if (!IsSingleUse)
            {
                return;
            }

            if (bomberFadeRoutine != null)
            {
                StopCoroutine(bomberFadeRoutine);
                bomberFadeRoutine = null;
            }

            SetBomberState(false);
        }

        public void ConsumeBomber()
        {
            if (!IsSingleUse || hasExplodedThisWave)
            {
                return;
            }

            if (bomberFadeRoutine != null)
            {
                StopCoroutine(bomberFadeRoutine);
            }

            bomberFadeRoutine = StartCoroutine(FadeBomberOutRoutine());
        }

        public bool TriggerExplosion()
        {
            if (!IsSingleUse || hasExplodedThisWave || !IsAlive)
            {
                return false;
            }

            float damage = GetAttackDamage();
            RunBuffs buffs = CardSystem.Instance != null ? CardSystem.Instance.runBuffs : null;
            if (buffs != null)
            {
                damage *= buffs.attackMultiplier;
                damage *= buffs.bomberAttackMultiplier;
            }

            float radius = GetBomberExplosionRadius();
            IReadOnlyList<Enemy> enemies = Enemy.ActiveEnemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                Enemy enemy = enemies[i];
                if (enemy == null || !enemy.IsAlive)
                {
                    continue;
                }

                if (Vector2.Distance(transform.position, enemy.transform.position) <= radius)
                {
                    enemy.ApplyDamage(damage);
                }
            }

            StartCoroutine(PlayBomberExplosionRoutine(radius));
            ConsumeBomber();
            return true;
        }

        public float GetBomberExplosionRadius()
        {
            float radius = Tier switch
            {
                2 => 2.5f,
                3 => 3f,
                _ => 2f
            };

            RunBuffs buffs = CardSystem.Instance != null ? CardSystem.Instance.runBuffs : null;
            if (buffs != null)
            {
                radius *= buffs.bomberRadiusMultiplier;
            }

            return radius;
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
            spriteRenderer.color = tint;
            RefreshVisualSize();
        }

        private void RefreshVisualSize()
        {
            if (spriteRenderer == null)
            {
                return;
            }

            transform.localScale = Vector3.one * currentSizeMultiplier * visualSizeBoost;
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
                case TroopType.Mage:
                    firstTarget = start + new Vector3(0f, 0.1f, 0f);
                    firstDuration = 0.06f;
                    secondDuration = 0.1f;
                    break;
                case TroopType.Healer:
                    firstTarget = start + (direction * 0.06f);
                    firstDuration = 0.05f;
                    secondDuration = 0.09f;
                    break;
                case TroopType.Bomber:
                    firstTarget = start + (direction * 0.2f);
                    firstDuration = 0.06f;
                    secondDuration = 0.04f;
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

        private float GetHpMultiplier()
        {
            return CardSystem.Instance != null ? CardSystem.Instance.runBuffs.hpMultiplier : 1f;
        }

        private void SetBomberState(bool exploded)
        {
            hasExplodedThisWave = exploded;

            if (spriteRenderer != null)
            {
                Color color = spriteRenderer.color;
                color.a = exploded ? 0.08f : 1f;
                spriteRenderer.color = color;
            }

            if (boxCollider != null)
            {
                boxCollider.enabled = !exploded;
            }
        }

        private IEnumerator FadeBomberOutRoutine()
        {
            SetDragging(false);

            float duration = 0.18f;
            float elapsed = 0f;
            Color startColor = spriteRenderer.color;
            hasExplodedThisWave = true;

            if (boxCollider != null)
            {
                boxCollider.enabled = false;
            }

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float alpha = Mathf.Lerp(startColor.a, 0.08f, t);
                spriteRenderer.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
                yield return null;
            }

            spriteRenderer.color = new Color(startColor.r, startColor.g, startColor.b, 0.08f);
            bomberFadeRoutine = null;
        }

        private IEnumerator PlayBomberExplosionRoutine(float radius)
        {
            GameObject flash = new("BomberFlash");
            flash.transform.position = transform.position;
            SpriteRenderer flashRenderer = CreateEffectSprite(flash.transform, GetRuntimeCircleSprite(), Color.white, 7);
            flash.transform.localScale = Vector3.one * 0.2f;

            GameObject explosion = new("BomberExplosion");
            explosion.transform.position = transform.position;
            SpriteRenderer explosionRenderer = CreateEffectSprite(explosion.transform, GetRuntimeCircleSprite(), baseColor, 6);
            explosion.transform.localScale = Vector3.zero;

            float elapsed = 0f;
            float duration = 0.2f;
            Vector3 fullScale = Vector3.one * radius * 2f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                flash.transform.localScale = Vector3.Lerp(Vector3.one * 0.2f, Vector3.one * radius * 1.2f, t);
                explosion.transform.localScale = Vector3.Lerp(Vector3.zero, fullScale, t);
                flashRenderer.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.95f, 0f, t));
                explosionRenderer.color = new Color(baseColor.r, baseColor.g, baseColor.b, Mathf.Lerp(0.85f, 0f, t));
                yield return null;
            }

            Destroy(flash);
            Destroy(explosion);
        }

        private SpriteRenderer CreateEffectSprite(Transform parent, Sprite sprite, Color color, int sortingOrder)
        {
            GameObject effectObject = new("Effect");
            effectObject.transform.SetParent(parent, false);
            SpriteRenderer renderer = effectObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingLayerName = "Effects";
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private static Sprite GetRuntimeCircleSprite()
        {
            if (runtimeCircleSprite != null)
            {
                return runtimeCircleSprite;
            }

            const int size = 64;
            Texture2D texture = new(size, size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size * size];
            Vector2 center = new((size - 1) * 0.5f, (size - 1) * 0.5f);
            float radius = size * 0.45f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    float alpha = distance <= radius ? 1f - Mathf.Clamp01((distance / radius) * 0.4f) : 0f;
                    pixels[(y * size) + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            runtimeCircleSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            runtimeCircleSprite.name = "TroopRuntimeCircle";
            return runtimeCircleSprite;
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
