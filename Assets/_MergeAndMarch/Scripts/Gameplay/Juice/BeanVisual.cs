using System.Collections;
using MergeAndMarch.Data;
using UnityEngine;

namespace MergeAndMarch.Gameplay
{
    /// <summary>How a unit's prop acts during an attack (art bible Section 5, "the prop does the acting").</summary>
    public enum BeanPropAction
    {
        Jab,
        Squeeze,
        Raise,
        Pulse
    }

    /// <summary>
    /// Layered "bean" renderer for a troop or enemy: shadow, glow/rim, wings, body, tier-3 accent,
    /// face, prop, white flash overlay and tier badge. Plays the idle, attack, hit, drag, deploy and
    /// death motion of the juice language (design/art/art-bible.md Sections 2 and 5) as code-driven
    /// transforms on single sprites.
    /// </summary>
    /// <example>
    /// <code>
    /// BeanVisual bean = BeanVisual.Create(transform, data.beanBody, data.beanProp, data.beanAccent,
    ///     data.beanFlash, null, false, BeanPropAction.Jab, fx.Config, fx.Art, "Troops", 0);
    /// bean.SetTier(2);
    /// bean.PlayAttack(enemy.transform.position);
    /// </code>
    /// </example>
    public class BeanVisual : MonoBehaviour
    {
        private const int ShadowOrder = -4;
        private const int GlowOrder = -3;
        private const int WingsOrder = -2;
        private const int BodyOrder = 0;
        private const int AccentOrder = 1;
        private const int FaceOrder = 2;
        private const int PropOrder = 3;
        private const int FlashOrder = 4;
        private const int BadgeOrder = 6;
        private const float BadgeWorldPx = 52f;
        private const float StandardFaceCanvas = 256f;

        private JuiceConfig config;
        private BeanArtSet art;
        private Transform unitRoot;
        private bool isEnemy;
        private BeanPropAction propAction;

        private SpriteRenderer shadowRenderer;
        private SpriteRenderer glowRenderer;
        private SpriteRenderer wingsRenderer;
        private SpriteRenderer bodyRenderer;
        private SpriteRenderer accentRenderer;
        private SpriteRenderer faceRenderer;
        private SpriteRenderer propRenderer;
        private SpriteRenderer flashRenderer;
        private SpriteRenderer badgeRenderer;

        private Vector3 restPosition;
        private float bodyHeight = 1f;
        private float bodyCenterY;
        private float shadowBaseScale = 1f;

        private Vector2 squash = Vector2.one;
        private float popScale = 1f;
        private Vector3 recoilOffset;
        private Vector3 propOffset;
        private float propScale = 1f;
        private float propAngle;
        private float idlePhase;
        private float marchPhase;
        private float marchRate = 1f;
        private bool marching;
        private bool dragging;
        private float dragBlend;
        private bool highlighted;
        private int tier = 1;
        private float alpha = 1f;
        private Color tint = Color.white;
        private float flashAlpha;
        private Color flashColor = Color.white;
        private bool frozen;

        private BeanFace baseFace = BeanFace.Idle;
        private BeanFace heldFace = BeanFace.Idle;
        private float heldFaceUntil = -1f;
        private float nextBlinkTime;
        private float blinkUntil = -1f;

        private Coroutine squashRoutine;
        private Coroutine flashRoutine;
        private Coroutine tintRoutine;
        private Coroutine propRoutine;
        private Coroutine recoilRoutine;

        public JuiceConfig Config => config;
        public BeanArtSet Art => art;

        /// <summary>Body height in the unit's local space (before tier scale).</summary>
        public float BodyHeight => bodyHeight;

        /// <summary>World position of the feet (the shadow's ground point).</summary>
        public Vector3 FeetPosition => unitRoot != null ? unitRoot.TransformPoint(restPosition) : transform.position;

        /// <summary>World position of the body's visual centre.</summary>
        public Vector3 CenterPosition => transform.TransformPoint(new Vector3(0f, bodyCenterY, 0f));

