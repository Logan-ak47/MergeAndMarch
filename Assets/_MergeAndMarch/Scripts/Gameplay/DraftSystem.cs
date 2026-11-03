using System;
using System.Collections;
using System.Collections.Generic;
using MergeAndMarch.Core;
using MergeAndMarch.Data;
using UnityEngine;

namespace MergeAndMarch.Gameplay
{
    /// <summary>
    /// Kill-energy troop offers (design/specs/offer-tray.md). Kills and wave clears fill an energy
    /// meter; at <see cref="GameConfig.DraftThreshold"/> an offer of N tier-1 troops appears in the
    /// tray (non-modal, with a short slow-mo pulse). The player drags one card onto the grid to
    /// deploy, merge, or (grid full) replace; the rest vanish.
    ///
    /// States and transitions:
    ///   Closed --(accepting and merges idle and (free offer pending or energy &gt;= threshold))--&gt; Open
    ///   Open   --TryDeploy / TryMerge / TryReplace succeeds--&gt; Closed (a paid offer spends the threshold, n += 1)
    ///   Open   --Cancel() / ResetRun()--&gt; Closed (run ended or restarted; nothing granted)
    /// While Open the energy holds at the threshold, so offers never stack.
    /// </summary>
    public class DraftSystem : MonoBehaviour
    {
        public enum DraftState
        {
            Closed,
            Open
        }

        private readonly List<TroopData> pool = new();
        private readonly List<TroopData> offers = new();
        private readonly HashSet<TroopType> mergeableTypes = new();
        private readonly List<Troop> troopBuffer = new();
        private readonly List<Vector2Int> emptySlots = new();

        private GameConfig gameConfig;
        private BattleGrid battleGrid;
        private DeploymentSystem deployment;
        private MergeController mergeController;
        private EnemySpawner enemySpawner;
        private Coroutine slowMoRoutine;
        private bool currentOfferIsFree;
        private bool freeOfferPending;

        public DraftState State { get; private set; } = DraftState.Closed;
        public bool IsOpen => State == DraftState.Open;
        public bool IsSlowMoPulseActive => slowMoRoutine != null;
        public float Energy { get; private set; }
        public int DraftsTaken { get; private set; }
        public float Threshold => gameConfig != null ? gameConfig.DraftThreshold(DraftsTaken) : 1f;
        public IReadOnlyList<TroopData> Offers => offers;

        /// <summary>Set by the wave flow: offers only appear during a live run.</summary>
        public bool AcceptingDrafts { get; set; }

        /// <summary>Raised with the cards when an offer appears.</summary>
        public event Action<IReadOnlyList<TroopData>> DraftOpened;
        /// <summary>Raised when an offer closes for any reason.</summary>
        public event Action DraftClosed;
        /// <summary>Raised with (energy, threshold) whenever either changes.</summary>
        public event Action<float, float> EnergyChanged;
        /// <summary>Raised with the troop that an offer card put on the grid.</summary>
        public event Action<Troop> TroopDrafted;

        /// <summary>Injects dependencies; safe to call more than once.</summary>
        public void Initialize(GameConfig config, BattleGrid grid, DeploymentSystem deploymentSystem, MergeController merges, EnemySpawner spawner)
        {
            gameConfig = config;
            battleGrid = grid;
            deployment = deploymentSystem;
            mergeController = merges;

            if (enemySpawner != spawner)
            {
                if (enemySpawner != null)
                {
                    enemySpawner.EnemyDefeatedEvent -= HandleEnemyDefeated;
                }

                enemySpawner = spawner;
                if (enemySpawner != null)
                {
                    enemySpawner.EnemyDefeatedEvent += HandleEnemyDefeated;
                }
            }
        }

        private void OnDestroy()
        {
            if (enemySpawner != null)
            {
                enemySpawner.EnemyDefeatedEvent -= HandleEnemyDefeated;
            }
        }

