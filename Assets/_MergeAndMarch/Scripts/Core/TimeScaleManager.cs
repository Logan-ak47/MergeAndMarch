using UnityEngine;

namespace MergeAndMarch.Core
{
    public class TimeScaleManager : MonoBehaviour
    {
        [SerializeField] private float defaultTimeScale = 1f;

        public static TimeScaleManager Instance { get; private set; }

        private float requestedTimeScale = 1f;
        private float hitStopUntil = -1f;

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

        private void Update()
        {
            if (hitStopUntil > 0f && Time.unscaledTime >= hitStopUntil)
            {
                hitStopUntil = -1f;
                ApplyTimeScale();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                hitStopUntil = -1f;
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

        /// <summary>
        /// Freezes gameplay time for a few frames to sell an impact (art bible Section 2:
        /// merge 60 ms, tier-3 100 ms). Slow-mo or reset requests made during the freeze
        /// are kept and applied when it ends.
        /// </summary>
        public void HitStop(float duration)
        {
            if (duration <= 0f)
            {
                return;
            }

            hitStopUntil = Mathf.Max(hitStopUntil, Time.unscaledTime + duration);
            ApplyTimeScale();
        }

        private void SetTimeScale(float timeScale)
        {
            requestedTimeScale = timeScale;
            ApplyTimeScale();
        }

        private void ApplyTimeScale()
        {
            float timeScale = hitStopUntil > 0f ? 0f : requestedTimeScale;
            Time.timeScale = timeScale;
            Time.fixedDeltaTime = 0.02f * Mathf.Max(timeScale, 0.01f);
        }
    }
}