        /// <summary>Creates the bean layer stack under a unit root.</summary>
        public static BeanVisual Create(
            Transform unitRoot,
            Sprite body,
            Sprite prop,
            Sprite accent,
            Sprite flash,
            Sprite wings,
            bool enemy,
            BeanPropAction action,
            JuiceConfig juiceConfig,
            BeanArtSet artSet,
            string sortingLayer,
            int baseOrder)
        {
            GameObject beanObject = new("Bean");
            beanObject.transform.SetParent(unitRoot, false);
            BeanVisual bean = beanObject.AddComponent<BeanVisual>();
            bean.Setup(unitRoot, body, prop, accent, flash, wings, enemy, action, juiceConfig, artSet);
            bean.SetSorting(sortingLayer, baseOrder);
            return bean;
        }

        private void Setup(
            Transform root,
            Sprite body,
            Sprite prop,
            Sprite accent,
            Sprite flash,
            Sprite wings,
            bool enemy,
            BeanPropAction action,
            JuiceConfig juiceConfig,
            BeanArtSet artSet)
        {
            unitRoot = root;
            isEnemy = enemy;
            propAction = action;
            config = juiceConfig != null ? juiceConfig : ScriptableObject.CreateInstance<JuiceConfig>();
            art = artSet;

            bodyHeight = body != null ? body.bounds.size.y : 1f;
            bodyCenterY = body != null ? body.bounds.center.y : 0.5f;
            restPosition = new Vector3(0f, -bodyCenterY, 0f);
            transform.localPosition = restPosition;

            shadowRenderer = CreateLayer("Shadow", root, art != null ? art.fxShadow : null);
            shadowRenderer.transform.localPosition = restPosition;
            if (shadowRenderer.sprite != null && body != null)
            {
                shadowBaseScale = (body.bounds.size.x * 0.72f) / Mathf.Max(0.01f, shadowRenderer.sprite.bounds.size.x);
            }

            glowRenderer = CreateLayer("Glow", transform, flash);
            glowRenderer.enabled = false;
            wingsRenderer = CreateLayer("Wings", transform, wings);
            wingsRenderer.enabled = wings != null;
            bodyRenderer = CreateLayer("Body", transform, body);
            accentRenderer = CreateLayer("Accent", transform, accent);
            accentRenderer.enabled = false;
            faceRenderer = CreateLayer("Face", transform, art != null ? art.GetFace(BeanFace.Idle, enemy) : null);
            float faceScale = body != null ? body.rect.height / StandardFaceCanvas : 1f;
            faceRenderer.transform.localScale = Vector3.one * faceScale;
            propRenderer = CreateLayer("Prop", transform, prop);
            propRenderer.enabled = prop != null;
            flashRenderer = CreateLayer("Flash", transform, flash);
            flashRenderer.enabled = false;

            badgeRenderer = CreateLayer("Badge", root, null);
            badgeRenderer.enabled = false;

            idlePhase = Random.Range(0f, Mathf.PI * 2f);
            marchPhase = Random.Range(0f, 1f);
            nextBlinkTime = Time.time + Random.Range(config.blinkIntervalMin, config.blinkIntervalMax);
            ApplyColors();
        }

        private static SpriteRenderer CreateLayer(string layerName, Transform parent, Sprite sprite)
        {
            GameObject layer = new(layerName);
            layer.transform.SetParent(parent, false);
            SpriteRenderer renderer = layer.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            return renderer;
        }

        /// <summary>Sets the sorting layer and the base order the layer stack is offset from.</summary>
        public void SetSorting(string sortingLayer, int baseOrder)
        {
            ApplySorting(shadowRenderer, sortingLayer, baseOrder + ShadowOrder);
            ApplySorting(glowRenderer, sortingLayer, baseOrder + GlowOrder);
            ApplySorting(wingsRenderer, sortingLayer, baseOrder + WingsOrder);
            ApplySorting(bodyRenderer, sortingLayer, baseOrder + BodyOrder);
            ApplySorting(accentRenderer, sortingLayer, baseOrder + AccentOrder);
            ApplySorting(faceRenderer, sortingLayer, baseOrder + FaceOrder);
            ApplySorting(propRenderer, sortingLayer, baseOrder + PropOrder);
            ApplySorting(flashRenderer, sortingLayer, baseOrder + FlashOrder);
            ApplySorting(badgeRenderer, sortingLayer, baseOrder + BadgeOrder);
        }

        private static void ApplySorting(SpriteRenderer renderer, string sortingLayer, int order)
        {
            if (renderer != null)
            {
                renderer.sortingLayerName = sortingLayer;
                renderer.sortingOrder = order;
            }
        }

