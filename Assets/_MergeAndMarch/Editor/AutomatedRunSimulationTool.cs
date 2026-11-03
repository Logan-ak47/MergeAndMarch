using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MergeAndMarch.Core;
using MergeAndMarch.Data;
using MergeAndMarch.Gameplay;
using UnityEditor;
using UnityEngine;

namespace MergeAndMarch.Editor
{
    /// <summary>
    /// Balance harness: plays whole runs at high speed with a bot policy and reports per-run and
    /// per-wave metrics (wave time-to-kill, peak enemies, closest approach, troops lost, first
    /// tier-2/3, boss time-to-kill). Start it from play mode with <see cref="RunBalanceBatch"/>
    /// or the menu (greedy only). Reports go to production/qa/balance/.
    /// </summary>
    [InitializeOnLoad]
    public static class AutomatedRunSimulationTool
    {
        /// <summary>How the bot plays: greedy merges and scored cards, random play, or never merging.</summary>
        public enum BotPolicy
        {
            Greedy,
            Random,
            Passive
        }

        private const string MenuPath = "MergeAndMarch/Generate Merge-Focused Run Report";
        private const string BalanceMenuPath = "MergeAndMarch/Run Balance Batch (3 policies x 10)";
        private const string BalanceReportDirectory = "production/qa/balance";
        private const float ContactDistance = 0.5f;
        private const float HumanCardPickSeconds = 3f;
        private const float SimulationTimeScale = 14f;
        private const double MaxRunDurationSeconds = 150d;
        private const string ReportDirectory = "Assets/_MergeAndMarch/TestReports";
        private const string ReportFileName = "AutomatedRunReport_MergeSmart.md";
        private const string TriggerFileName = "AutomatedRunReport_MergeSmart.trigger";

        private static readonly List<Troop> TroopBuffer = new();
        private static readonly List<MergePair> MergeBuffer = new();
        private static readonly List<RunRecord> CompletedRuns = new();
        private static readonly List<BotPolicy> PolicyQueue = new();
        private static readonly HashSet<int> TrackedTroopIds = new();
        private static readonly HashSet<int> FrameTroopIds = new();
        private static readonly HashSet<int> MergedTroopIds = new();

        private static bool isRunning;
        private static bool playRequested;
        private static int currentRunIndex;
        private static double nextActionAt;
        private static double nextRunAt;
        private static string reportAssetPath;
        private static System.Random sessionRng;
        private static System.Random runRng;
        private static RunRecord activeRun;
        private static WaveStat activeWave;
        private static int runsPerPolicy = 5;
        private static BotPolicy currentPolicy = BotPolicy.Greedy;

        private static int TargetRunCount => Mathf.Max(1, PolicyQueue.Count) * runsPerPolicy;

        private static RunManager runManager;
        private static WaveManager waveManager;
        private static BattleGrid battleGrid;
        private static MergeController mergeController;
        private static CardSystem cardSystem;
        private static GameConfig gameConfig;
        private static RuntimeConfigSnapshot originalConfig;

        static AutomatedRunSimulationTool()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        }

        [MenuItem(MenuPath)]
        public static void GenerateAutomatedRunReport()
        {
            ConfigureBatch(5, BotPolicy.Greedy);
            BeginSession();
        }

        [MenuItem(BalanceMenuPath)]
        public static void RunDefaultBalanceBatch()
        {
            RunBalanceBatch(10);
        }

        /// <summary>
        /// Runs <paramref name="runs"/> games for each policy (greedy, random, passive by default).
        /// Call while in play mode: entering play mode reloads scripts and would drop the request.
        /// </summary>
        /// <example><code>AutomatedRunSimulationTool.RunBalanceBatch(10);</code></example>
        public static void RunBalanceBatch(int runs, params BotPolicy[] policies)
        {
            if (policies == null || policies.Length == 0)
            {
                policies = new[] { BotPolicy.Greedy, BotPolicy.Random, BotPolicy.Passive };
            }

            ConfigureBatch(runs, policies);
            BeginSession();
        }

        private static void ConfigureBatch(int runs, params BotPolicy[] policies)
        {
            if (isRunning)
            {
                return;
            }

            runsPerPolicy = Mathf.Max(1, runs);
            PolicyQueue.Clear();
            PolicyQueue.AddRange(policies);
        }

        private static void BeginSession()
        {
            if (isRunning)
            {
                Debug.LogWarning("Automated run simulation is already in progress.");
                return;
            }

            isRunning = true;
            playRequested = true;
            currentRunIndex = 0;
            nextActionAt = 0d;
            nextRunAt = 0d;
            reportAssetPath = $"{ReportDirectory}/{ReportFileName}";
            CompletedRuns.Clear();
            activeRun = null;
            originalConfig = default;
            sessionRng = new System.Random(Environment.TickCount);
            runRng = null;
            ClearReferences();

            if (EditorApplication.isPlaying)
            {
                SetupSimulationContext();
                StartNextRun();
            }
            else
            {
                EditorApplication.isPlaying = true;
            }
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange stateChange)
        {
            if (stateChange == PlayModeStateChange.EnteredPlayMode && (playRequested || HasTriggerFile()))
            {
                if (!isRunning)
                {
                    isRunning = true;
                    playRequested = true;
                    currentRunIndex = 0;
                    nextActionAt = 0d;
                    nextRunAt = 0d;
                    reportAssetPath = $"{ReportDirectory}/{ReportFileName}";
                    CompletedRuns.Clear();
                    activeRun = null;
                    originalConfig = default;
                    sessionRng = new System.Random(Environment.TickCount);
                    runRng = null;
                    ClearReferences();
                }

                Debug.Log("Automated run simulation started.");
                SetupSimulationContext();
                StartNextRun();
            }
            else if (stateChange == PlayModeStateChange.ExitingPlayMode)
            {
                RestoreRuntimeSettings();
            }
            else if (stateChange == PlayModeStateChange.EnteredEditMode)
            {
                DeleteTriggerFile();
                playRequested = false;
                isRunning = false;
                activeRun = null;
                runRng = null;
                ClearReferences();
            }
        }