        /// <summary>Clears energy and offer count for a new run and queues the run-start free offer.</summary>
        public void ResetRun()
        {
            CloseOffer();
            Energy = 0f;
            DraftsTaken = 0;
            freeOfferPending = gameConfig != null && gameConfig.freeDraftAtRunStart;
            RaiseEnergyChanged();
        }

        /// <summary>Closes an open offer without granting anything (run ended).</summary>
        public void Cancel()
        {
            CloseOffer();
        }

        /// <summary>Adds energy; it holds at the threshold while an offer waits.</summary>
        public void AddEnergy(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            Energy += amount;
            if (IsOpen)
            {
                Energy = Mathf.Min(Energy, Threshold);
            }

            RaiseEnergyChanged();
        }

        /// <summary>Wave-clear bonus: a fraction of the current threshold (GameConfig.waveClearEnergyFraction).</summary>
        public void GrantWaveClearEnergy()
        {
            if (gameConfig != null)
            {
                AddEnergy(Threshold * gameConfig.waveClearEnergyFraction);
            }
        }

        /// <summary>True if card <paramref name="index"/> can merge into <paramref name="target"/> (same type, tier 1).</summary>
        public bool CanMergeInto(int index, Troop target)
        {
            return IsOpen && index >= 0 && index < offers.Count && target != null && target.IsInteractable
                && target.Data != null && target.Data.troopType == offers[index].troopType && target.Tier == 1;
        }

        /// <summary>True when every grid slot is occupied (replacing is allowed only then).</summary>
        public bool IsGridFull()
        {
            if (battleGrid == null)
            {
                return false;
            }

            battleGrid.GetEmptySlots(emptySlots);
            return emptySlots.Count == 0;
        }

        /// <summary>Deploys card <paramref name="index"/> into an empty grid slot.</summary>
        public bool TryDeploy(int index, int column, int row)
        {
            if (!IsValidCard(index) || battleGrid == null || !battleGrid.IsWithinBounds(column, row) || battleGrid.GetTroopAt(column, row) != null)
            {
                return false;
            }

            Troop troop = deployment.CreateTroop(offers[index], battleGrid.GetSlotWorldPosition(column, row));
            if (troop == null)
            {
                return false;
            }

            battleGrid.RegisterTroop(troop, column, row);
            CompleteOffer(troop, 0f);
            return true;
        }

        /// <summary>Merges card <paramref name="index"/> into a matching tier-1 grid troop.</summary>
        public bool TryMerge(int index, Troop target)
        {
            if (!CanMergeInto(index, target) || mergeController == null || mergeController.IsBusy)
            {
                return false;
            }

            Troop troop = deployment.CreateTroop(offers[index], target.transform.position);
            if (troop == null)
            {
                return false;
            }

            if (!mergeController.MergeIncoming(troop, target))
            {
                Destroy(troop.gameObject);
                return false;
            }

            CompleteOffer(target, 0f);
            return true;
        }

        /// <summary>Grid full only: replaces <paramref name="target"/> with card <paramref name="index"/>, refunding some energy.</summary>
        public bool TryReplace(int index, Troop target)
        {
            if (!IsValidCard(index) || target == null || !IsGridFull() || battleGrid == null)
            {
                return false;
            }

            int column = target.Column;
            int row = target.Row;
            battleGrid.RemoveTroop(target);
            Destroy(target.gameObject);

            Troop troop = deployment.CreateTroop(offers[index], battleGrid.GetSlotWorldPosition(column, row));
            if (troop == null)
            {
                return false;
            }

            battleGrid.RegisterTroop(troop, column, row);
            float refund = gameConfig != null ? Threshold * gameConfig.replaceRefundFraction : 0f;
            CompleteOffer(troop, refund);
            return true;
        }

        private bool IsValidCard(int index)
        {
            return IsOpen && index >= 0 && index < offers.Count && deployment != null;
        }

        private void CompleteOffer(Troop troop, float refund)
        {
            if (!currentOfferIsFree)
            {
                Energy = Mathf.Max(0f, Energy - Threshold);
                DraftsTaken++;
            }

            Energy += refund;
            CloseOffer();
            RaiseEnergyChanged();
            TroopDrafted?.Invoke(troop);
        }