        /// <summary>Tier visuals: badge, gold accent and glow at tier 3 (art bible Section 5).</summary>
        public void SetTier(int newTier)
        {
            tier = Mathf.Clamp(newTier, 1, 3);
            Sprite badge = art != null ? art.GetBadge(tier) : null;
            badgeRenderer.sprite = badge;
            badgeRenderer.enabled = badge != null && !isEnemy && !frozen;
            accentRenderer.enabled = tier >= 3 && accentRenderer.sprite != null;
            ApplyColors();
        }

        public void SetMarching(bool isMarching)
        {
            marching = isMarching;
        }

        public void SetMarchRate(float rate)
        {
            marchRate = Mathf.Max(0.1f, rate);
        }

        public void SetDragging(bool isDragging)
        {
            dragging = isDragging;
        }

        /// <summary>White rim pulse on valid merge partners (art bible Section 2, merge pickup/drag).</summary>
        public void SetHighlighted(bool on)
        {
            highlighted = on;
        }

        /// <summary>Overall opacity (dimmed troops while dragging, spent bombers).</summary>
        public void SetAlpha(float value)
        {
            alpha = Mathf.Clamp01(value);
            ApplyColors();
        }

        public void SetBaseFace(BeanFace face)
        {
            baseFace = face;
        }

        /// <summary>Shows a face for a short time, then returns to the base face.</summary>
        public void ShowFace(BeanFace face, float duration)
        {
            if (frozen)
            {
                return;
            }

            heldFace = face;
            heldFaceUntil = Time.time + Mathf.Max(0.01f, duration);
        }

        /// <summary>Fades a coloured silhouette over the whole bean (hit flash, merge flash).</summary>
        public void Flash(Color color, float duration, float startAlpha = 1f)
        {
            Restart(ref flashRoutine, FlashRoutine(color, duration, config.FlashAlpha(startAlpha)));
        }

        /// <summary>Multiplies the bean by a colour and fades it back (troop hit tint).</summary>
        public void Tint(Color color, float duration)
        {
            Restart(ref tintRoutine, TintRoutine(color, duration));
        }

        /// <summary>Squash to a scale, then settle back to 1 with an out-back ease.</summary>
        public void Squash(Vector2 target, float inDuration, float outDuration)
        {
            Restart(ref squashRoutine, SquashRoutine(config.Squash(target), inDuration, outDuration));
        }

        /// <summary>Pushes the bean along a world direction and back (hit recoil).</summary>
        public void Recoil(Vector3 worldDirection, float distance, float duration)
        {
            Vector3 local = unitRoot != null ? unitRoot.InverseTransformDirection(worldDirection.normalized) : worldDirection.normalized;
            Restart(ref recoilRoutine, RecoilRoutine(local * config.Motion(distance), duration));
        }

        /// <summary>
        /// Attack recipe: anticipation squash, release stretch, settle; the prop acts toward the
        /// target and the face narrows (art bible Section 2, troop attack).
        /// </summary>
        public void PlayAttack(Vector3 targetWorldPosition)
        {
            if (frozen)
            {
                return;
            }

            Restart(ref squashRoutine, AttackSquashRoutine());
            if (propRenderer.enabled)
            {
                Restart(ref propRoutine, PropRoutine(targetWorldPosition));
            }

            if (!isEnemy)
            {
                ShowFace(BeanFace.Attack, config.attackFaceDuration);
            }
        }

        /// <summary>Deploy pop-in: 0 to peak to 1, then a landing squash.</summary>
        public IEnumerator PlayDeployPop()
        {
            popScale = 0f;
            float duration = Mathf.Max(0.01f, config.deployDuration);
            float peak = 1f + config.Motion(config.deployPeakScale - 1f);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                popScale = t < 0.6f
                    ? Mathf.Lerp(0f, peak, EaseOutQuad(t / 0.6f))
                    : Mathf.Lerp(peak, 1f, EaseOutQuad((t - 0.6f) / 0.4f));
                yield return null;
            }

            popScale = 1f;
            Squash(config.deployLandSquash, config.deployLandDuration * 0.4f, config.deployLandDuration * 0.6f);
        }