        private static void Update()
        {
            if (!EditorApplication.isPlaying)
            {
                return;
            }

            if (!isRunning && HasTriggerFile())
            {
                isRunning = true;
                playRequested = true;
                currentRunIndex = 0;
                nextActionAt = 0d;
                nextRunAt = 0d;
                reportAssetPath = $"{ReportDirectory}/{ReportFileName}";
                CompletedRuns.Clear();
                activeRun = null;
                originalConfig = default;
                sessionRng = new System.Random(Environment.TickCount);
                runRng = null;
                ClearReferences();
                Debug.Log("Automated run simulation started from trigger file.");
                SetupSimulationContext();
                StartNextRun();
            }

            if (!isRunning)
            {
                return;
            }

            if (!ResolveReferences())
            {
                return;
            }

            ApplySimulationSpeed();

            double now = EditorApplication.timeSinceStartup;

            if (activeRun == null)
            {
                if (currentRunIndex < TargetRunCount && now >= nextRunAt)
                {
                    StartNextRun();
                }

                return;
            }

            if (waveManager == null || runManager == null || battleGrid == null || mergeController == null || cardSystem == null)
            {
                return;
            }

            if (now - activeRun.EditorStartTime > MaxRunDurationSeconds)
            {
                FinalizeRun("Timed Out", bossCleared: false);
                return;
            }

            TrackRunMetrics();

            if (cardSystem.IsCardPickActive && cardSystem.CurrentChoices.Count > 0 && now >= nextActionAt)
            {
                if (currentPolicy == BotPolicy.Greedy)
                {
                    SelectSmartCard();
                }
                else
                {
                    SelectRandomCard();
                }

                return;
            }

            DraftSystem draft = waveManager.Draft;
            if (draft != null && draft.IsOpen && now >= nextActionAt)
            {
                PickDraft(draft);
                nextActionAt = now + 0.05d;
                return;
            }

            if (waveManager.State == WaveManager.WaveState.Victory)
            {
                FinalizeRun("Victory", bossCleared: true);
                return;
            }

            if (waveManager.State == WaveManager.WaveState.Defeat)
            {
                FinalizeRun("Defeat", bossCleared: false);
                return;
            }

            if (now >= nextActionAt && !cardSystem.IsCardPickActive && currentPolicy != BotPolicy.Passive)
            {
                bool merged = currentPolicy == BotPolicy.Greedy
                    ? TrySmartMerge()
                    : runRng.NextDouble() < 0.4d && TryRandomMerge();
                nextActionAt = now + (merged ? 0.03d : Mathf.Lerp(0.06f, 0.14f, (float)runRng.NextDouble()));
            }
        }

        /// <summary>
        /// Offer tray (design/specs/offer-tray.md): Greedy merges a card into a matching tier-1 troop,
        /// else deploys into an uncovered lane, else (grid full) replaces its weakest troop; Random
        /// plays a random card to a random valid target; Passive only deploys (it never merges).
        /// </summary>
        private static void PickDraft(DraftSystem draft)
        {
            IReadOnlyList<TroopData> offers = draft.Offers;
            if (offers.Count == 0 || mergeController.IsBusy)
            {
                return;
            }

            TroopBuffer.Clear();
            battleGrid.GetTroops(TroopBuffer);
            bool done = false;
            if (currentPolicy == BotPolicy.Greedy)
            {
                for (int i = 0; i < offers.Count && !done; i++)
                {
                    Troop target = TroopBuffer.Find(troop => draft.CanMergeInto(i, troop));
                    if (target != null && draft.TryMerge(i, target))
                    {
                        activeRun.OfferMerges++;
                        done = true;
                    }
                }
            }
            else if (currentPolicy == BotPolicy.Random && runRng.NextDouble() < 0.4d)
            {
                int i = runRng.Next(offers.Count);
                Troop target = TroopBuffer.Find(troop => draft.CanMergeInto(i, troop));
                if (target != null && draft.TryMerge(i, target))
                {
                    activeRun.OfferMerges++;
                    done = true;
                }
            }

            if (!done)
            {
                int i = runRng.Next(offers.Count);
                if (TryPickDeploySlot(offers[i], out int column, out int row))
                {
                    done = draft.TryDeploy(i, column, row);
                    if (done)
                    {
                        activeRun.OfferDeploys++;
                    }
                }
                else if (currentPolicy != BotPolicy.Passive && draft.IsGridFull() && TroopBuffer.Count > 0)
                {
                    Troop weakest = TroopBuffer.OrderBy(troop => troop.Tier).ThenBy(troop => troop.CurrentHP).First();
                    done = draft.TryReplace(i, weakest);
                    if (done)
                    {
                        activeRun.OfferReplaces++;
                    }
                }
            }

            if (done)
            {
                activeRun.DraftsTaken++;
                if (activeWave != null)
                {
                    activeWave.Drafts++;
                }
            }
        }

        private static bool TryPickDeploySlot(TroopData data, out int column, out int row)
        {
            column = -1;
            row = -1;
            List<Vector2Int> empty = new();
            battleGrid.GetEmptySlots(empty);
            if (empty.Count == 0)
            {
                return false;
            }

            if (currentPolicy != BotPolicy.Greedy)
            {
                Vector2Int slot = empty[runRng.Next(empty.Count)];
                column = slot.x;
                row = slot.y;
                return true;
            }

            // Greedy: an empty lane first, front row for melee/contact troops and back row for the rest.
            int preferredRow = data != null && (data.troopType == TroopType.Knight || data.troopType == TroopType.Bomber) ? 0 : 1;
            float bestScore = float.MinValue;
            foreach (Vector2Int slot in empty)
            {
                bool laneCovered = battleGrid.GetTroopAt(slot.x, 0) != null || battleGrid.GetTroopAt(slot.x, 1) != null;
                float score = (laneCovered ? 0f : 10f) + (slot.y == preferredRow ? 1f : 0f) + ((float)runRng.NextDouble() * 0.1f);
                if (score > bestScore)
                {
                    bestScore = score;
                    column = slot.x;
                    row = slot.y;
                }
            }

            return column >= 0;
        }

        private static void SetupSimulationContext()
        {
            ResolveReferences(force: true);
            CaptureOriginalRuntimeSettings();

            if (waveManager != null)
            {
                waveManager.AutoRestartOnRunEnd = false;
            }

            ApplyFastConfig();
        }

