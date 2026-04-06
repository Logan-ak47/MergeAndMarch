using System.Collections;
using MergeAndMarch.Core;
using MergeAndMarch.Data;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MergeAndMarch.Gameplay
{
    public class MergeController : MonoBehaviour
    {
        [SerializeField] private GameConfig gameConfig;
        [SerializeField] private BattleGrid battleGrid;
        [SerializeField] private Camera targetCamera;

        private Troop draggedTroop;
        private Vector3 dragOffset;
        private Vector3 originalWorldPosition;
        private bool isResolving;

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

            Color targetColor = target.Renderer.color;
            Color sourceColor = source.Renderer.color;
            target.Renderer.color = Color.white;
            source.Renderer.color = Color.white;
            yield return WaitRealtime(gameConfig.mergeFlashDuration);
            target.Renderer.color = targetColor;
            source.Renderer.color = sourceColor;

            if (source != null)
            {
                Destroy(source.gameObject);
            }

            target.UpgradeTier(gameConfig);
            target.SetVisualSizeBoost(gameConfig.mergeOvershootScale);
            yield return AnimateVisualSizeBoost(target, 1f, gameConfig.mergePopDuration);

            draggedTroop = null;
            isResolving = false;
            RestoreTime();
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
                if (troop == null)
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

        private IEnumerator WaitRealtime(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }
    }
}
