using System.Collections;
using System.Collections.Generic;
using MergeAndMarch.Core;
using MergeAndMarch.Data;
using UnityEngine;

namespace MergeAndMarch.Gameplay
{
    /// <summary>
    /// Pooled flat-shape effects, screen shake, hit-stop and floating labels for the juice
    /// language (design/art/art-bible.md Section 2; pooled sprites per Section 8.7).
    /// Loads <see cref="JuiceConfig"/> and <see cref="BeanArtSet"/> from Resources and
    /// creates itself on first use.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class JuiceFx : MonoBehaviour
    {
        private const string ConfigResourcePath = "MergeAndMarch/JuiceConfig";
        private const string ArtResourcePath = "MergeAndMarch/BeanArtSet";
        private const string EffectsLayer = "Effects";
        private const string BehindUnitsLayer = "Grid";

        private const string DimLayer = "Troops";
        private const int DimOrder = 10;

        private static JuiceFx instance;
        private static bool isQuitting;
        private static Sprite whiteSprite;

        [SerializeField] private JuiceConfig config;
        [SerializeField] private BeanArtSet art;

        private readonly List<Effect> active = new();
        private readonly Stack<Effect> pool = new();
        private float shakeStrength;
        private float shakeDuration;
        private float shakeElapsed;
        private Vector3 lastShakeOffset;
        private Vector3 lastCameraPosition;
        private bool hasLastCameraPosition;
        private SpriteRenderer slowMoDim;
        private SpriteRenderer slowMoVignette;
        private SpriteRenderer slowMoIcon;
        private bool slowMoActive;
        private float slowMoBlend;
        private Color[] bomberPalette;

        private sealed class Effect
        {
            public SpriteRenderer Renderer;
            public Vector3 Velocity;
            public float Gravity;
            public float AngularVelocity;
            public float Age;
            public float Lifetime;
            public float StartScale;
            public float EndScale;
            public Color Color;
            public float EndAlpha;
            public float FadeStart;
            public bool Unscaled;
        }

        /// <summary>The shared instance, created on first use while playing.</summary>
        public static JuiceFx Instance
        {
            get
            {
                if (instance == null && Application.isPlaying && !isQuitting)
                {
                    instance = FindFirstObjectByType<JuiceFx>();
                    if (instance == null)
                    {
                        instance = new GameObject("JuiceFx").AddComponent<JuiceFx>();
                    }
                }

                return instance;
            }
        }

        public JuiceConfig Config => config;
        public BeanArtSet Art => art;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            isQuitting = false;
            Application.quitting -= MarkQuitting;
            Application.quitting += MarkQuitting;
        }

        private static void MarkQuitting()
        {
            isQuitting = true;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            if (config == null)
            {
                config = Resources.Load<JuiceConfig>(ConfigResourcePath);
            }

            if (config == null)
            {
                config = ScriptableObject.CreateInstance<JuiceConfig>();
            }

            if (art == null)
            {
                art = Resources.Load<BeanArtSet>(ArtResourcePath);
            }
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        private void Update()
        {
            UpdateSlowMoOverlay();
            for (int i = active.Count - 1; i >= 0; i--)
            {
                Effect effect = active[i];
                float dt = effect.Unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
                effect.Age += dt;
                float t = effect.Age / effect.Lifetime;
                if (t >= 1f || effect.Renderer == null)
                {
                    Release(i);
                    continue;
                }

                Transform effectTransform = effect.Renderer.transform;
                effect.Velocity.y += effect.Gravity * dt;
                effectTransform.position += effect.Velocity * dt;
                effectTransform.Rotate(0f, 0f, effect.AngularVelocity * dt);

                float eased = 1f - ((1f - t) * (1f - t));
                float scale = Mathf.LerpUnclamped(effect.StartScale, effect.EndScale, eased);
                effectTransform.localScale = new Vector3(scale, scale, 1f);

                float fadeT = effect.FadeStart >= 1f ? 0f : Mathf.Clamp01((t - effect.FadeStart) / (1f - effect.FadeStart));
                Color color = effect.Color;
                color.a *= Mathf.Lerp(1f, effect.EndAlpha, fadeT);
                effect.Renderer.color = color;
            }
        }

        private void LateUpdate()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return;
            }