        /// <summary>Troop death: stretch up, then shrink to nothing with X eyes.</summary>
        public IEnumerator PlayTroopDeath()
        {
            Freeze(BeanFace.Dizzy);
            float duration = Mathf.Max(0.01f, config.troopDeathDuration);
            Vector2 stretched = new(1f / Mathf.Max(1f, config.troopDeathStretch), config.troopDeathStretch);
            yield return LerpSquash(squash, stretched, duration * 0.35f, EaseOutQuad);
            yield return LerpSquash(squash, Vector2.zero, duration, EaseInQuad);
            SetVisible(false);
        }

        /// <summary>Enemy death: squash flat, then pop (the caller spawns the chunks).</summary>
        public IEnumerator PlayEnemyDeath()
        {
            Freeze(BeanFace.Dizzy);
            yield return LerpSquash(squash, config.Squash(config.enemyDeathSquash), config.enemyDeathSquashDuration, EaseOutQuad);
            SetVisible(false);
        }

        /// <summary>Hides the badge and returns its sprite and world size so it can pop off as an effect.</summary>
        public Sprite DetachBadge(out Vector3 position, out float worldSize)
        {
            position = badgeRenderer.transform.position;
            worldSize = config.PxToWorld(BadgeWorldPx);
            Sprite badge = badgeRenderer.enabled ? badgeRenderer.sprite : null;
            badgeRenderer.enabled = false;
            return badge;
        }

        private void Freeze(BeanFace face)
        {
            frozen = true;
            StopRoutine(ref squashRoutine);
            StopRoutine(ref propRoutine);
            StopRoutine(ref recoilRoutine);
            heldFaceUntil = -1f;
            baseFace = face;
            dragging = false;
            highlighted = false;
            shadowRenderer.enabled = false;
            badgeRenderer.enabled = false;
            glowRenderer.enabled = false;
        }

        private void SetVisible(bool visible)
        {
            shadowRenderer.enabled = visible;
            glowRenderer.enabled = false;
            wingsRenderer.enabled = visible && wingsRenderer.sprite != null;
            bodyRenderer.enabled = visible;
            accentRenderer.enabled = visible && tier >= 3 && accentRenderer.sprite != null;
            faceRenderer.enabled = visible;
            propRenderer.enabled = visible && propRenderer.sprite != null;
            flashRenderer.enabled = false;
            badgeRenderer.enabled = false;
        }

        private void LateUpdate()
        {
            float time = Time.time;
            float dt = Time.deltaTime;
            float bob = 0f;
            float breath = 1f;
            float tilt = 0f;

            if (!frozen)
            {
                if (isEnemy && marching)
                {
                    marchPhase += dt * marchRate / Mathf.Max(0.05f, config.marchHopPeriod);
                    float hop = Mathf.Sin(marchPhase * Mathf.PI);
                    bob = Mathf.Abs(hop) * config.Motion(config.marchHopPercent) * bodyHeight;
                    tilt = hop * config.Motion(config.marchTiltDegrees);
                }
                else
                {
                    float wave = Mathf.Sin(idlePhase + (time * Mathf.PI * 2f / Mathf.Max(0.05f, config.idleBobPeriod)));
                    bob = wave * config.Motion(config.idleBobPercent) * bodyHeight * 0.5f;
                    breath = 1f + ((wave + 1f) * 0.5f * config.Motion(config.idleBreathAmount));
                }
            }

            dragBlend = Mathf.MoveTowards(dragBlend, dragging ? 1f : 0f, Time.unscaledDeltaTime * 12f);
            float dragScale = Mathf.Lerp(1f, config.dragPickupScale, dragBlend);
            float wobble = Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f * config.dragWobbleFrequency) * config.Motion(config.dragWobbleDegrees) * dragBlend;

            float scale = breath * dragScale * popScale;
            transform.localPosition = restPosition + (Vector3.up * bob) + recoilOffset;
            transform.localScale = new Vector3(squash.x * scale, squash.y * scale, 1f);
            transform.localRotation = Quaternion.Euler(0f, 0f, tilt + wobble);

            if (wingsRenderer.enabled)
            {
                float flap = Mathf.Abs(Mathf.Sin(time * Mathf.PI * 2f * 2.5f));
                wingsRenderer.transform.localScale = new Vector3(1f, 1f - (flap * config.Motion(config.flyerWingFlapDegrees) / 60f), 1f);
            }

