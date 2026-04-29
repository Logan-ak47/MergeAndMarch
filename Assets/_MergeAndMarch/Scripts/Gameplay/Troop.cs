using System.Collections.Generic;
using MergeAndMarch.Data;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MergeAndMarch.Gameplay
{
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(BoxCollider2D))]
    public class Troop : MonoBehaviour
    {
        private static Sprite runtimeCircleSprite;
        private static Sprite runtimeSquareSprite;
        private static readonly Color HpBarGreen = new(0.2667f, 1f, 0.5333f, 1f);
        private static readonly Color HpBarYellow = new(1f, 0.8667f, 0.2667f, 1f);
        private static readonly Color HpBarRed = new(1f, 0.2667f, 0.2667f, 1f);
        private static readonly Color DamageNumberColor = new(1f, 0.36f, 0.28f, 1f);
        private static readonly Color DeathArrowColor = new(1f, 0.12f, 0.12f, 0.95f);

        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private BoxCollider2D boxCollider;
        [SerializeField] private SpriteRenderer tierGlowRenderer;
        [SerializeField] private SpriteRenderer mergeHighlightRenderer;
        [SerializeField] private GameObject hpBarRoot;
        [SerializeField] private Image hpBarFillImage;

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
        private bool isHighlighted;
        private bool isDimmed;
        private bool hasLastAttackerPosition;
        private bool deathIndicatorSpawned;
        private Vector3 mergeHighlightBaseScale = Vector3.one;
        private Vector3 baseScale = Vector3.one;
        private Vector3 lastAttackerPosition;
        private float tierPulseScale = 1f;

        private void Reset()
        {
            CacheComponents();
            ResolveReadabilityReferences();
            ConfigureCollider();
        }

        private void Awake()
        {
            CacheComponents();
            CaptureBaseSpriteSize();
            ResolveReadabilityReferences();
            ConfigureCollider();
            if (mergeHighlightRenderer != null)
            {
                mergeHighlightBaseScale = mergeHighlightRenderer.transform.localScale;
                mergeHighlightRenderer.gameObject.SetActive(false);
            }
        }

        private void LateUpdate()
        {
            UpdateTierPulse();
            UpdateHighlightPulse();
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
            hasLastAttackerPosition = false;
            deathIndicatorSpawned = false;

            name = $"{troopData.displayName}_{column}_{row}";

            Sprite tierSprite = troopData.GetSpriteForTier(Tier);
            if (tierSprite != null)
            {
                spriteRenderer.sprite = SpriteBackgroundCleaner.GetCleanedSprite(tierSprite);
            }

            CaptureBaseSpriteSize();
            spriteRenderer.color = baseColor;
            spriteRenderer.drawMode = SpriteDrawMode.Simple;
            defaultSortingOrder = spriteRenderer.sortingOrder;
            ResolveReadabilityReferences();
            ApplyTierVisuals(config);
            ConfigureCollider();
            currentHP = MaxHP;
            SetBomberState(false);
            UpdateHPBar();
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
            currentConfig = config;
            Tier = Mathf.Clamp(Tier + 1, 1, 3);
            visualSizeBoost = 1f;
            ApplyTierVisuals(config);
            ConfigureCollider();
            currentHP = MaxHP;
            UpdateHPBar();
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
            UpdateReadabilitySorting();
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

        public void SetMergeHighlight(bool on)
        {
            isHighlighted = on;
            if (mergeHighlightRenderer != null)
            {
                mergeHighlightRenderer.gameObject.SetActive(on);
                if (!on)
                {
                    mergeHighlightRenderer.transform.localScale = mergeHighlightBaseScale;
                }
            }
        }

        public void SetDimmed(bool on)
        {
            isDimmed = on;
            if (spriteRenderer == null)
            {
                return;
            }

            Color c = spriteRenderer.color;
            c.a = on ? 0.45f : 1f;
            spriteRenderer.color = c;
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
            UpdateHPBar();
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
            UpdateHPBar();
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

        public bool ApplyDamage(float damage, Enemy attacker = null)
        {
            if (!IsCombatActive)
            {
                return true;
            }

            if (attacker != null)
            {
                lastAttackerPosition = attacker.transform.position;
                hasLastAttackerPosition = true;
            }

            float appliedDamage = Mathf.Max(0f, damage);
            currentHP = Mathf.Max(0f, currentHP - appliedDamage);
            StartFlash(Color.white, 0.1f);
            UpdateHPBar();
            if (appliedDamage > 0.01f)
            {
                DamageNumber.Spawn(transform.position + (Vector3.up * 0.15f), appliedDamage, DamageNumberColor);
            }

            bool died = !IsAlive;
            if (died && !deathIndicatorSpawned)
            {
                deathIndicatorSpawned = true;
                SpawnDeathDirectionIndicator();
            }

            return died;
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

            Sprite tierSprite = Data.GetSpriteForTier(Tier);
            if (tierSprite != null)
            {
                spriteRenderer.sprite = SpriteBackgroundCleaner.GetCleanedSprite(tierSprite);
                CaptureBaseSpriteSize();
            }

            currentSizeMultiplier = ResolveTierVisualScale(config);
            spriteRenderer.color = Color.white;
            if (mergeHighlightRenderer != null)
            {
                mergeHighlightRenderer.sprite = spriteRenderer.sprite;
            }

            if (tierGlowRenderer != null)
            {
                tierGlowRenderer.sprite = spriteRenderer.sprite;
                tierGlowRenderer.enabled = Tier >= 2;
                tierGlowRenderer.color = Tier == 3
                    ? new Color(1f, 0.92f, 0.66f, 0.9f)
                    : new Color(1f, 1f, 1f, 0.6f);
                tierGlowRenderer.transform.localScale = Tier == 3 ? Vector3.one * 1.18f : Vector3.one * 1.1f;
                tierGlowRenderer.transform.localPosition = new Vector3(0f, 0f, 0.05f);
            }
            RefreshVisualSize();
        }

        private float ResolveTierVisualScale(GameConfig config)
        {
            float requestedScale = Tier switch
            {
                2 => config.tierTwoScale,
                3 => config.tierThreeScale,
                _ => config.troopBaseScale
            };

            Sprite tierOneSprite = Data.GetSpriteForTier(1);
            Sprite cleanedTierOneSprite = tierOneSprite != null ? SpriteBackgroundCleaner.GetCleanedSprite(tierOneSprite) : null;
            float referenceSize = cleanedTierOneSprite != null
                ? Mathf.Max(cleanedTierOneSprite.bounds.size.x, cleanedTierOneSprite.bounds.size.y)
                : Mathf.Max(baseSpriteSize.x, baseSpriteSize.y);
            float currentSize = Mathf.Max(0.01f, Mathf.Max(baseSpriteSize.x, baseSpriteSize.y));

            return requestedScale * Mathf.Max(0.01f, referenceSize) / currentSize;
        }

        private void RefreshVisualSize()
        {
            if (spriteRenderer == null)
            {
                return;
            }

            baseScale = Vector3.one * currentSizeMultiplier * visualSizeBoost;
            ApplyCurrentScale();
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

            if (tierGlowRenderer != null)
            {
                tierGlowRenderer.enabled = !exploded && Tier >= 2;
            }

            if (boxCollider != null)
            {
                boxCollider.enabled = !exploded;
            }

            UpdateHPBar();
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

        private void SpawnDeathDirectionIndicator()
        {
            if (!hasLastAttackerPosition)
            {
                return;
            }

            Vector3 direction = lastAttackerPosition - transform.position;
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = Vector3.up;
            }

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            GameObject arrow = new("DeathDirectionArrow");
            arrow.transform.position = transform.position + (Vector3.up * 0.12f);
            arrow.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            arrow.transform.localScale = Vector3.one;

            Sprite square = GetRuntimeSquareSprite();
            SpriteRenderer shaft = CreateEffectSprite(arrow.transform, square, DeathArrowColor, 220);
            shaft.name = "Shaft";
            shaft.transform.localPosition = new Vector3(0.18f, 0f, 0f);
            shaft.transform.localScale = new Vector3(0.35f, 0.055f, 1f);

            SpriteRenderer headTop = CreateEffectSprite(arrow.transform, square, DeathArrowColor, 221);
            headTop.name = "HeadTop";
            headTop.transform.localPosition = new Vector3(0.39f, 0.055f, 0f);
            headTop.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            headTop.transform.localScale = new Vector3(0.17f, 0.055f, 1f);

            SpriteRenderer headBottom = CreateEffectSprite(arrow.transform, square, DeathArrowColor, 221);
            headBottom.name = "HeadBottom";
            headBottom.transform.localPosition = new Vector3(0.39f, -0.055f, 0f);
            headBottom.transform.localRotation = Quaternion.Euler(0f, 0f, -45f);
            headBottom.transform.localScale = new Vector3(0.17f, 0.055f, 1f);

            arrow.AddComponent<CombatFeedbackFx>().PlayFade(1f);
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

        private static Sprite GetRuntimeSquareSprite()
        {
            if (runtimeSquareSprite != null)
            {
                return runtimeSquareSprite;
            }

            Texture2D texture = new(16, 16, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[16 * 16];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.white;
            }

            texture.SetPixels(pixels);
            texture.Apply();
            runtimeSquareSprite = Sprite.Create(texture, new Rect(0f, 0f, 16f, 16f), new Vector2(0.5f, 0.5f), 100f);
            runtimeSquareSprite.name = "TroopRuntimeSquare";
            return runtimeSquareSprite;
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
            boxCollider.offset = spriteRenderer != null && spriteRenderer.sprite != null
                ? (Vector2)spriteRenderer.sprite.bounds.center
                : Vector2.zero;

            float visualScale = Mathf.Max(0.01f, currentSizeMultiplier * visualSizeBoost);
            Vector2 visualLocalSize = baseSpriteSize * 0.85f;
            if (currentConfig != null)
            {
                float desiredWorldSize = currentConfig.cellSize * 0.78f;
                Vector2 minimumLocalSize = Vector2.one * (desiredWorldSize / visualScale);
                visualLocalSize = Vector2.Max(visualLocalSize, minimumLocalSize);
            }

            boxCollider.size = visualLocalSize;
        }

        private void ResolveReadabilityReferences()
        {
            if (mergeHighlightRenderer == null)
            {
                mergeHighlightRenderer = transform.Find("MergeHighlight")?.GetComponent<SpriteRenderer>();
            }

            if (tierGlowRenderer == null)
            {
                tierGlowRenderer = transform.Find("TierGlow")?.GetComponent<SpriteRenderer>();
            }

            if (tierGlowRenderer != null)
            {
                tierGlowRenderer.sortingLayerName = spriteRenderer != null ? spriteRenderer.sortingLayerName : "Troops";
                tierGlowRenderer.enabled = false;
            }

            if (hpBarRoot == null)
            {
                hpBarRoot = transform.Find("HPBarRoot")?.gameObject;
            }

            if (hpBarFillImage == null && hpBarRoot != null)
            {
                hpBarFillImage = hpBarRoot.transform.Find("Fill")?.GetComponent<Image>();
            }

            UpdateReadabilitySorting();
        }

        private void UpdateHPBar()
        {
            if (hpBarRoot == null || hpBarFillImage == null)
            {
                ResolveReadabilityReferences();
            }

            if (hpBarRoot == null || hpBarFillImage == null)
            {
                return;
            }

            if (MaxHP <= 0.01f)
            {
                hpBarRoot.SetActive(false);
                hpBarFillImage.fillAmount = 0f;
                return;
            }

            float ratio = Mathf.Clamp01(currentHP / MaxHP);
            hpBarFillImage.fillAmount = ratio;
            hpBarFillImage.color = ratio > 0.5f ? HpBarGreen : ratio > 0.25f ? HpBarYellow : HpBarRed;

            bool shouldShow = !hasExplodedThisWave && currentHP > 0.01f && ratio < 0.999f;
            hpBarRoot.SetActive(shouldShow);
        }

        private void UpdateHighlightPulse()
        {
            if (!isHighlighted || mergeHighlightRenderer == null)
            {
                return;
            }

            float pulse = 1f + (Mathf.Sin(Time.unscaledTime * 4f) * 0.08f);
            mergeHighlightRenderer.transform.localScale = mergeHighlightBaseScale * pulse;
        }

        private void UpdateTierPulse()
        {
            float targetPulse = Tier == 3 ? 1f + (Mathf.Sin(Time.unscaledTime * 2f) * 0.05f) : 1f;
            if (Mathf.Abs(targetPulse - tierPulseScale) < 0.0001f)
            {
                return;
            }

            tierPulseScale = targetPulse;
            ApplyCurrentScale();
        }

        private void ApplyCurrentScale()
        {
            transform.localScale = baseScale * tierPulseScale;
        }

        private void UpdateReadabilitySorting()
        {
            if (spriteRenderer == null)
            {
                return;
            }

            if (mergeHighlightRenderer != null)
            {
                mergeHighlightRenderer.sortingLayerName = spriteRenderer.sortingLayerName;
                mergeHighlightRenderer.sortingOrder = spriteRenderer.sortingOrder - 2;
            }

            if (tierGlowRenderer != null)
            {
                tierGlowRenderer.sortingLayerName = spriteRenderer.sortingLayerName;
                tierGlowRenderer.sortingOrder = spriteRenderer.sortingOrder - 1;
            }

            if (hpBarRoot != null)
            {
                Canvas canvas = hpBarRoot.GetComponent<Canvas>();
                if (canvas != null)
                {
                    canvas.sortingLayerName = spriteRenderer.sortingLayerName;
                    canvas.sortingOrder = spriteRenderer.sortingOrder + 2;
                }
            }
        }

    }
}
