using UnityEngine;

namespace MergeAndMarch.Core
{
    public class TimeScaleManager : MonoBehaviour
    {
        [SerializeField] private float defaultTimeScale = 1f;

        public static TimeScaleManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            SetTimeScale(defaultTimeScale);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                SetTimeScale(1f);
                Instance = null;
            }
        }

        public void SetGameplaySlowMo(float timeScale)
        {
            SetTimeScale(Mathf.Clamp(timeScale, 0.01f, 1f));
        }

        public void ResetTimeScale()
        {
            SetTimeScale(defaultTimeScale);
        }

        private void SetTimeScale(float timeScale)
        {
            Time.timeScale = timeScale;
            Time.fixedDeltaTime = 0.02f * Time.timeScale;
        }
    }
}
