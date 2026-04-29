using System.Collections;
using TMPro;
using UnityEngine;

namespace MergeAndMarch.Gameplay
{
    public class DamageNumber : MonoBehaviour
    {
        private const float Lifetime = 0.75f;
        private const float FloatDistance = 0.75f;
        private const float CanvasScale = 0.01f;
        private static readonly Vector2 CanvasSize = new(140f, 70f);

        public static void Spawn(Vector3 worldPosition, float amount, Color color)
        {
            GameObject root = new($"Damage_{Mathf.RoundToInt(amount)}");
            DamageNumber damageNumber = root.AddComponent<DamageNumber>();
            damageNumber.Initialize(worldPosition, Mathf.RoundToInt(amount).ToString(), color);
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
            textMesh.fontSize = 54f;
            textMesh.fontStyle = FontStyles.Bold;
            textMesh.alignment = TextAlignmentOptions.Center;
            textMesh.color = color;
            textMesh.text = value;
            textMesh.textWrappingMode = TextWrappingModes.NoWrap;
            textMesh.outlineWidth = 0.2f;
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
                float alpha = t < 0.25f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.25f) / 0.75f);
                textMesh.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
                yield return null;
            }

            Destroy(gameObject);
        }
    }
}