            // GridCameraFramer rewrites the camera position every LateUpdate; this runs after it
            // (execution order 1000), so the shake is an offset on top of the framed position.
            Transform cameraTransform = mainCamera.transform;
            Vector3 basePosition = hasLastCameraPosition && cameraTransform.position == lastCameraPosition
                ? lastCameraPosition - lastShakeOffset
                : cameraTransform.position;

            Vector3 offset = Vector3.zero;
            if (shakeElapsed < shakeDuration)
            {
                shakeElapsed += Time.unscaledDeltaTime;
                float falloff = 1f - Mathf.Clamp01(shakeElapsed / Mathf.Max(0.0001f, shakeDuration));
                Vector2 random = Random.insideUnitCircle * (shakeStrength * falloff);
                offset = new Vector3(random.x, random.y, 0f);
            }

            cameraTransform.position = basePosition + offset;
            lastShakeOffset = offset;
            lastCameraPosition = cameraTransform.position;
            hasLastCameraPosition = true;
        }

        /// <summary>Spawns one pooled sprite that scales, moves and fades over its lifetime.</summary>
        public void Spawn(
            Sprite sprite,
            Vector3 position,
            Color color,
            float startSize,
            float endSize,
            float lifetime,
            float endAlpha = 0f,
            Vector3 velocity = default,
            float gravity = 0f,
            float angularVelocity = 0f,
            bool unscaled = false,
            string sortingLayer = EffectsLayer,
            int sortingOrder = 10,
            float fadeStart = 0f,
            float rotation = 0f)
        {
            if (sprite == null || lifetime <= 0f)
            {
                return;
            }

            Effect effect = Acquire();
            float spriteSize = Mathf.Max(0.0001f, Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y));
            effect.Renderer.sprite = sprite;
            effect.Renderer.sortingLayerName = sortingLayer;
            effect.Renderer.sortingOrder = sortingOrder;
            effect.Renderer.color = color;
            effect.Renderer.gameObject.SetActive(true);

            Transform effectTransform = effect.Renderer.transform;
            effectTransform.position = position;
            effectTransform.rotation = Quaternion.Euler(0f, 0f, rotation);
            effect.StartScale = startSize / spriteSize;
            effect.EndScale = endSize / spriteSize;
            effectTransform.localScale = new Vector3(effect.StartScale, effect.StartScale, 1f);

            effect.Velocity = velocity;
            effect.Gravity = gravity;
            effect.AngularVelocity = angularVelocity;
            effect.Age = 0f;
            effect.Lifetime = lifetime;
            effect.Color = color;
            effect.EndAlpha = endAlpha;
            effect.FadeStart = fadeStart;
            effect.Unscaled = unscaled;
            active.Add(effect);
        }

        /// <summary>Spawns a radial burst of pooled sprites.</summary>
        public void Burst(
            Sprite sprite,
            Vector3 position,
            Color[] palette,
            Color fallbackColor,
            int count,
            float speed,
            float lifetime,
            float size,
            float gravity,
            bool unscaled,
            bool spin,
            int sortingOrder = 12)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                Vector3 direction = new(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                Color color = palette != null && palette.Length > 0 ? palette[Random.Range(0, palette.Length)] : fallbackColor;
                Spawn(
                    sprite,
                    position,
                    color,
                    size,
                    size * 0.6f,
                    lifetime * Random.Range(0.8f, 1.2f),
                    0f,
                    direction * (speed * Random.Range(0.55f, 1f)),
                    gravity,
                    spin ? Random.Range(-540f, 540f) : 0f,
                    unscaled,
                    EffectsLayer,
                    sortingOrder,
                    0.5f,
                    Random.Range(0f, 360f));
            }
        }

        /// <summary>Screen shake in 1080-reference px; ignored with Reduce Motion or shake off.</summary>
        public void Shake(float px, float duration)
        {
            if (config == null || config.reduceMotion || !config.screenShakeEnabled || px <= 0f || duration <= 0f)
            {
                return;
            }

            float strength = config.PxToWorld(px);
            bool shaking = shakeElapsed < shakeDuration;
            if (!shaking || strength >= shakeStrength)
            {
                shakeStrength = strength;
                shakeDuration = duration;
                shakeElapsed = 0f;
            }
        }

        /// <summary>Freezes gameplay time for an impact.</summary>
        public void HitStop(float duration)
        {
            TimeScaleManager.Instance?.HitStop(duration);
        }

        /// <summary>Small sparks where a hit lands (art bible: hit on enemy, troop hit).</summary>
        public void HitSparks(Vector3 position, Color color, int count)
        {
            if (art == null)
            {
                return;
            }

            Burst(art.fxSparkle, position, null, color, count, config.hitSparkSpeed, config.hitSparkLifetime, config.hitSparkSize, 0f, false, true, 14);
        }

        /// <summary>Enemy death: chunks, a white puff ring and a coin popping out.</summary>
        public void EnemyDeathBurst(Vector3 position, Color color, bool heavy)
        {
            if (art == null)
            {
                return;
            }

            Burst(art.fxChunk, position, null, color, config.Count(config.enemyDeathChunksMin, config.enemyDeathChunksMax), config.enemyDeathChunkSpeed, config.enemyDeathChunkLifetime, 0.13f, -9f, false, true);
            Spawn(art.fxRing, position, Color.white, 0.3f, 1.1f, 0.22f);
            Spawn(art.fxPuff, position, new Color(1f, 1f, 1f, 0.85f), 0.3f, 0.9f, 0.25f);
            Spawn(art.fxCoin, position, Color.white, 0.24f, 0.24f, 0.45f, 0f, new Vector3(0f, 2.6f, 0f), -7f, 0f, false, EffectsLayer, 16, 0.6f);
            if (heavy)
            {
                Shake(config.tankDeathShakePx, config.tankDeathShakeDuration);
            }
        }

        /// <summary>Troop death: a grey poof.</summary>
        public void TroopDeathPoof(Vector3 position)
        {
            if (art == null)
            {
                return;
            }

            Color grey = new(0.78f, 0.78f, 0.84f, 0.9f);
            Spawn(art.fxPuff, position, grey, 0.35f, 1.05f, 0.3f);
            Burst(art.fxCircle, position, null, grey, config.Count(4, 6), 1.8f, 0.3f, 0.1f, 0f, false, false);
            Shake(config.troopDeathShakePx, config.troopDeathShakeDuration);
        }

        /// <summary>Deploy landing: a dust puff at the feet.</summary>
        public void DeployDust(Vector3 position)
        {
            if (art == null)
            {
                return;
            }

            Spawn(art.fxPuff, position, new Color(1f, 1f, 1f, 0.7f), 0.25f, 0.75f, 0.25f, 0f, default, 0f, 0f, false, EffectsLayer, 4);
        }

        /// <summary>A sprite (e.g. a tier badge) that pops off and falls away.</summary>
        public void PopOff(Sprite sprite, Vector3 position, float size)
        {
            Spawn(sprite, position, Color.white, size, size, 0.45f, 0f, new Vector3(Random.Range(-0.8f, 0.8f), 2.4f, 0f), -9f, Random.Range(-360f, 360f), false, EffectsLayer, 18, 0.4f);
        }

        /// <summary>
        /// The merge resolve and tier-3 creation recipes: flash, ring, confetti, rays at tier 3,
        /// hit-stop, shake and the "TIER N!" label after the flash peak.
        /// </summary>
        public void MergeBurst(Vector3 position, int tier, Color troopColor)
        {
            if (art == null)
            {
                return;
            }

            bool tierThree = tier >= 3;
            Color flashColor = tierThree ? config.gold : Color.white;
            flashColor.a = config.FlashAlpha(0.9f);

            Spawn(art.fxCircle, position, flashColor, 0.6f, 1.7f, 0.14f, 0f, default, 0f, 0f, true, EffectsLayer, 30);
            Spawn(art.fxRing, position, Color.white, 0.4f, 1.9f, 0.28f, 0f, default, 0f, 0f, true, EffectsLayer, 29);
            Burst(art.fxConfetti, position, config.confettiColors, troopColor, config.Count(config.mergeConfettiMin, config.mergeConfettiMax), config.mergeConfettiSpeed, config.mergeConfettiLifetime, 0.15f, -7f, true, true, 28);

            if (tierThree)
            {
                Color rays = config.gold;
                rays.a = config.FlashAlpha(0.8f);
                if (!config.reduceMotion)
                {
                    Spawn(art.fxRays, position, rays, 1.3f, 2.4f, config.tierThreeRaysDuration, 0f, default, 0f, 90f, true, BehindUnitsLayer, 50, 0.4f);
                }

                Burst(art.fxStar, position, null, config.gold, config.Count(6, 8), config.mergeConfettiSpeed * 0.8f, config.mergeConfettiLifetime, 0.2f, -5f, true, true, 31);
                HitStop(config.tierThreeHitStop);
                Shake(config.tierThreeShakePx, config.tierThreeShakeDuration);
            }
            else
            {
                HitStop(config.mergeHitStop);
                Shake(config.mergeShakePx, config.mergeShakeDuration);
            }

            string label = tierThree ? config.mergeTierThreeLabel : config.mergeTierTwoLabel;
            StartCoroutine(SpawnLabelAfter(0.12f, position + (Vector3.up * 0.75f), label, tierThree ? config.gold : Color.white));
        }

        /// <summary>
        /// Drag slow-mo overlay (art bible Sections 2 and 7): an ink dim and vignette between the
        /// troops and the focus layer, so the dragged troop and its merge partners stay bright,
        /// plus an hourglass icon under the HUD row.
        /// </summary>
        public void SetSlowMo(bool isActive)
        {
            slowMoActive = isActive;
            if (isActive)
            {
                EnsureSlowMoOverlay();
            }
        }

        /// <summary>Knight melee: a crescent slash on the target, angled away from the attacker.</summary>
        public void KnightSlash(Vector3 targetPosition, Vector3 attackerPosition, Color color)
        {
            if (art == null)
            {
                return;
            }

            Vector3 direction = targetPosition - attackerPosition;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            Color tint = Color.Lerp(Color.white, color, 0.3f);
            Spawn(art.fxSlash, targetPosition, tint, config.knightSlashStartSize, config.knightSlashEndSize, config.knightSlashDuration, 0f, default, 0f, 0f, false, EffectsLayer, 20, 0.3f, angle);
        }

        /// <summary>Archer release: a white pop and two sparks at the bow.</summary>
        public void MuzzlePop(Vector3 position, Color color)
        {
            if (art == null)
            {
                return;
            }

            Spawn(art.fxCircle, position, Color.white, 0.1f, config.muzzlePopSize, 0.08f, 0f, default, 0f, 0f, false, EffectsLayer, 18);
            Burst(art.fxSparkle, position, null, color, 2, config.hitSparkSpeed * 0.6f, 0.18f, config.hitSparkSize * 0.8f, 0f, false, true, 18);
        }

        /// <summary>One puff of a projectile trail; call once per frame of flight.</summary>
        public void ProjectileTrail(Vector3 position, Color color)
        {
            if (art == null)
            {
                return;
            }

            color.a = 0.7f;
            Spawn(art.fxCircle, position, color, 0.14f, 0.03f, 0.15f, 0f, default, 0f, 0f, false, EffectsLayer, 4);
        }

        /// <summary>Healer pulse: a mint ring from the healer and sparkles rising from the healed troop.</summary>
        public void HealRing(Vector3 healerPosition, Vector3 targetPosition)
        {
            if (art == null)
            {
                return;
            }

            Spawn(art.fxRing, healerPosition, config.healMint, 0.15f, config.healRingSize, config.healRingDuration, 0f, default, 0f, 0f, false, EffectsLayer, 6);
            int count = config.Count(4, 6);
            for (int i = 0; i < count; i++)
            {
                Vector3 velocity = new(Random.Range(-0.4f, 0.4f), Random.Range(1f, 1.6f), 0f);
                Vector3 offset = new(Random.Range(-0.25f, 0.25f), Random.Range(0f, 0.3f), 0f);
                Spawn(art.fxSparkle, targetPosition + offset, config.healMint, 0.16f, 0.06f, 0.45f, 0f, velocity, 0f, Random.Range(-180f, 180f), false, EffectsLayer, 17, 0.5f);
            }
        }

        /// <summary>Bomber explosion: orange blast, hot core, ring, puff, chunks and shake.</summary>
        public void BomberBlast(Vector3 position, float radius)
        {
            if (art == null)
            {
                return;
            }

            bomberPalette ??= new[] { config.bomberBlastColor, config.bomberBlastCore, Color.white };
            Color blast = config.bomberBlastColor;
            blast.a = config.FlashAlpha(0.85f);
            Color core = config.bomberBlastCore;
            core.a = config.FlashAlpha(0.95f);
            Spawn(art.fxCircle, position, blast, 0.2f, radius * 2f, 0.3f, 0f, default, 0f, 0f, false, EffectsLayer, 24);
            Spawn(art.fxCircle, position, core, 0.1f, radius * 1.1f, 0.22f, 0f, default, 0f, 0f, false, EffectsLayer, 25);
            Spawn(art.fxRing, position, config.bomberBlastCore, 0.3f, radius * 2.2f, 0.3f, 0f, default, 0f, 0f, false, EffectsLayer, 26);
            Spawn(art.fxPuff, position, new Color(1f, 1f, 1f, 0.7f), 0.4f, radius * 1.2f, 0.35f, 0f, default, 0f, 0f, false, EffectsLayer, 23);
            Burst(art.fxChunk, position, bomberPalette, config.bomberBlastColor, config.Count(8, 12), 4f, 0.4f, 0.14f, -8f, false, true, 27);
            Shake(config.bomberShakePx, config.bomberShakeDuration);
        }

        /// <summary>Boss entrance: the biggest shake in the game, a dust ring and a "BOSS!" banner.</summary>
        public void BossEntrance(Vector3 position)
        {
            if (art == null)
            {
                return;
            }

            Shake(config.bossEntranceShakePx, config.bossEntranceShakeDuration);
            Spawn(art.fxRing, position, Color.white, 0.5f, 3f, 0.35f);
            Spawn(art.fxPuff, position, new Color(1f, 1f, 1f, 0.8f), 0.6f, 2.2f, 0.4f);
            StartCoroutine(SpawnLabelAfter(0.1f, position + (Vector3.up * 1.4f), config.bossLabel, config.enemyRed));
        }

        /// <summary>An enemy hit the base: red burst, shake and a red "-N" number.</summary>
        public void BaseHit(Vector3 position, float damage)
        {
            if (art != null)
            {
                Spawn(art.fxRing, position, config.enemyRed, 0.3f, 1.3f, 0.25f);
                Burst(art.fxChunk, position, null, config.enemyRed, config.Count(5, 7), 3f, 0.35f, 0.13f, -6f, false, true);
            }

            Shake(config.baseHitShakePx, config.baseHitShakeDuration);
            DamageNumber.SpawnLabel(position + (Vector3.up * 0.4f), "-" + Mathf.RoundToInt(damage), config.enemyRed, 1.4f);
        }

        /// <summary>Run lost: a heavy shake on the defeat moment.</summary>
        public void DefeatShake()
        {
            Shake(config.defeatShakePx, config.defeatShakeDuration);
        }

        private void EnsureSlowMoOverlay()
        {
            if (slowMoDim != null)
            {
                return;
            }

            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return;
            }

            GameObject root = new("SlowMoOverlay");
            root.transform.SetParent(mainCamera.transform, false);
            root.transform.localPosition = new Vector3(0f, 0f, 10f);
            slowMoDim = CreateOverlayLayer(root.transform, "Dim", GetWhiteSprite(), DimLayer, DimOrder);
            slowMoVignette = CreateOverlayLayer(root.transform, "Vignette", art != null ? art.fxVignette : null, DimLayer, DimOrder + 1);
            slowMoIcon = CreateOverlayLayer(root.transform, "Hourglass", art != null ? art.iconHourglass : null, EffectsLayer, 40);
        }

        private static SpriteRenderer CreateOverlayLayer(Transform parent, string layerName, Sprite sprite, string sortingLayer, int order)
        {
            GameObject layer = new(layerName);
            layer.transform.SetParent(parent, false);
            SpriteRenderer renderer = layer.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingLayerName = sortingLayer;
            renderer.sortingOrder = order;
            renderer.enabled = false;
            return renderer;
        }

        private void UpdateSlowMoOverlay()
        {
            if (slowMoDim == null)
            {
                return;
            }

            float fade = slowMoActive ? config.slowMoFadeIn : config.slowMoFadeOut;
            slowMoBlend = Mathf.MoveTowards(slowMoBlend, slowMoActive ? 1f : 0f, Time.unscaledDeltaTime / Mathf.Max(0.01f, fade));
            bool visible = slowMoBlend > 0.001f;
            slowMoDim.enabled = visible;
            slowMoVignette.enabled = visible && slowMoVignette.sprite != null;
            slowMoIcon.enabled = visible && slowMoIcon.sprite != null;
            Camera mainCamera = Camera.main;
            if (!visible || mainCamera == null)
            {
                return;
            }

            float height = mainCamera.orthographicSize * 2f;
            float width = height * mainCamera.aspect;
            SizeTo(slowMoDim, width * 1.2f, height * 1.2f);
            SizeTo(slowMoVignette, width * 1.05f, height * 1.05f);
            slowMoDim.color = new Color(config.ink.r, config.ink.g, config.ink.b, config.slowMoDimAlpha * slowMoBlend);
            slowMoVignette.color = new Color(config.ink.r, config.ink.g, config.ink.b, config.slowMoVignetteAlpha * slowMoBlend);

            if (slowMoIcon.enabled)
            {
                float iconSize = config.PxToWorld(config.slowMoIconPx) * (1f + (Mathf.Sin(Time.unscaledTime * 6f) * 0.06f));
                SizeTo(slowMoIcon, iconSize, iconSize);
                slowMoIcon.transform.localPosition = new Vector3(0f, (height * 0.5f) - config.PxToWorld(config.slowMoIconTopPx), 0f);
                slowMoIcon.color = new Color(1f, 1f, 1f, slowMoBlend);
            }
        }

        private static void SizeTo(SpriteRenderer renderer, float width, float height)
        {
            if (renderer.sprite == null)
            {
                return;
            }

            Vector3 size = renderer.sprite.bounds.size;
            renderer.transform.localScale = new Vector3(width / Mathf.Max(0.0001f, size.x), height / Mathf.Max(0.0001f, size.y), 1f);
        }

        private static Sprite GetWhiteSprite()
        {
            if (whiteSprite == null)
            {
                Texture2D texture = Texture2D.whiteTexture;
                whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), texture.width);
                whiteSprite.name = "JuiceWhite";
            }

            return whiteSprite;
        }

        private IEnumerator SpawnLabelAfter(float delay, Vector3 position, string label, Color color)
        {
            float elapsed = 0f;
            while (elapsed < delay)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            DamageNumber.SpawnLabel(position, label, color, 2f);
        }

        private Effect Acquire()
        {
            if (active.Count >= Mathf.Max(1, config.maxLiveEffects))
            {
                Effect oldest = active[0];
                active.RemoveAt(0);
                return oldest;
            }

            if (pool.Count > 0)
            {
                return pool.Pop();
            }

            GameObject effectObject = new("Fx");
            effectObject.transform.SetParent(transform, false);
            return new Effect { Renderer = effectObject.AddComponent<SpriteRenderer>() };
        }

        private void Release(int index)
        {
            Effect effect = active[index];
            active.RemoveAt(index);
            if (effect.Renderer != null)
            {
                effect.Renderer.gameObject.SetActive(false);
                pool.Push(effect);
            }
        }
    }
}