        private static void StartNextRun()
        {
            if (!ResolveReferences(force: true) || runManager == null || waveManager == null)
            {
                return;
            }

            if (currentRunIndex >= TargetRunCount)
            {
                FinishAndWriteReport();
                return;
            }

            int policyIndex = Mathf.Clamp(currentRunIndex / runsPerPolicy, 0, Mathf.Max(0, PolicyQueue.Count - 1));
            currentPolicy = PolicyQueue.Count > 0 ? PolicyQueue[policyIndex] : BotPolicy.Greedy;
            currentRunIndex++;
            int seed = sessionRng.Next(1, int.MaxValue);
            runRng = new System.Random(seed);
            UnityEngine.Random.InitState(seed);

            activeWave = null;
            TrackedTroopIds.Clear();
            MergedTroopIds.Clear();
            activeRun = new RunRecord
            {
                Policy = currentPolicy,
                RunNumber = currentRunIndex,
                Seed = seed,
                EditorStartTime = EditorApplication.timeSinceStartup,
                StartingLineup = runManager.GetStartingLineupComposition()
                    .Select(type => type.ToString())
                    .ToList()
            };

            nextActionAt = EditorApplication.timeSinceStartup + 0.02d;
            runManager.StartRun();
        }

        private static void SelectSmartCard()
        {
            IReadOnlyList<CardData> choices = cardSystem.CurrentChoices;
            if (choices == null || choices.Count == 0)
            {
                nextActionAt = EditorApplication.timeSinceStartup + 0.1d;
                return;
            }

            int pickIndex = 0;
            float bestScore = float.MinValue;
            for (int i = 0; i < choices.Count; i++)
            {
                float score = ScoreCardChoice(choices[i]);
                if (score > bestScore)
                {
                    bestScore = score;
                    pickIndex = i;
                }
            }

            CardData chosen = choices[pickIndex];
            activeRun.CardPicks.Add(new CardPickRecord
            {
                ClearedWave = Mathf.Max(1, waveManager.CurrentWave),
                Options = choices.Select(FormatCard).ToList(),
                Chosen = $"{FormatCard(chosen)} [score {bestScore:0.0}]"
            });

            cardSystem.OnCardSelected(pickIndex);
            nextActionAt = EditorApplication.timeSinceStartup + 0.2d;
        }

        private static bool TrySmartMerge()
        {
            TroopBuffer.Clear();
            battleGrid.GetTroops(TroopBuffer);

            if (TroopBuffer.Count < 2)
            {
                return false;
            }

            MergeBuffer.Clear();
            for (int i = 0; i < TroopBuffer.Count; i++)
            {
                for (int j = i + 1; j < TroopBuffer.Count; j++)
                {
                    Troop first = TroopBuffer[i];
                    Troop second = TroopBuffer[j];
                    if (first != null && second != null && first.CanMergeWith(second))
                    {
                        MergeBuffer.Add(new MergePair(first, second));
                    }
                }
            }

            if (MergeBuffer.Count == 0)
            {
                return false;
            }

            MergePair pair = default;
            float bestScore = float.MinValue;
            for (int i = 0; i < MergeBuffer.Count; i++)
            {
                float score = ScoreMerge(MergeBuffer[i]);
                if (score > bestScore)
                {
                    bestScore = score;
                    pair = MergeBuffer[i];
                }
            }

            bool boosted = CardSystem.Instance != null && CardSystem.Instance.runBuffs.nextMergeBoosted;
            int resultTier = Mathf.Clamp(pair.Target.Tier + 1 + (boosted && pair.Target.Tier < 3 ? 1 : 0), 1, 3);

            int sourceId = pair.Source.GetInstanceID();
            if (!mergeController.TrySimulateMerge(pair.Source, pair.Target))
            {
                return false;
            }

            MergedTroopIds.Add(sourceId);
            activeRun.Merges.Add(new MergeRecord
            {
                Wave = Mathf.Max(1, waveManager.CurrentWave),
                Source = $"{pair.Source.Data.displayName} T{pair.Source.Tier}",
                Target = $"{pair.Target.Data.displayName} T{pair.Target.Tier}",
                Result = $"{pair.Target.Data.displayName} T{resultTier}",
                Boosted = boosted,
                Score = bestScore
            });

            return true;
        }

        private static void FinalizeRun(string outcome, bool bossCleared)
        {
            CloseWave(Time.time);
            activeRun.CombatSeconds = activeRun.Waves.Sum(wave => wave.Duration);
            WaveStat bossWave = activeRun.Waves.LastOrDefault(wave => wave.IsBoss);
            activeRun.BossSeconds = bossCleared && bossWave != null ? bossWave.Duration : -1f;
            activeRun.Outcome = outcome;
            activeRun.LossCause = bossCleared ? "-" : waveManager != null && waveManager.Base.IsDestroyed ? "Egg" : "Wipe";
            activeRun.TroopsAliveAtEnd = GetTroopCount();
            activeRun.EggHpAtEnd = waveManager != null ? waveManager.Base.CurrentHP : 0f;
            activeRun.WaveReached = waveManager != null ? waveManager.CurrentWave : 0;
            activeRun.BossCleared = bossCleared;
            activeRun.TotalCoins = waveManager != null ? waveManager.TotalCoins : 0;
            activeRun.TotalMerges = waveManager != null ? waveManager.MergeCount : activeRun.Merges.Count;
            activeRun.FinalBuffSummary = cardSystem != null ? cardSystem.GetActiveBuffSummary() : "Unavailable";

            CompletedRuns.Add(activeRun);
            activeRun = null;
            nextRunAt = EditorApplication.timeSinceStartup + 0.4d;

            if (currentRunIndex >= TargetRunCount)
            {
                FinishAndWriteReport();
            }
        }

        private static void FinishAndWriteReport()
        {
            RestoreRuntimeSettings();
            string path = WriteBalanceReport();
            Debug.Log($"Balance harness report written to {path}");
            DeleteTriggerFile();
            EditorApplication.isPlaying = false;
        }

        private static bool ResolveReferences(bool force = false)
        {
            if (force || runManager == null)
            {
                runManager = UnityEngine.Object.FindFirstObjectByType<RunManager>();
            }

            if (force || waveManager == null)
            {
                waveManager = UnityEngine.Object.FindFirstObjectByType<WaveManager>();
            }

            if (force || battleGrid == null)
            {
                battleGrid = UnityEngine.Object.FindFirstObjectByType<BattleGrid>();
            }

            if (force || mergeController == null)
            {
                mergeController = UnityEngine.Object.FindFirstObjectByType<MergeController>();
            }

            if (force || cardSystem == null)
            {
                cardSystem = UnityEngine.Object.FindFirstObjectByType<CardSystem>();
            }

            if ((force || gameConfig == null) && runManager != null)
            {
                gameConfig = runManager.Config;
            }

            return runManager != null && waveManager != null && battleGrid != null && mergeController != null && cardSystem != null && gameConfig != null;
        }

