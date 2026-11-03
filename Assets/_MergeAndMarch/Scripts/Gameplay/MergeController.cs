using System.Collections;
using System.Collections.Generic;
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
        [SerializeField] private WaveManager waveManager;
        [SerializeField] private DraftSystem draft;

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

        /// <summary>True while a drag or a merge/swap animation is in progress.</summary>
        public bool IsBusy => isResolving || draggedTroop != null;

        /// <summary>Set by the offer tray while a card is being dragged, so troop drags cannot start.</summary>
        public bool InputLocked { get; set; }

        /// <summary>Wires the troop draft so its slow-mo pulse is not cancelled (design/specs/offer-tray.md).</summary>
        public void SetDraft(DraftSystem draftSystem)
        {
            draft = draftSystem;
        }

        /// <summary>
        /// Merges a troop that is not on the grid (an offer card dropped on a matching troop) into
        /// <paramref name="target"/>, with the normal merge juice.
        /// </summary>
        public bool MergeIncoming(Troop incoming, Troop target)
        {
            if (isResolving || incoming == null || target == null || !incoming.CanMergeWith(target))
            {
                return false;
            }

            ClearAllHighlights();
            StartCoroutine(ResolveMerge(incoming, target));
            return true;
        }

        /// <summary>Highlights grid troops an offer card of this type could merge with and dims the rest.</summary>
        public void HighlightOfferTargets(TroopType type)
        {
            ClearAllHighlights();
            battleGrid.GetTroops(troopScanBuffer);
            for (int i = 0; i < troopScanBuffer.Count; i++)
            {
                Troop troop = troopScanBuffer[i];
                if (troop == null || !troop.IsInteractable)
                {
                    continue;
                }

                bool match = troop.Data != null && troop.Data.troopType == type && troop.Tier == 1;
                troop.SetMergeHighlight(match);
                troop.SetDimmed(!match);
                (match ? highlightedTroops : dimmedTroops).Add(troop);
            }
        }

        /// <summary>Clears offer-card highlights.</summary>
        public void ClearOfferHighlights()
        {
            ClearAllHighlights();
        }

        private void Update()
        {
            if (isResolving)
            {
                return;
            }

            if (draggedTroop == null && InputLocked)
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
            JuiceFx.Instance?.SetSlowMo(true);
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
            JuiceFx.Instance?.SetSlowMo(false);

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

        /// <summary>
        /// Merge resolve and tier-3 creation recipes (design/art/art-bible.md Section 2): the two troops
        /// pull together, flash white, the result pops from 0.6 past full size and settles, with confetti,
        /// hit-stop, shake and a "TIER N!" label from <see cref="JuiceFx"/>.
        /// </summary>
        private IEnumerator ResolveMerge(Troop source, Troop target)
        {
            isResolving = true;
            JuiceFx fx = JuiceFx.Instance;
            JuiceConfig juice = fx != null ? fx.Config : null;

            int sourceColumn = source.Column;
            int sourceRow = source.Row;
            battleGrid.RemoveTroop(source);
            source.SetGridPosition(source.transform.position, -1, -1);

            Vector3 targetPosition = battleGrid.GetSlotWorldPosition(target.Column, target.Row);
            yield return AnimateMove(source, targetPosition, juice != null ? juice.mergePullDuration : gameConfig.mergeSlideDuration);

            // The target can be killed while the merge animates; cancel instead of upgrading a destroyed troop.
            if (!IsMergeable(target))
            {
                CancelMerge(source, sourceColumn, sourceRow);
                yield break;
            }

            float flashDuration = juice != null ? juice.mergeFlashDuration : gameConfig.mergeFlashDuration;
            source.PlayMergeFlash(flashDuration);
            target.PlayMergeFlash(flashDuration);
            yield return WaitRealtime(flashDuration * 0.5f);

            if (!IsMergeable(target))
            {
                CancelMerge(source, sourceColumn, sourceRow);
                yield break;
            }

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

            fx?.MergeBurst(targetPosition, target.Tier, ResolveMergeColor(target));
            ResolveWaveManager();
            waveManager?.RegisterMerge();
            yield return AnimateMergePop(target, juice);
            ApplyMergeHeal(target);

            draggedTroop = null;
            isResolving = false;
            RestoreTime();
        }

        /// <summary>
        /// Stops any drag or merge in progress and clears its state. Called when a run starts so a
        /// merge interrupted by the previous run can never leave merging locked.
        /// </summary>
        public void ResetState()
        {
            StopAllCoroutines();
            ClearAllHighlights();
            if (draggedTroop != null)
            {
                draggedTroop.SetDragging(false);
            }

            draggedTroop = null;
            isResolving = false;
            JuiceFx.Instance?.SetSlowMo(false);
            RestoreTime();
        }

        private static bool IsMergeable(Troop troop)
        {
            return troop != null && troop.IsAlive;
        }

        /// <summary>Puts the dragged troop back on the grid after a merge whose target died.</summary>
        private void CancelMerge(Troop source, int column, int row)
        {
            if (IsMergeable(source))
            {
                bool hasSlot = column >= 0 && row >= 0 && battleGrid.GetTroopAt(column, row) == null;
                if (hasSlot || battleGrid.TryGetNearestSlot(source.transform.position, out column, out row) && battleGrid.GetTroopAt(column, row) == null)
                {
                    battleGrid.RegisterTroop(source, column, row, moveToSlot: true);
                }
                else
                {
                    Destroy(source.gameObject);
                }
            }

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
            // An offer's slow-mo pulse owns the time scale until it ends.
            if (draft != null && draft.IsSlowMoPulseActive)
            {
                return;
            }

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

        /// <summary>
        /// Pops the merge result from the spawn scale past full size and settles it; tier 3 overshoots
        /// further and adds a second bounce. Runs on unscaled time so hit-stop does not stall it.
        /// </summary>
        private IEnumerator AnimateMergePop(Troop troop, JuiceConfig juice)
        {
            if (troop == null)
            {
                yield break;
            }

            bool tierThree = troop.Tier >= 3;
            float spawnScale = juice != null ? juice.mergeSpawnScale : 0.6f;
            float overshoot = juice != null ? (tierThree ? juice.tierThreeOvershoot : juice.mergeOvershoot) : gameConfig.mergeOvershootScale;
            if (juice != null)
            {
                overshoot = 1f + juice.Motion(overshoot - 1f);
            }

            float secondBounce = tierThree && juice != null ? 1f + juice.Motion(juice.tierThreeSecondBounce - 1f) : 1f;
            float duration = juice != null ? juice.mergeSettleDuration : gameConfig.mergePopDuration;
            if (tierThree)
            {
                duration *= 1.5f;
            }

            float elapsed = 0f;
            duration = Mathf.Max(0.01f, duration);
            while (elapsed < duration && troop != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float boost;
                if (t < 0.4f)
                {
                    boost = Mathf.Lerp(spawnScale, overshoot, EaseOutQuad(t / 0.4f));
                }
                else if (!tierThree || t < 0.7f)
                {
                    float end = tierThree ? 0.7f : 1f;
                    boost = Mathf.Lerp(overshoot, tierThree ? 0.96f : 1f, EaseOutQuad((t - 0.4f) / (end - 0.4f)));
                }
                else
                {
                    float k = (t - 0.7f) / 0.3f;
                    boost = Mathf.Lerp(0.96f, 1f, k) + (Mathf.Sin(k * Mathf.PI) * (secondBounce - 1f));
                }

                troop.SetVisualSizeBoost(boost);
                yield return null;
            }

            if (troop != null)
            {
                troop.SetVisualSizeBoost(1f);
            }
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

        private static float EaseOutQuad(float t)
        {
            return 1f - ((1f - t) * (1f - t));
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
