using System.Collections;
using UnityEngine;

namespace MergeAndMarch.Gameplay
{
    public class CombatFeedbackFx : MonoBehaviour
    {
        public void PlayFade(float lifetime)
        {
            StartCoroutine(FadeRoutine(Mathf.Max(0.01f, lifetime)));
        }

        private IEnumerator FadeRoutine(float lifetime)
        {
            SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>();
            Color[] startColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                startColors[i] = renderers[i].color;
            }

            Vector3 startScale = transform.localScale;
            float elapsed = 0f;
            while (elapsed < lifetime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / lifetime);
                transform.localScale = Vector3.Lerp(startScale, startScale * 1.18f, t);

                for (int i = 0; i < renderers.Length; i++)
                {
                    Color color = startColors[i];
                    color.a = Mathf.Lerp(startColors[i].a, 0f, t);
                    renderers[i].color = color;
                }

                yield return null;
            }

            Destroy(gameObject);
        }
    }
}