        private static void ClearReferences()
        {
            runManager = null;
            waveManager = null;
            battleGrid = null;
            mergeController = null;
            cardSystem = null;
            gameConfig = null;
        }

        private static void CaptureOriginalRuntimeSettings()
        {
            if (gameConfig == null || originalConfig.IsCaptured)
            {
                return;
            }

            originalConfig = new RuntimeConfigSnapshot(gameConfig);
        }

        private static void ApplyFastConfig()
        {
            if (gameConfig == null)
            {
                return;
            }

            gameConfig.startWaveDelay = 0f;
            gameConfig.timeBetweenWaves = 0.05f;
            gameConfig.waveClearedBannerDuration = 0.05f;
            gameConfig.runEndRestartDelay = 0.1f;
            gameConfig.enemySpawnIntervalMin = 0.05f;
            gameConfig.enemySpawnIntervalMax = 0.08f;
            gameConfig.mergeSlideDuration = 0.03f;
            gameConfig.mergeFlashDuration = 0.03f;
            gameConfig.mergePopDuration = 0.05f;
            gameConfig.dragReturnDuration = 0.02f;
            gameConfig.swapMoveDuration = 0.02f;
            gameConfig.tacticalSlowTimeScale = 1f;
        }

        private static void ApplySimulationSpeed()
        {
            if (Mathf.Abs(Time.timeScale - SimulationTimeScale) > 0.001f)
            {
                Time.timeScale = SimulationTimeScale;
                Time.fixedDeltaTime = 0.02f * Time.timeScale;
            }
        }

        private static void RestoreRuntimeSettings()
        {
            if (originalConfig.IsCaptured && gameConfig != null)
            {
                originalConfig.Restore(gameConfig);
            }

            if (waveManager != null)
            {
                waveManager.AutoRestartOnRunEnd = true;
            }

            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
        }

        private static void WriteReportFile()
        {
            string absoluteDirectory = Path.Combine(Directory.GetCurrentDirectory(), ReportDirectory.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(absoluteDirectory);

            StringBuilder builder = new();
            builder.AppendLine("# Merge-Focused Automated Run Simulation Report");
            builder.AppendLine();
            builder.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            builder.AppendLine($"Runs Simulated: {CompletedRuns.Count}");
            builder.AppendLine();
            builder.AppendLine("Heuristics: merge-first behavior, immediate merge attempts, cards that improve board strength or create merge chains, and highest-value merges by resulting tier/troop utility.");
            builder.AppendLine();

            int victories = CompletedRuns.Count(run => run.BossCleared);
            float averageWave = CompletedRuns.Count > 0 ? (float)CompletedRuns.Average(run => run.WaveReached) : 0f;
            builder.AppendLine("## Summary");
            builder.AppendLine();
            builder.AppendLine($"- Boss clears: {victories}/{CompletedRuns.Count}");
            builder.AppendLine($"- Average wave reached: {averageWave:0.0}");
            builder.AppendLine($"- Highest wave reached: {(CompletedRuns.Count > 0 ? CompletedRuns.Max(run => run.WaveReached) : 0)}");
            builder.AppendLine($"- Average merges per run: {(CompletedRuns.Count > 0 ? CompletedRuns.Average(run => run.TotalMerges) : 0f):0.0}");
            builder.AppendLine();

            foreach (RunRecord run in CompletedRuns)
            {
                builder.AppendLine($"## Run {run.RunNumber}");
                builder.AppendLine();
                builder.AppendLine($"- Seed: `{run.Seed}`");
                builder.AppendLine($"- Starting lineup: {string.Join(", ", run.StartingLineup)}");
                builder.AppendLine($"- Outcome: {run.Outcome}");
                builder.AppendLine($"- Wave reached: {run.WaveReached}");
                builder.AppendLine($"- Boss cleared: {(run.BossCleared ? "Yes" : "No")}");
                builder.AppendLine($"- Total coins: {run.TotalCoins}");
                builder.AppendLine($"- Total merges: {run.TotalMerges}");
                builder.AppendLine();

                builder.AppendLine("### Card Picks");
                builder.AppendLine();
                if (run.CardPicks.Count == 0)
                {
                    builder.AppendLine("- None");
                }
                else
                {
                    foreach (CardPickRecord pick in run.CardPicks)
                    {
                        builder.AppendLine($"- After Wave {pick.ClearedWave}: chose `{pick.Chosen}` from [{string.Join(" | ", pick.Options)}]");
                    }
                }

                builder.AppendLine();
                builder.AppendLine("### Merges");
                builder.AppendLine();
                if (run.Merges.Count == 0)
                {
                    builder.AppendLine("- None");
                }
                else
                {
                    foreach (MergeRecord merge in run.Merges)
                    {
                        string boostedSuffix = merge.Boosted ? " (boosted)" : string.Empty;
                        builder.AppendLine($"- Wave {merge.Wave}: `{merge.Source}` into `{merge.Target}` -> `{merge.Result}`{boostedSuffix} [score {merge.Score:0.0}]");
                    }
                }

                builder.AppendLine();
                builder.AppendLine("### Final Buffs");
                builder.AppendLine();
                builder.AppendLine("```text");
                builder.AppendLine(run.FinalBuffSummary ?? "Unavailable");
                builder.AppendLine("```");
                builder.AppendLine();
            }

            string absolutePath = Path.Combine(absoluteDirectory, ReportFileName);
            File.WriteAllText(absolutePath, builder.ToString());
            AssetDatabase.Refresh();
        }

        private static void SelectRandomCard()
        {
            IReadOnlyList<CardData> choices = cardSystem.CurrentChoices;
            int pickIndex = runRng.Next(0, choices.Count);
            activeRun.CardPicks.Add(new CardPickRecord
            {
                ClearedWave = Mathf.Max(1, waveManager.CurrentWave),
                Options = choices.Select(FormatCard).ToList(),
                Chosen = $"{FormatCard(choices[pickIndex])} [random]"
            });

            cardSystem.OnCardSelected(pickIndex);
            nextActionAt = EditorApplication.timeSinceStartup + 0.2d;
        }

        /// <summary>A casual player: merges any available pair, not the best one.</summary>
        private static bool TryRandomMerge()
        {
            TroopBuffer.Clear();
            battleGrid.GetTroops(TroopBuffer);
            MergeBuffer.Clear();
            for (int i = 0; i < TroopBuffer.Count; i++)
            {
                for (int j = i + 1; j < TroopBuffer.Count; j++)
                {
                    if (TroopBuffer[i] != null && TroopBuffer[j] != null && TroopBuffer[i].CanMergeWith(TroopBuffer[j]))
                    {
                        MergeBuffer.Add(new MergePair(TroopBuffer[i], TroopBuffer[j]));
                    }
                }
            }

            if (MergeBuffer.Count == 0)
            {
                return false;
            }

            MergePair pair = MergeBuffer[runRng.Next(0, MergeBuffer.Count)];
            int resultTier = Mathf.Clamp(pair.Target.Tier + 1, 1, 3);
            int sourceId = pair.Source.GetInstanceID();
            if (!mergeController.TrySimulateMerge(pair.Source, pair.Target))
            {
                return false;
            }

            MergedTroopIds.Add(sourceId);
            activeRun.Merges.Add(new MergeRecord
            {
                Wave = Mathf.Max(1, waveManager.CurrentWave),
                Source = $"{pair.Source.Data.displayName} T{pair.Source.Tier}",
                Target = $"{pair.Target.Data.displayName} T{pair.Target.Tier}",
                Result = $"{pair.Target.Data.displayName} T{resultTier}",
                Score = 0f
            });
            return true;
        }

        /// <summary>Samples board and enemy state every editor frame for the per-wave metrics.</summary>
        private static void TrackRunMetrics()
        {
            if (activeRun == null)
            {
                return;
            }

            float now = Time.time;
            WaveManager.WaveState state = waveManager.State;
            bool inCombat = state == WaveManager.WaveState.Combat || state == WaveManager.WaveState.BossWave;
            int wave = waveManager.CurrentWave;
            if (inCombat && (activeWave == null || activeWave.Wave != wave))
            {
                CloseWave(now);
                activeWave = new WaveStat
                {
                    Wave = wave,
                    IsBoss = state == WaveManager.WaveState.BossWave,
                    StartTime = now,
                    Closest = float.MaxValue,
                    BaseHpAtStart = waveManager.Base.CurrentHP
                };
            }
            else if (!inCombat && activeWave != null)
            {
                CloseWave(now);
            }

            TroopBuffer.Clear();
            battleGrid.GetTroops(TroopBuffer);
            FrameTroopIds.Clear();
            int maxTier = 0;
            for (int i = 0; i < TroopBuffer.Count; i++)
            {
                Troop troop = TroopBuffer[i];
                if (troop != null && troop.IsAlive)
                {
                    FrameTroopIds.Add(troop.GetInstanceID());
                    maxTier = Mathf.Max(maxTier, troop.Tier);
                }
            }

            foreach (int id in TrackedTroopIds)
            {
                if (!FrameTroopIds.Contains(id) && !MergedTroopIds.Contains(id))
                {
                    activeRun.TroopsLost++;
                    if (activeWave != null)
                    {
                        activeWave.TroopsLost++;
                    }
                }
            }

            TrackedTroopIds.Clear();
            TrackedTroopIds.UnionWith(FrameTroopIds);

            if (maxTier >= 2 && activeRun.FirstTierTwoWave == 0)
            {
                activeRun.FirstTierTwoWave = Mathf.Max(1, wave);
            }

            if (maxTier >= 3 && activeRun.FirstTierThreeWave == 0)
            {
                activeRun.FirstTierThreeWave = Mathf.Max(1, wave);
            }

            if (activeWave == null)
            {
                return;
            }

            float gridTop = battleGrid.GetGridWorldBounds().max.y;
            int alive = 0;
            IReadOnlyList<Enemy> enemies = Enemy.ActiveEnemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                Enemy enemy = enemies[i];
                if (enemy == null || !enemy.IsAlive)
                {
                    continue;
                }

                alive++;
                activeWave.Closest = Mathf.Min(activeWave.Closest, enemy.transform.position.y - gridTop);
            }

            activeWave.PeakAlive = Mathf.Max(activeWave.PeakAlive, alive);
        }

