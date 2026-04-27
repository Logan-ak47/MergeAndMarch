using System.Collections;
using System.Collections.Generic;
using MergeAndMarch.Core;
using MergeAndMarch.Data;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.InputSystem;

namespace MergeAndMarch.Gameplay
{
    public class MergeController : MonoBehaviour
    {
        [SerializeField] private GameConfig gameConfig;
        [SerializeField] private BattleGrid battleGrid;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private WaveManager waveManager;
        [Header("Merge Juice")]
        [SerializeField] private ParticleSystem mergeBurstPrefab;
        [SerializeField] private float mergeBurstLifetime = 0.4f;
        [SerializeField] private int mergeBurstCount = 25;
        [SerializeField] private float mergeBurstRadius = 0.2f;
        [SerializeField] private float mergeBurstSpeed = 3f;
        [SerializeField] private float mergeResultPopDuration = 0.3f;
        [SerializeField] private float mergeResultPopOvershoot = 1.7f;
        [SerializeField] private float mergeShakeDuration = 0.2f;
        [SerializeField] private float mergeShakeStrength = 0.15f;
        [SerializeField] private int mergeShakeVibrato = 10;

        private Troop draggedTroop;
        private Vector3 dragOffset;
        private Vector3 originalWorldPosition;
        private bool isResolving;
        private readonly List<Troop> troopScanBuffer = new();
        private readonly List<Troop> highlightedTroops = new();
        private readonly List<Troop> dimmedTroops = new();