            propRenderer.transform.localPosition = propOffset;
            propRenderer.transform.localScale = new Vector3(propScale, propScale, 1f);
            propRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, propAngle);

            UpdateFace(time);
            UpdateGlow();
            UpdateShadowAndBadge(dragScale * popScale);
        }

        private void UpdateFace(float time)
        {
            if (art == null || faceRenderer == null)
            {
                return;
            }

            BeanFace shown = baseFace;
            if (time < heldFaceUntil)
            {
                shown = heldFace;
            }
            else if (!frozen && baseFace == BeanFace.Idle)
            {
                if (time >= nextBlinkTime)
                {
                    blinkUntil = time + config.blinkDuration;
                    nextBlinkTime = time + Random.Range(config.blinkIntervalMin, Mathf.Max(config.blinkIntervalMin, config.blinkIntervalMax));
                }

                if (time < blinkUntil)
                {
                    shown = BeanFace.Blink;
                }
            }

            Sprite faceSprite = art.GetFace(shown, isEnemy);
            if (faceRenderer.sprite != faceSprite)
            {
                faceRenderer.sprite = faceSprite;
            }
        }

        private void UpdateGlow()
        {
            if (frozen || glowRenderer.sprite == null)
            {
                glowRenderer.enabled = false;
                return;
            }

            float glowScale;
            Color glowColor;
            if (highlighted)
            {
                float pulse = Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / Mathf.Max(0.05f, config.highlightPulsePeriod));
                glowScale = config.highlightRimScale * (1f + (pulse * 0.03f));
                glowColor = new Color(1f, 1f, 1f, 0.9f * alpha);
            }
            else if (tier >= 3)
            {
                float pulse = Mathf.Sin(Time.time * 2.5f);
                glowScale = config.tierThreeGlowScale;
                glowColor = config.gold;
                glowColor.a = config.tierThreeGlowAlpha * (0.85f + (pulse * 0.15f)) * alpha;
            }
            else
            {
                glowRenderer.enabled = false;
                return;
            }

            glowRenderer.enabled = true;
            glowRenderer.color = glowColor;
            // Scale the silhouette about the body centre rather than the feet pivot.
            glowRenderer.transform.localScale = new Vector3(glowScale, glowScale, 1f);
            glowRenderer.transform.localPosition = new Vector3(0f, bodyCenterY * (1f - glowScale), 0f);
        }

        private void UpdateShadowAndBadge(float liftScale)
        {
            if (unitRoot == null)
            {
                return;
            }

            float rootScale = Mathf.Max(0.0001f, Mathf.Abs(unitRoot.lossyScale.x));
            if (shadowRenderer.enabled)
            {
                float grow = Mathf.Lerp(1f, config.dragShadowGrow, dragBlend);
                float shadowScale = shadowBaseScale * grow * liftScale;
                shadowRenderer.transform.localScale = new Vector3(shadowScale, shadowScale, 1f);
                shadowRenderer.color = new Color(config.ink.r, config.ink.g, config.ink.b, 0.3f * alpha);
            }

            if (badgeRenderer.enabled && badgeRenderer.sprite != null)
            {
                float spriteSize = Mathf.Max(0.0001f, badgeRenderer.sprite.bounds.size.x);
                float localSize = config.PxToWorld(BadgeWorldPx) / rootScale;
                badgeRenderer.transform.localScale = Vector3.one * (localSize / spriteSize);
                badgeRenderer.transform.localPosition = restPosition + new Vector3(0f, -localSize * 0.15f, 0f);
            }
        }

        private void ApplyColors()
        {
            if (bodyRenderer == null)
            {
                return;
            }

            Color layerColor = new(tint.r, tint.g, tint.b, alpha);
            bodyRenderer.color = layerColor;
            propRenderer.color = layerColor;
            wingsRenderer.color = layerColor;
            faceRenderer.color = new Color(1f, 1f, 1f, alpha);
            Color accent = config.gold;
            accent.a = alpha;
            accentRenderer.color = accent;
            badgeRenderer.color = new Color(1f, 1f, 1f, alpha);
            flashRenderer.enabled = flashAlpha > 0.001f;
            flashRenderer.color = new Color(flashColor.r, flashColor.g, flashColor.b, flashAlpha * alpha);
        }

        private IEnumerator FlashRoutine(Color color, float duration, float startAlpha)
        {
            flashColor = color;
            float elapsed = 0f;
            duration = Mathf.Max(0.01f, duration);
            while (elapsed < duration)
            {
                flashAlpha = Mathf.Lerp(startAlpha, 0f, elapsed / duration);
                ApplyColors();
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            flashAlpha = 0f;
            ApplyColors();
            flashRoutine = null;
        }

        private IEnumerator TintRoutine(Color color, float duration)
        {
            float elapsed = 0f;
            duration = Mathf.Max(0.01f, duration);
            while (elapsed < duration)
            {
                tint = Color.Lerp(color, Color.white, elapsed / duration);
                ApplyColors();
                elapsed += Time.deltaTime;
                yield return null;
            }

            tint = Color.white;
            ApplyColors();
            tintRoutine = null;
        }

        private IEnumerator SquashRoutine(Vector2 target, float inDuration, float outDuration)
        {
            yield return LerpSquash(squash, target, inDuration, EaseOutQuad);
            yield return LerpSquash(target, Vector2.one, outDuration, EaseOutBack);
            squash = Vector2.one;
            squashRoutine = null;
        }

        private IEnumerator AttackSquashRoutine()
        {
            yield return LerpSquash(squash, config.Squash(config.attackAnticipation), config.attackAnticipationDuration, EaseOutQuad);
            yield return LerpSquash(squash, config.Squash(config.attackRelease), config.attackReleaseDuration, EaseOutQuad);
            yield return LerpSquash(squash, Vector2.one, config.attackSettleDuration, EaseOutBack);
            squash = Vector2.one;
            squashRoutine = null;
        }

        private IEnumerator LerpSquash(Vector2 from, Vector2 to, float duration, System.Func<float, float> ease)
        {
            float elapsed = 0f;
            duration = Mathf.Max(0.001f, duration);
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                squash = Vector2.LerpUnclamped(from, to, ease(Mathf.Clamp01(elapsed / duration)));
                yield return null;
            }

            squash = to;
        }

        private IEnumerator RecoilRoutine(Vector3 localOffset, float duration)
        {
            float elapsed = 0f;
            duration = Mathf.Max(0.01f, duration);
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                recoilOffset = localOffset * Mathf.Sin(t * Mathf.PI);
                yield return null;
            }

            recoilOffset = Vector3.zero;
            recoilRoutine = null;
        }

        private IEnumerator PropRoutine(Vector3 targetWorldPosition)
        {
            float outDuration = config.attackAnticipationDuration + config.attackReleaseDuration;
            float backDuration = config.attackSettleDuration;
            Vector3 direction = transform.InverseTransformDirection(targetWorldPosition - transform.position);
            direction.z = 0f;
            direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.up;
            float jab = config.Motion(config.propJabPercent) * bodyHeight;

            float elapsed = 0f;
            float total = outDuration + backDuration;
            while (elapsed < total)
            {
                elapsed += Time.deltaTime;
                float t = elapsed < outDuration
                    ? EaseOutQuad(Mathf.Clamp01(elapsed / outDuration))
                    : 1f - EaseOutQuad(Mathf.Clamp01((elapsed - outDuration) / backDuration));
                switch (propAction)
                {
                    case BeanPropAction.Jab:
                        propOffset = direction * (jab * t);
                        break;
                    case BeanPropAction.Squeeze:
                        propScale = Mathf.Lerp(1f, 0.8f, t);
                        break;
                    case BeanPropAction.Raise:
                        propAngle = Mathf.Lerp(0f, -20f, t);
                        propOffset = Vector3.up * (jab * 0.5f * t);
                        break;
                    case BeanPropAction.Pulse:
                        propScale = Mathf.Lerp(1f, 1f + config.Motion(0.2f), t);
                        break;
                }

                yield return null;
            }

            propOffset = Vector3.zero;
            propScale = 1f;
            propAngle = 0f;
            propRoutine = null;
        }

        private void Restart(ref Coroutine routine, IEnumerator next)
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            StopRoutine(ref routine);
            routine = StartCoroutine(next);
        }

        private void StopRoutine(ref Coroutine routine)
        {
            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
            }
        }

        private static float EaseOutQuad(float t)
        {
            return 1f - ((1f - t) * (1f - t));
        }

        private static float EaseInQuad(float t)
        {
            return t * t;
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float p = t - 1f;
            return 1f + (c3 * p * p * p) + (c1 * p * p);
        }
    }
}
