using System.Collections;
using TMPro;
using UnityEngine;

namespace MergeAndMarch.Gameplay
{
    public class DamageNumber : MonoBehaviour
    {
        public static void Spawn(Vector3 worldPosition, float amount, Color color)
        {
            GameObject root = new($"Damage_{Mathf.RoundToInt(amount)}");
            DamageNumber damageNumber = root.AddComponent<DamageNumber>();
            damageNumber.Initialize(worldPosition, Mathf.RoundToInt(amount).ToString(), color);
        }

        private TextMeshPro textMesh;

        private void Initialize(Vector3 worldPosition, string value, Color color)
        {
            transform.position = worldPosition;
            textMesh = gameObject.AddComponent<TextMeshPro>();
            textMesh.font = TMP_Settings.defaultFontAsset;
            textMesh.fontSize = 4f;
            textMesh.fontStyle = FontStyles.Bold;
            textMesh.alignment = TextAlignmentOptions.Center;
            textMesh.color = color;
            textMesh.text = value;
            transform.localScale = Vector3.one * 0.24f;
            Renderer textRenderer = textMesh.GetComponent<Renderer>();
            textRenderer.sortingLayerName = "Effects";
            textRenderer.sortingOrder = 10;
            StartCoroutine(FloatRoutine(color));
        }

        private IEnumerator FloatRoutine(Color color)
        {
            Vector3 start = transform.position;
            Vector3 end = start + (Vector3.up * 0.8f);
            float elapsed = 0f;
            const float duration = 0.6f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                transform.position = Vector3.Lerp(start, end, t);
                textMesh.color = new Color(color.r, color.g, color.b, Mathf.Lerp(1f, 0f, t));
                yield return null;
            }

            Destroy(gameObject);
        }
    }
}
