using System.Collections;
using TMPro;
using UnityEngine;

namespace MergeAndMarch.Gameplay
{
    public class DamageNumber : MonoBehaviour
    {
        private const float Lifetime = 0.75f;
        private const float FloatDistance = 0.4f;
        private const float CanvasScale = 0.01f;
        private static readonly Vector2 CanvasSize = new(96f, 48f);

        public static void Spawn(Vector3 worldPosition, float amount, Color color)
        {
            string value = FormatDamage(amount);
            GameObject root = new($"Damage_{value}");
            DamageNumber damageNumber = root.AddComponent<DamageNumber>();
            damageNumber.Initialize(worldPosition, value, color);
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

        private TextMeshProUGUI textMesh;
        private Color baseColor;

        private void Initialize(Vector3 worldPosition, string value, Color color)
        {
            transform.position = worldPosition;
            transform.localScale = Vector3.one * CanvasScale;
            baseColor = color;

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
            textMesh.fontSize = 32f;
            textMesh.fontStyle = FontStyles.Bold;
            textMesh.alignment = TextAlignmentOptions.Center;
            textMesh.color = color;
            textMesh.text = value;
            textMesh.textWrappingMode = TextWrappingModes.NoWrap;
            textMesh.outlineWidth = 0.16f;
            textMesh.outlineColor = Color.black;
            textMesh.raycastTarget = false;

            RectTransform textRect = textMesh.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            textMesh.ForceMeshUpdate();
            StartCoroutine(FloatRoutine());
        }

        private IEnumerator FloatRoutine()
        {
            Vector3 start = transform.position;
            Vector3 end = start + (Vector3.up * FloatDistance);
            float elapsed = 0f;

            while (elapsed < Lifetime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / Lifetime);
                transform.position = Vector3.Lerp(start, end, t);
                float alpha = t < 0.5f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.5f) / 0.5f);
                textMesh.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
                yield return null;
            }

            Destroy(gameObject);
        }
    }
}