        private void Awake()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }
        }

        private void Update()
        {
            if (isResolving)
            {
                return;
            }

            if (targetCamera == null)
            {
                targetCamera = Camera.main;
                if (targetCamera == null)
                {
                    return;
                }
            }

            if (TryGetPointerDown(out Vector3 pointerDownWorld))
            {
                BeginDrag(pointerDownWorld);
            }

            if (draggedTroop == null)
            {
                return;
            }

            if (TryGetPointerWorldPosition(out Vector3 pointerWorldPosition))
            {
                UpdateDrag(pointerWorldPosition + dragOffset);
            }

            if (TryGetPointerUp(out Vector3 pointerUpWorld))
            {
                EndDrag(pointerUpWorld);
            }
        }

        public bool TrySimulateMerge(Troop source, Troop target)
        {
            if (isResolving || draggedTroop != null || source == null || target == null)
            {
                return false;
            }

            if (!source.CanMergeWith(target))
            {
                return false;
            }

            ClearAllHighlights();
            StartCoroutine(ResolveMerge(source, target));
            return true;
        }

        private void BeginDrag(Vector3 worldPosition)
        {
            Troop troop = GetTroopUnderPoint(worldPosition);
            if (troop == null)
            {
                return;
            }

            draggedTroop = troop;
            originalWorldPosition = troop.transform.position;
            dragOffset = troop.transform.position - worldPosition;
            draggedTroop.SetDragging(true);

            if (TimeScaleManager.Instance != null && gameConfig != null)
            {
                TimeScaleManager.Instance.SetGameplaySlowMo(gameConfig.tacticalSlowTimeScale);
            }

            HighlightMergeTargets(draggedTroop);
        }

        private void UpdateDrag(Vector3 worldPosition)
        {
            if (draggedTroop == null)
            {
                return;
            }

            draggedTroop.transform.position = new Vector3(worldPosition.x, worldPosition.y, originalWorldPosition.z);
        }

        private void EndDrag(Vector3 worldPosition)
        {
            if (draggedTroop == null)
            {
                return;
            }

            ClearAllHighlights();
            draggedTroop.SetDragging(false);

            if (!battleGrid.TryGetNearestSlot(worldPosition, out int targetColumn, out int targetRow))
            {
                StartCoroutine(AnimateMove(draggedTroop, originalWorldPosition, gameConfig.dragReturnDuration, RestoreTime));
                draggedTroop = null;
                return;
            }

            Troop targetTroop = battleGrid.GetTroopAt(targetColumn, targetRow);

            if (targetTroop == null)
            {
                ResolveMoveToEmpty(targetColumn, targetRow);
                return;
            }

            if (targetTroop == draggedTroop)
            {
                StartCoroutine(AnimateMove(draggedTroop, originalWorldPosition, gameConfig.dragReturnDuration, RestoreTime));
                draggedTroop = null;
                return;
            }

            if (draggedTroop.CanMergeWith(targetTroop))
            {
                StartCoroutine(ResolveMerge(draggedTroop, targetTroop));
                return;
            }

            StartCoroutine(ResolveSwap(draggedTroop, targetTroop));
        }

        private void ResolveMoveToEmpty(int targetColumn, int targetRow)
        {
            Troop troop = draggedTroop;
            battleGrid.RemoveTroop(troop);
            battleGrid.RegisterTroop(troop, targetColumn, targetRow, moveToSlot: false);
            Vector3 destination = battleGrid.GetSlotWorldPosition(targetColumn, targetRow);
            draggedTroop = null;
            StartCoroutine(AnimateMove(troop, destination, gameConfig.dragReturnDuration, RestoreTime));
        }

        private IEnumerator ResolveSwap(Troop first, Troop second)
        {
            isResolving = true;

            int firstColumn = first.Column;
            int firstRow = first.Row;
            int secondColumn = second.Column;
            int secondRow = second.Row;

            battleGrid.RemoveTroop(first);
            battleGrid.RemoveTroop(second);
            battleGrid.RegisterTroop(first, secondColumn, secondRow, moveToSlot: false);
            battleGrid.RegisterTroop(second, firstColumn, firstRow, moveToSlot: false);

            Vector3 firstDestination = battleGrid.GetSlotWorldPosition(secondColumn, secondRow);
            Vector3 secondDestination = battleGrid.GetSlotWorldPosition(firstColumn, firstRow);

            yield return AnimateMove(first, firstDestination, gameConfig.swapMoveDuration);
            yield return AnimateMove(second, secondDestination, gameConfig.swapMoveDuration);

            draggedTroop = null;
            isResolving = false;
            RestoreTime();
        }

        private IEnumerator ResolveMerge(Troop source, Troop target)
        {
            isResolving = true;

            battleGrid.RemoveTroop(source);
            source.SetGridPosition(source.transform.position, -1, -1);

            Vector3 targetPosition = battleGrid.GetSlotWorldPosition(target.Column, target.Row);
            yield return AnimateMove(source, targetPosition, gameConfig.mergeSlideDuration);

            yield return FlashTroopsWhite(source, target, gameConfig.mergeFlashDuration);
            SpawnMergeBurst(targetPosition, ResolveMergeColor(target));
            StartCoroutine(AnimateCameraShake());

            if (source != null)
            {
                Destroy(source.gameObject);
            }

            bool boostedMerge = CardSystem.Instance != null && CardSystem.Instance.ConsumeNextMergeBoost();
            target.UpgradeTier(gameConfig);
            if (boostedMerge && target.Tier < 3)
            {
                target.UpgradeTier(gameConfig);
            }

            ResolveWaveManager();
            waveManager?.RegisterMerge();
            target.SetVisualSizeBoost(0.01f);
            yield return AnimateVisualPopIn(target, 0.01f, 1f, mergeResultPopDuration, mergeResultPopOvershoot);
            ApplyMergeHeal(target);

            draggedTroop = null;
            isResolving = false;
            RestoreTime();
        }

        private void ApplyMergeHeal(Troop mergeTarget)
        {
            if (mergeTarget == null || CardSystem.Instance == null)
            {
                return;
            }

            float healPercent = CardSystem.Instance.runBuffs.mergeHealPercent;
            if (healPercent <= 0f)
            {
                return;
            }

            HealTroopAt(mergeTarget.Column - 1, mergeTarget.Row, healPercent);
            HealTroopAt(mergeTarget.Column + 1, mergeTarget.Row, healPercent);
            HealTroopAt(mergeTarget.Column, mergeTarget.Row - 1, healPercent);
            HealTroopAt(mergeTarget.Column, mergeTarget.Row + 1, healPercent);
        }

        private void HealTroopAt(int column, int row, float healPercent)
        {
            Troop troop = battleGrid.GetTroopAt(column, row);
            if (troop != null)
            {
                troop.HealPercent(healPercent);
            }
        }

        private void RestoreTime()
        {
            if (TimeScaleManager.Instance != null)
            {
                TimeScaleManager.Instance.ResetTimeScale();
            }
        }

        private Troop GetTroopUnderPoint(Vector3 worldPosition)
        {
            Collider2D[] hits = Physics2D.OverlapPointAll(worldPosition);
            Troop closestTroop = null;
            float closestDistance = float.MaxValue;

            foreach (Collider2D hit in hits)
            {
                Troop troop = hit.GetComponent<Troop>();
                if (troop == null || !troop.IsInteractable)
                {
                    continue;
                }

                float distance = (troop.transform.position - worldPosition).sqrMagnitude;
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestTroop = troop;
                }
            }

            return closestTroop;
        }

        private bool TryGetPointerWorldPosition(out Vector3 worldPosition)
        {
            worldPosition = Vector3.zero;

            if (!TryGetPointerScreenPosition(out Vector2 screenPosition))
            {
                return false;
            }

            Vector3 screenPoint = new(screenPosition.x, screenPosition.y, Mathf.Abs(targetCamera.transform.position.z - originalWorldPosition.z));
            worldPosition = targetCamera.ScreenToWorldPoint(screenPoint);
            return true;
        }

        private bool TryGetPointerScreenPosition(out Vector2 screenPosition)
        {
            if (Touchscreen.current != null)
            {
                var primaryTouch = Touchscreen.current.primaryTouch;
                if (primaryTouch.press.isPressed)
                {
                    screenPosition = primaryTouch.position.ReadValue();
                    return true;
                }
            }

            if (Mouse.current != null)
            {
                screenPosition = Mouse.current.position.ReadValue();
                return true;
            }

            screenPosition = default;
            return false;
        }

        private bool TryGetPointerDown(out Vector3 worldPosition)
        {
            worldPosition = Vector3.zero;

            bool down = false;
            if (Touchscreen.current != null)
            {
                down = Touchscreen.current.primaryTouch.press.wasPressedThisFrame;
            }
            else if (Mouse.current != null)
            {
                down = Mouse.current.leftButton.wasPressedThisFrame;
            }

            if (!down)
            {
                return false;
            }

            return TryGetPointerWorldPosition(out worldPosition);
        }

        private bool TryGetPointerUp(out Vector3 worldPosition)
        {
            worldPosition = Vector3.zero;

            bool up = false;
            if (Touchscreen.current != null)
            {
                up = Touchscreen.current.primaryTouch.press.wasReleasedThisFrame;
            }
            else if (Mouse.current != null)
            {
                up = Mouse.current.leftButton.wasReleasedThisFrame;
            }

            if (!up)
            {
                return false;
            }

            return TryGetPointerWorldPosition(out worldPosition);
        }

        private IEnumerator AnimateMove(Troop troop, Vector3 destination, float duration, System.Action onComplete = null)
        {
            if (troop == null)
            {
                onComplete?.Invoke();
                yield break;
            }

            Vector3 start = troop.transform.position;
            float elapsed = 0f;
            duration = Mathf.Max(0.01f, duration);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                troop.transform.position = Vector3.Lerp(start, destination, t);
                yield return null;
            }

            troop.transform.position = destination;
            onComplete?.Invoke();
        }

        private IEnumerator AnimateVisualSizeBoost(Troop troop, float destinationBoost, float duration)
        {
            float elapsed = 0f;
            duration = Mathf.Max(0.01f, duration);
            float startBoost = gameConfig.mergeOvershootScale;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                float boost = Mathf.Lerp(startBoost, destinationBoost, eased);
                troop.SetVisualSizeBoost(boost);
                yield return null;
            }

            troop.SetVisualSizeBoost(destinationBoost);
        }

        private IEnumerator AnimateVisualPopIn(Troop troop, float startBoost, float destinationBoost, float duration, float overshoot)
        {
            if (troop == null)
            {
                yield break;
            }

            float elapsed = 0f;
            duration = Mathf.Max(0.01f, duration);
            troop.SetVisualSizeBoost(startBoost);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = EaseOutBack(t, overshoot);
                float boost = Mathf.LerpUnclamped(startBoost, destinationBoost, eased);
                troop.SetVisualSizeBoost(boost);
                yield return null;
            }

            troop.SetVisualSizeBoost(destinationBoost);
        }

        private IEnumerator FlashTroopsWhite(Troop source, Troop target, float duration)
        {
            if (source == null || target == null || source.Renderer == null || target.Renderer == null)
            {
                yield break;
            }

            Color targetColor = target.Renderer.color;
            Color sourceColor = source.Renderer.color;
            target.Renderer.color = Color.white;
            source.Renderer.color = Color.white;

            yield return WaitRealtime(duration);

            if (target != null && target.Renderer != null)
            {
                target.Renderer.color = targetColor;
            }

            if (source != null && source.Renderer != null)
            {
                source.Renderer.color = sourceColor;
            }
        }

        private void SpawnMergeBurst(Vector3 position, Color troopColor)
        {
            ParticleSystem burst = mergeBurstPrefab != null
                ? Instantiate(mergeBurstPrefab, position, Quaternion.identity)
                : CreateRuntimeMergeBurst(position);

            ConfigureMergeBurst(burst, troopColor);
            burst.Play();
            Destroy(burst.gameObject, mergeBurstLifetime + 0.35f);
        }

        private ParticleSystem CreateRuntimeMergeBurst(Vector3 position)
        {
            GameObject burstObject = new("MergeBurstFX");
            burstObject.transform.position = position;
            ParticleSystem burst = burstObject.AddComponent<ParticleSystem>();
            ParticleSystemRenderer renderer = burst.GetComponent<ParticleSystemRenderer>();
            renderer.sortingLayerName = "Effects";
            renderer.sortingOrder = 8;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;

            return burst;
        }

        private void ConfigureMergeBurst(ParticleSystem burst, Color troopColor)
        {
            if (burst == null)
            {
                return;
            }

            burst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = burst.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = mergeBurstLifetime;
            main.startLifetime = mergeBurstLifetime;
            main.startSpeed = mergeBurstSpeed;
            main.startSize = 0.15f;
            main.maxParticles = Mathf.Max(mergeBurstCount, 1);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.gravityModifier = 0f;

            var emission = burst.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Max(1, mergeBurstCount)) });

            var shape = burst.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = mergeBurstRadius;

            var sizeOverLifetime = burst.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));

            var colorOverLifetime = burst.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(troopColor, 0.45f),
                    new GradientColorKey(troopColor, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.9f, 0.45f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

            var velocityOverLifetime = burst.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.space = ParticleSystemSimulationSpace.Local;
            velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(0f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(0f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            var limitVelocity = burst.limitVelocityOverLifetime;
            limitVelocity.enabled = true;
            limitVelocity.limit = mergeBurstSpeed;
            limitVelocity.dampen = 0.9f;
            limitVelocity.separateAxes = false;

            var renderer = burst.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.sortingLayerName = "Effects";
                renderer.sortingOrder = 8;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            burst.Clear();
        }

        private IEnumerator AnimateCameraShake()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            if (targetCamera == null)
            {
                yield break;
            }

            Transform cameraTransform = targetCamera.transform;
            Vector3 originalPosition = cameraTransform.position;
            int shakes = Mathf.Max(1, mergeShakeVibrato);

            for (int i = 0; i < shakes; i++)
            {
                float normalized = shakes <= 1 ? 1f : i / (float)(shakes - 1);
                float strength = Mathf.Lerp(mergeShakeStrength, 0f, normalized);
                Vector2 offset2D = Random.insideUnitCircle * strength;
                cameraTransform.position = originalPosition + new Vector3(offset2D.x, offset2D.y, 0f);
                yield return WaitRealtime(mergeShakeDuration / shakes);
            }

            cameraTransform.position = originalPosition;
        }

        private Color ResolveMergeColor(Troop troop)
        {
            if (troop != null && troop.Data != null)
            {
                return troop.Data.troopColor;
            }

            if (troop != null && troop.Renderer != null)
            {
                return troop.Renderer.color;
            }

            return Color.white;
        }

        private static float EaseOutBack(float t, float overshoot)
        {
            float c1 = overshoot;
            float c3 = c1 + 1f;
            float p = t - 1f;
            return 1f + (c3 * p * p * p) + (c1 * p * p);
        }

        private IEnumerator WaitRealtime(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private void HighlightMergeTargets(Troop dragged)
        {
            if (battleGrid == null || dragged == null)
            {
                return;
            }

            battleGrid.GetTroops(troopScanBuffer);
            for (int i = 0; i < troopScanBuffer.Count; i++)
            {
                Troop troop = troopScanBuffer[i];
                if (troop == null || troop == dragged || !troop.IsInteractable)
                {
                    continue;
                }

                if (troop.CanMergeWith(dragged))
                {
                    troop.SetMergeHighlight(true);
                    highlightedTroops.Add(troop);
                }
                else
                {
                    troop.SetDimmed(true);
                    dimmedTroops.Add(troop);
                }
            }
        }

        private void ClearAllHighlights()
        {
            for (int i = 0; i < highlightedTroops.Count; i++)
            {
                if (highlightedTroops[i] != null)
                {
                    highlightedTroops[i].SetMergeHighlight(false);
                }
            }

            for (int i = 0; i < dimmedTroops.Count; i++)
            {
                if (dimmedTroops[i] != null)
                {
                    dimmedTroops[i].SetDimmed(false);
                }
            }

            highlightedTroops.Clear();
            dimmedTroops.Clear();
        }

        private void ResolveWaveManager()
        {
            if (waveManager == null)
            {
                waveManager = FindFirstObjectByType<WaveManager>();
            }
        }
    }
}