        private static void CloseWave(float now)
        {
            if (activeWave == null || activeRun == null)
            {
                return;
            }

            activeWave.Duration = Mathf.Max(0f, now - activeWave.StartTime);
            activeWave.BaseDamage = Mathf.Max(0f, activeWave.BaseHpAtStart - waveManager.Base.CurrentHP);
            activeRun.Waves.Add(activeWave);
            activeWave = null;
        }

        private static string WriteBalanceReport()
        {
            string absoluteDirectory = Path.Combine(Directory.GetCurrentDirectory(), BalanceReportDirectory.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(absoluteDirectory);
            string fileName = $"balance-{DateTime.Now:yyyyMMdd-HHmmss}.md";
            StringBuilder b = new();
            b.AppendLine("# Balance Harness Report");
            b.AppendLine();
            b.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}. Runs: {CompletedRuns.Count} ({runsPerPolicy} per policy). Contact = an enemy came within {ContactDistance} units of the grid's top edge.");
            b.AppendLine();
            AppendConfigSnapshot(b);

            b.AppendLine("## Summary");
            b.AppendLine();
            b.AppendLine("| Policy | Runs | Win % | Median death wave | Avg merges | Offers | Deploys | Card merges | Replaces | Egg deaths | Wipes | Troops alive at egg death | First T2 | First T3 | Troops lost | Combat s | Est. run s | Boss TTK s |");
            b.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
            float overheadPerWave = originalConfig.IsCaptured ? originalConfig.TimeBetweenWaves + originalConfig.WaveClearedBannerDuration : 2.5f;
            foreach (BotPolicy policy in CompletedRuns.Select(run => run.Policy).Distinct())
            {
                List<RunRecord> runs = CompletedRuns.Where(run => run.Policy == policy).ToList();
                List<RunRecord> losses = runs.Where(run => !run.BossCleared).ToList();
                List<RunRecord> wins = runs.Where(run => run.BossCleared).ToList();
                string deathWave = losses.Count > 0 ? Median(losses.Select(run => (float)run.WaveReached)).ToString("0") : "-";
                float estRun = (float)runs.Average(run => run.CombatSeconds + (Mathf.Max(0, run.Waves.Count - 1) * overheadPerWave) + (run.CardPicks.Count * HumanCardPickSeconds));
                string bossTtk = wins.Count > 0 ? wins.Average(run => run.BossSeconds).ToString("0.0") : "-";
                b.AppendLine($"| {policy} | {runs.Count} | {100f * wins.Count / runs.Count:0} | {deathWave} | {runs.Average(run => run.TotalMerges):0.0} | {runs.Average(run => run.DraftsTaken):0.0} | {runs.Average(run => run.OfferDeploys):0.0} | {runs.Average(run => run.OfferMerges):0.0} | {runs.Average(run => run.OfferReplaces):0.0} | {runs.Count(run => run.LossCause == "Egg")} | {runs.Count(run => run.LossCause == "Wipe")} | {(runs.Any(run => run.LossCause == "Egg") ? runs.Where(run => run.LossCause == "Egg").Average(run => run.TroopsAliveAtEnd).ToString("0.0") : "-")} | {AverageWave(runs, run => run.FirstTierTwoWave)} | {AverageWave(runs, run => run.FirstTierThreeWave)} | {runs.Average(run => run.TroopsLost):0.0} | {runs.Average(run => run.CombatSeconds):0} | {estRun:0} | {bossTtk} |");
            }

            b.AppendLine();
            foreach (BotPolicy policy in CompletedRuns.Select(run => run.Policy).Distinct())
            {
                List<RunRecord> runs = CompletedRuns.Where(run => run.Policy == policy).ToList();
                b.AppendLine($"## Per-wave: {policy}");
                b.AppendLine();
                b.AppendLine("| Wave | Runs reaching | Avg duration s | Avg peak alive | Avg closest (units) | Contact % | Avg troops lost | Avg base dmg | Avg drafts |");
                b.AppendLine("|---|---|---|---|---|---|---|---|---|");
                int maxWave = runs.SelectMany(run => run.Waves).Select(wave => wave.Wave).DefaultIfEmpty(0).Max();
                for (int w = 1; w <= maxWave; w++)
                {
                    List<WaveStat> stats = runs.SelectMany(run => run.Waves).Where(wave => wave.Wave == w).ToList();
                    if (stats.Count == 0)
                    {
                        continue;
                    }

                    List<WaveStat> withEnemies = stats.Where(wave => wave.Closest < float.MaxValue).ToList();
                    string closest = withEnemies.Count > 0 ? withEnemies.Average(wave => wave.Closest).ToString("0.00") : "-";
                    float contact = withEnemies.Count > 0 ? 100f * withEnemies.Count(wave => wave.Closest <= ContactDistance) / withEnemies.Count : 0f;
                    b.AppendLine($"| {w} | {stats.Count} | {stats.Average(wave => wave.Duration):0.0} | {stats.Average(wave => wave.PeakAlive):0.0} | {closest} | {contact:0} | {stats.Average(wave => wave.TroopsLost):0.0} | {stats.Average(wave => wave.BaseDamage):0.0} | {stats.Average(wave => wave.Drafts):0.0} |");
                }

                b.AppendLine();
            }

            b.AppendLine("## Runs");
            b.AppendLine();
            foreach (RunRecord run in CompletedRuns)
            {
                string cards = string.Join(", ", run.CardPicks.Select(pick => pick.Chosen.Split(' ')[0]));
                b.AppendLine($"- {run.Policy} #{run.RunNumber}: {run.Outcome} ({run.LossCause}, {run.TroopsAliveAtEnd} troops left, egg {run.EggHpAtEnd:0}) at wave {run.WaveReached}, base dmg {run.Waves.Sum(wave => wave.BaseDamage):0}, merges {run.TotalMerges}, lost {run.TroopsLost}, combat {run.CombatSeconds:0}s, cards [{cards}]");
            }

            string absolutePath = Path.Combine(absoluteDirectory, fileName);
            File.WriteAllText(absolutePath, b.ToString());
            return $"{BalanceReportDirectory}/{fileName}";
        }