        private void Update()
        {
            if (IsOpen || !AcceptingDrafts)
            {
                return;
            }

            if (mergeController != null && mergeController.IsBusy)
            {
                return;
            }

            if (freeOfferPending || Energy >= Threshold)
            {
                OpenOffer(freeOfferPending);
            }
        }

        private void HandleEnemyDefeated(Enemy enemy)
        {
            if (AcceptingDrafts && enemy != null)
            {
                AddEnergy(enemy.EnergyValue);
            }
        }

        private void OpenOffer(bool free)
        {
            RollOffers();
            if (offers.Count == 0)
            {
                freeOfferPending = false;
                return;
            }

            currentOfferIsFree = free;
            freeOfferPending = false;
            State = DraftState.Open;
            Energy = Mathf.Min(Energy, Threshold);
            if (slowMoRoutine != null)
            {
                StopCoroutine(slowMoRoutine);
            }

            slowMoRoutine = StartCoroutine(SlowMoPulse());
            DraftOpened?.Invoke(offers);
            RaiseEnergyChanged();
        }

        private void CloseOffer()
        {
            if (slowMoRoutine != null)
            {
                StopCoroutine(slowMoRoutine);
                slowMoRoutine = null;
                TimeScaleManager.Instance?.ResetTimeScale();
            }

            if (!IsOpen)
            {
                return;
            }

            State = DraftState.Closed;
            offers.Clear();
            DraftClosed?.Invoke();
        }

        /// <summary>Short slow-mo when an offer appears so the player notices it; then normal speed.</summary>
        private IEnumerator SlowMoPulse()
        {
            float scale = gameConfig != null ? gameConfig.offerSlowMoScale : 0.3f;
            float duration = gameConfig != null ? gameConfig.offerSlowMoDuration : 1f;
            TimeScaleManager.Instance?.SetGameplaySlowMo(scale);
            yield return new WaitForSecondsRealtime(duration);
            slowMoRoutine = null;
            if (mergeController == null || !mergeController.IsBusy)
            {
                TimeScaleManager.Instance?.ResetTimeScale();
            }
        }

        /// <summary>
        /// Rolls distinct troop types from the weighted pool; at least one card matches a tier-1/2
        /// troop the player already owns, so a merge is always on the table.
        /// </summary>
        private void RollOffers()
        {
            offers.Clear();
            pool.Clear();
            deployment?.GetDraftPool(pool);
            int offerCount = gameConfig != null ? Mathf.Max(1, gameConfig.draftOfferCount) : 3;

            List<TroopData> remaining = new(pool);
            while (offers.Count < offerCount && remaining.Count > 0)
            {
                TroopData pick = remaining[UnityEngine.Random.Range(0, remaining.Count)];
                offers.Add(pick);
                remaining.RemoveAll(data => data == null || data.troopType == pick.troopType);
            }

            CollectMergeableTypes();
            if (mergeableTypes.Count == 0 || offers.Exists(data => mergeableTypes.Contains(data.troopType)))
            {
                return;
            }

            List<TroopData> matching = pool.FindAll(data => data != null && mergeableTypes.Contains(data.troopType));
            if (matching.Count > 0 && offers.Count > 0)
            {
                offers[UnityEngine.Random.Range(0, offers.Count)] = matching[UnityEngine.Random.Range(0, matching.Count)];
            }
        }

        private void CollectMergeableTypes()
        {
            mergeableTypes.Clear();
            troopBuffer.Clear();
            battleGrid?.GetTroops(troopBuffer);
            for (int i = 0; i < troopBuffer.Count; i++)
            {
                Troop troop = troopBuffer[i];
                if (troop != null && troop.IsAlive && troop.Data != null && troop.Tier < 3)
                {
                    mergeableTypes.Add(troop.Data.troopType);
                }
            }
        }

        private void RaiseEnergyChanged()
        {
            EnergyChanged?.Invoke(Energy, Threshold);
        }
    }
}
