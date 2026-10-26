using System.Collections;
using System.Collections.Generic;
using MergeAndMarch.Data;
using TMPro;
using UnityEngine;

namespace MergeAndMarch.Gameplay
{
    /// <summary>
    /// Floating world-space numbers and labels (art bible Section 2, "Hit on enemy" numbers;
    /// Section 7 caps). Numbers pop to 1.3x, rise and fade; at most 12 are on screen, oldest first out.
    /// </summary>
    /// <example>
    /// <code>
    /// DamageNumber.Spawn(enemy.transform.position, 42f, Color.white, emphasised: troop.Tier >= 3);
    /// DamageNumber.SpawnLabel(slotPosition, "TIER 2!", Color.white, 2f);
    /// </code>
    /// </example>
    public class DamageNumber : MonoBehaviour
    {
        private const float CanvasScale = 0.01f;
        private static readonly Vector2 CanvasSize = new(220f, 80f);
        private static readonly List<DamageNumber> liveNumbers = new();

        private TextMeshProUGUI textMesh;
        private Color baseColor;
        private float popScale;
        private float popDuration;
        private float rise;
        private float lifetime;
        private bool countsTowardCap;
        private bool unscaled;

        /// <summary>Spawns a damage number; ignored when damage numbers are turned off.</summary>
        public static void Spawn(Vector3 worldPosition, float amount, Color color, bool emphasised = false)
        {
            JuiceConfig juice = ResolveConfig();
            if (!juice.damageNumbersEnabled)
            {
                return;
            }

            while (liveNumbers.Count >= Mathf.Max(1, juice.damageNumbersMaxOnScreen))
            {
                DamageNumber oldest = liveNumbers[0];
                liveNumbers.RemoveAt(0);
                if (oldest != null)
                {
                    Destroy(oldest.gameObject);
                }
            }

            string value = FormatDamage(amount);
            float sizePx = juice.damageNumberFontSize * (emphasised ? juice.damageNumberTierThreeScale : 1f);
            DamageNumber number = Create($"Damage_{value}", worldPosition, value, color, sizePx, juice);
            number.lifetime = juice.damageNumberLifetime;
            number.countsTowardCap = true;
            liveNumbers.Add(number);
        }

        /// <summary>Spawns a floating label such as "TIER 2!" (not capped, runs on unscaled time).</summary>
        public static void SpawnLabel(Vector3 worldPosition, string text, Color color, float sizeMultiplier = 1f)
        {
            JuiceConfig juice = ResolveConfig();
            DamageNumber label = Create($"Label_{text}", worldPosition, text, color, juice.damageNumberFontSize * sizeMultiplier, juice);
            label.lifetime = juice.damageNumberLifetime * 1.8f;
            label.unscaled = true;
        }

        private static JuiceConfig ResolveConfig()
        {
            JuiceFx fx = JuiceFx.Instance;
            return fx != null && fx.Config != null ? fx.Config : ScriptableObject.CreateInstance<JuiceConfig>();
        }

        private static string FormatDamage(float damage)
        {
            float absDamage = Mathf.Abs(damage);
            if (absDamage >= 100000f)
            {
                return $"{Mathf.RoundToInt(damage / 1000f)}K";
            }

            if (absDamage >= 1000f)
            {
                return $"{damage / 1000f:0.#}K";
            }

            return Mathf.RoundToInt(damage).ToString();
        }

        private static DamageNumber Create(string objectName, Vector3 worldPosition, string text, Color color, float sizePx, JuiceConfig juice)
        {
            GameObject root = new(objectName);
            DamageNumber number = root.AddComponent<DamageNumber>();
            number.Initialize(worldPosition, text, color, sizePx, juice);
            return number;
        }

        private void Initialize(Vector3 worldPosition, string value, Color color, float sizePx, JuiceConfig juice)
        {
            transform.position = worldPosition;
            transform.localScale = Vector3.one * CanvasScale;
            baseColor = color;
            popScale = juice.damageNumberPopScale;
            popDuration = juice.damageNumberPopDuration;
            rise = juice.PxToWorld(juice.damageNumberRisePx);

            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            canvas.sortingLayerName = "Effects";
            canvas.sortingOrder = 200;

            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            canvasRect.sizeDelta = CanvasSize;

            GameObject textObject = new("Text");
            textObject.transform.SetParent(transform, false);
            textMesh = textObject.AddComponent<TextMeshProUGUI>();
            textMesh.font = TMP_Settings.defaultFontAsset;
            textMesh.fontSize = juice.PxToWorld(sizePx) / CanvasScale;
            textMesh.fontStyle = FontStyles.Bold;
            textMesh.alignment = TextAlignmentOptions.Center;
            textMesh.color = color;
            textMesh.text = value;
            textMesh.textWrappingMode = TextWrappingModes.NoWrap;
            textMesh.outlineWidth = 0.22f;
            textMesh.outlineColor = juice.ink;
            textMesh.raycastTarget = false;

            RectTransform textRect = textMesh.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            textMesh.ForceMeshUpdate();
            StartCoroutine(FloatRoutine());
        }

        private void OnDestroy()
        {
            if (countsTowardCap)
            {
                liveNumbers.Remove(this);
            }
        }

        private IEnumerator FloatRoutine()
        {
            Vector3 start = transform.position;
            Vector3 end = start + (Vector3.up * rise);
            float elapsed = 0f;
            lifetime = Mathf.Max(0.05f, lifetime);

            while (elapsed < lifetime)
            {
                elapsed += unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / lifetime);
                float pop = Mathf.Lerp(popScale, 1f, Mathf.Clamp01(elapsed / Mathf.Max(0.01f, popDuration)));
                transform.localScale = Vector3.one * (CanvasScale * pop);
                transform.position = Vector3.Lerp(start, end, 1f - ((1f - t) * (1f - t)));
                float alpha = t < 0.6f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.6f) / 0.4f);
                textMesh.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
                yield return null;
            }

            Destroy(gameObject);
        }
    }
}