        private static void AppendConfigSnapshot(StringBuilder b)
        {
            EnemySpawner spawner = UnityEngine.Object.FindFirstObjectByType<EnemySpawner>();
            b.AppendLine("## Config");
            b.AppendLine();
            if (gameConfig != null)
            {
                b.AppendLine($"- Tier stats x{string.Join(" / x", gameConfig.tierStatMultipliers)}; attack interval x{string.Join(" / x", gameConfig.tierAttackIntervalMultipliers)}");
                b.AppendLine($"- Archer range {gameConfig.archerRange}; spawn Y offset {gameConfig.enemySpawnYOffset}; contact overlap {gameConfig.enemyContactOverlap}");
                b.AppendLine($"- Starting lineup: {string.Join(", ", gameConfig.startingLineup)}");
            }

            if (spawner != null)
            {
                WaveConfig waves = spawner.Waves;
                b.AppendLine($"- Enemy HP x{waves.hpBase} * {waves.hpGrowth}^(w-1) (w1 x{waves.HpMultiplier(1):0.00}, w15 x{waves.HpMultiplier(15):0.00}); attack +{waves.attackGrowthPerWave * 100f:0}%/wave");
                b.AppendLine($"- Spawn interval {waves.SpawnInterval(1):0.00}s -> {waves.SpawnInterval(15):0.00}s; boss wave {waves.bossWave}, HP x{waves.bossHpMultiplier}, ATK x{waves.bossAttackMultiplier}");
            }

            b.AppendLine();
        }

        private static string AverageWave(List<RunRecord> runs, Func<RunRecord, int> selector)
        {
            List<int> values = runs.Select(selector).Where(value => value > 0).ToList();
            return values.Count > 0 ? values.Average().ToString("0.0") : "-";
        }

        private static float Median(IEnumerable<float> values)
        {
            List<float> sorted = values.OrderBy(value => value).ToList();
            if (sorted.Count == 0)
            {
                return 0f;
            }

            int mid = sorted.Count / 2;
            return sorted.Count % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) * 0.5f;
        }

        private static string FormatCard(CardData card)
        {
            if (card == null)
            {
                return "Unknown Card";
            }

            return $"{card.cardName} ({card.rarity}, {card.category})";
        }

        private static float ScoreCardChoice(CardData card)
        {
            if (card == null)
            {
                return float.MinValue;
            }

            int wave = Mathf.Max(1, waveManager != null ? waveManager.CurrentWave : 1);
            int troopCount = GetTroopCount();
            int emptySlots = GetEmptySlotCount();
            CountTroopTypes(out int knights, out int archers, out int mages, out int healers, out int bombers);
            int availablePairs = CountAvailableMergePairs();

            float score = card.rarity switch
            {
                CardRarity.Rare => 2.5f,
                CardRarity.Epic => 4f,
                _ => 0f
            };

            switch (card.effectType)
            {
                case CardEffectType.AttackBoostAll:
                    score += 16f + troopCount * 1.4f;
                    break;
                case CardEffectType.HPBoostAll:
                    score += 18f + troopCount * 1.6f;
                    break;
                case CardEffectType.AttackBoostKnights:
                    score += 6f + knights * 6f;
                    break;
                case CardEffectType.AttackBoostArchers:
                    score += 6f + archers * 6f;
                    break;
                case CardEffectType.AttackBoostMages:
                    score += 6f + mages * 7f;
                    break;
                case CardEffectType.HealBoostHealers:
                case CardEffectType.HealerBoost:
                    score += healers > 0 ? 8f + healers * 7f : 1f;
                    break;
                case CardEffectType.AttackBoostBombers:
                    score += bombers > 0 ? 7f + bombers * 6f : 0.5f;
                    break;
                case CardEffectType.SpawnRandomTroop:
                    score += emptySlots > 0 ? 18f : -10f;
                    break;
                case CardEffectType.SpawnKnight:
                    score += emptySlots > 0 ? 12f + ScoreSpawnForMerge(knights) : -10f;
                    break;
                case CardEffectType.SpawnArcher:
                    score += emptySlots > 0 ? 12f + ScoreSpawnForMerge(archers) : -10f;
                    break;
                case CardEffectType.SpawnMage:
                    score += emptySlots > 0 ? 11f + ScoreSpawnForMerge(mages) : -10f;
                    break;
                case CardEffectType.SpawnHealer:
                    score += emptySlots > 0 ? 9f + ScoreSpawnForMerge(healers) : -10f;
                    break;
                case CardEffectType.SpawnBomber:
                    score += emptySlots > 0 ? 7f + ScoreSpawnForMerge(bombers) : -10f;
                    break;
                case CardEffectType.BomberRadius:
                    score += bombers > 0 ? 8f : 1f;
                    break;
                case CardEffectType.MergeBoostNextTier:
                    score += availablePairs > 0 ? 28f + availablePairs * 3f : 6f;
                    break;
                case CardEffectType.HealAllTroops:
                    score += 10f + EstimateMissingHealthRatio() * 20f;
                    break;
                case CardEffectType.ReviveOneTroop:
                    score += emptySlots > 0 ? 13f : 5f;
                    break;
                case CardEffectType.ArcherSpeedBoost:
                    score += archers > 0 ? 8f + archers * 5f : 1f;
                    break;
                case CardEffectType.KnightThorns:
                    score += knights > 0 ? 8f + knights * 4f : 1f;
                    break;
                case CardEffectType.DoubleCoins:
                    score += wave <= 9 ? 14f : 6f;
                    break;
                case CardEffectType.ExtraDeploy:
                    score += emptySlots > 0 ? 20f + Mathf.Max(0, 3 - availablePairs) * 2f : 2f;
                    break;
                case CardEffectType.MergeHeal:
                    score += 10f + troopCount * 0.8f + availablePairs * 2f;
                    break;
                case CardEffectType.ArcherDoubleDamageVsTank:
                    score += archers > 0 && wave >= 3 ? 13f + archers * 4f : 4f;
                    break;
                case CardEffectType.KnightDamageFlyers:
                    score += knights > 0 && wave >= 5 ? 13f + knights * 4f : 3f;
                    break;
            }

            if (troopCount <= 3)
            {
                score += card.category == CardCategory.Spawn ? 4f : 0f;
            }

            if (availablePairs == 0 && card.category == CardCategory.MergeBoost)
            {
                score -= 8f;
            }

            if (wave <= 3 && card.category == CardCategory.Spawn)
            {
                score += 3f;
            }

            return score + (float)runRng.NextDouble() * 0.05f;
        }

        private static float ScoreMerge(MergePair pair)
        {
            if (pair.Source == null || pair.Target == null || pair.Target.Data == null)
            {
                return float.MinValue;
            }

            bool boosted = CardSystem.Instance != null && CardSystem.Instance.runBuffs.nextMergeBoosted;
            int resultTier = Mathf.Clamp(pair.Target.Tier + 1 + (boosted && pair.Target.Tier < 3 ? 1 : 0), 1, 3);
            float score = resultTier * 20f;
            score += pair.Target.Data.troopType switch
            {
                TroopType.Archer => 10f,
                TroopType.Knight => 9f,
                TroopType.Mage => 11f,
                TroopType.Healer => 7f,
                TroopType.Bomber => 5f,
                _ => 0f
            };

            float hpRatio = pair.Target.MaxHP > 0.01f ? pair.Target.CurrentHP / pair.Target.MaxHP : 1f;
            score += (1f - hpRatio) * 3f;

            if (boosted)
            {
                score += 12f;
            }

            if (resultTier >= 3)
            {
                score += 8f;
            }

            return score + (float)runRng.NextDouble() * 0.05f;
        }

        private static int GetTroopCount()
        {
            TroopBuffer.Clear();
            battleGrid.GetTroops(TroopBuffer);
            return TroopBuffer.Count;
        }

        private static int GetEmptySlotCount()
        {
            int occupied = GetTroopCount();
            return gameConfig != null ? Mathf.Max(0, (gameConfig.columns * gameConfig.rows) - occupied) : 0;
        }

        private static int CountAvailableMergePairs()
        {
            TroopBuffer.Clear();
            battleGrid.GetTroops(TroopBuffer);
            int pairs = 0;
            for (int i = 0; i < TroopBuffer.Count; i++)
            {
                for (int j = i + 1; j < TroopBuffer.Count; j++)
                {
                    if (TroopBuffer[i] != null && TroopBuffer[j] != null && TroopBuffer[i].CanMergeWith(TroopBuffer[j]))
                    {
                        pairs++;
                    }
                }
            }

            return pairs;
        }

        private static bool HasAnyMergeAvailable()
        {
            return CountAvailableMergePairs() > 0;
        }

        private static float EstimateMissingHealthRatio()
        {
            TroopBuffer.Clear();
            battleGrid.GetTroops(TroopBuffer);
            if (TroopBuffer.Count == 0)
            {
                return 0f;
            }

            float totalRatio = 0f;
            for (int i = 0; i < TroopBuffer.Count; i++)
            {
                Troop troop = TroopBuffer[i];
                if (troop == null || troop.MaxHP <= 0.01f)
                {
                    continue;
                }

                totalRatio += 1f - Mathf.Clamp01(troop.CurrentHP / troop.MaxHP);
            }

            return totalRatio / Mathf.Max(1, TroopBuffer.Count);
        }

        private static void CountTroopTypes(out int knights, out int archers, out int mages, out int healers, out int bombers)
        {
            knights = 0;
            archers = 0;
            mages = 0;
            healers = 0;
            bombers = 0;

            TroopBuffer.Clear();
            battleGrid.GetTroops(TroopBuffer);
            for (int i = 0; i < TroopBuffer.Count; i++)
            {
                Troop troop = TroopBuffer[i];
                if (troop == null || troop.Data == null)
                {
                    continue;
                }

                switch (troop.Data.troopType)
                {
                    case TroopType.Knight:
                        knights++;
                        break;
                    case TroopType.Archer:
                        archers++;
                        break;
                    case TroopType.Mage:
                        mages++;
                        break;
                    case TroopType.Healer:
                        healers++;
                        break;
                    case TroopType.Bomber:
                        bombers++;
                        break;
                }
            }
        }

        private static float ScoreSpawnForMerge(int troopCountOfType)
        {
            if (troopCountOfType <= 0)
            {
                return 2f;
            }

            return troopCountOfType % 2 == 1 ? 8f : 4f;
        }

        private static bool HasTriggerFile()
        {
            string absolutePath = Path.Combine(Directory.GetCurrentDirectory(), ReportDirectory.Replace('/', Path.DirectorySeparatorChar), TriggerFileName);
            return File.Exists(absolutePath);
        }

        private static void DeleteTriggerFile()
        {
            string absolutePath = Path.Combine(Directory.GetCurrentDirectory(), ReportDirectory.Replace('/', Path.DirectorySeparatorChar), TriggerFileName);
            if (File.Exists(absolutePath))
            {
                File.Delete(absolutePath);
            }
        }

        private readonly struct MergePair
        {
            public MergePair(Troop source, Troop target)
            {
                Source = source;
                Target = target;
            }

            public Troop Source { get; }
            public Troop Target { get; }
        }

        private struct RuntimeConfigSnapshot
        {
            public RuntimeConfigSnapshot(GameConfig config)
            {
                IsCaptured = config != null;
                StartWaveDelay = config != null ? config.startWaveDelay : 0f;
                TimeBetweenWaves = config != null ? config.timeBetweenWaves : 0f;
                WaveClearedBannerDuration = config != null ? config.waveClearedBannerDuration : 0f;
                RunEndRestartDelay = config != null ? config.runEndRestartDelay : 0f;
                EnemySpawnIntervalMin = config != null ? config.enemySpawnIntervalMin : 0f;
                EnemySpawnIntervalMax = config != null ? config.enemySpawnIntervalMax : 0f;
                MergeSlideDuration = config != null ? config.mergeSlideDuration : 0f;
                MergeFlashDuration = config != null ? config.mergeFlashDuration : 0f;
                MergePopDuration = config != null ? config.mergePopDuration : 0f;
                DragReturnDuration = config != null ? config.dragReturnDuration : 0f;
                SwapMoveDuration = config != null ? config.swapMoveDuration : 0f;
                TacticalSlowTimeScale = config != null ? config.tacticalSlowTimeScale : 1f;
            }

            public bool IsCaptured { get; }
            public float StartWaveDelay { get; }
            public float TimeBetweenWaves { get; }
            public float WaveClearedBannerDuration { get; }
            public float RunEndRestartDelay { get; }
            public float EnemySpawnIntervalMin { get; }
            public float EnemySpawnIntervalMax { get; }
            public float MergeSlideDuration { get; }
            public float MergeFlashDuration { get; }
            public float MergePopDuration { get; }
            public float DragReturnDuration { get; }
            public float SwapMoveDuration { get; }
            public float TacticalSlowTimeScale { get; }

            public void Restore(GameConfig config)
            {
                if (!IsCaptured || config == null)
                {
                    return;
                }

                config.startWaveDelay = StartWaveDelay;
                config.timeBetweenWaves = TimeBetweenWaves;
                config.waveClearedBannerDuration = WaveClearedBannerDuration;
                config.runEndRestartDelay = RunEndRestartDelay;
                config.enemySpawnIntervalMin = EnemySpawnIntervalMin;
                config.enemySpawnIntervalMax = EnemySpawnIntervalMax;
                config.mergeSlideDuration = MergeSlideDuration;
                config.mergeFlashDuration = MergeFlashDuration;
                config.mergePopDuration = MergePopDuration;
                config.dragReturnDuration = DragReturnDuration;
                config.swapMoveDuration = SwapMoveDuration;
                config.tacticalSlowTimeScale = TacticalSlowTimeScale;
            }
        }

        private sealed class WaveStat
        {
            public int Wave;
            public bool IsBoss;
            public float StartTime;
            public float Duration;
            public int PeakAlive;
            public float Closest;
            public int TroopsLost;
            public float BaseHpAtStart;
            public float BaseDamage;
            public int Drafts;
        }

        private sealed class RunRecord
        {
            public BotPolicy Policy;
            public List<WaveStat> Waves = new();
            public int TroopsLost;
            public int FirstTierTwoWave;
            public int FirstTierThreeWave;
            public float CombatSeconds;
            public float BossSeconds;
            public int RunNumber;
            public int Seed;
            public double EditorStartTime;
            public List<string> StartingLineup = new();
            public List<CardPickRecord> CardPicks = new();
            public List<MergeRecord> Merges = new();
            public string Outcome;
            public int WaveReached;
            public bool BossCleared;
            public int TotalCoins;
            public int TotalMerges;
            public int DraftsTaken;
            public int OfferDeploys;
            public int OfferMerges;
            public int OfferReplaces;
            public string LossCause;
            public int TroopsAliveAtEnd;
            public float EggHpAtEnd;
            public string FinalBuffSummary;
        }

        private sealed class CardPickRecord
        {
            public int ClearedWave;
            public List<string> Options = new();
            public string Chosen;
        }

        private sealed class MergeRecord
        {
            public int Wave;
            public string Source;
            public string Target;
            public string Result;
            public bool Boosted;
            public float Score;
        }
    }
}
