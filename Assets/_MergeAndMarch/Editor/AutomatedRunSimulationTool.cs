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
    [InitializeOnLoad]
    public static class AutomatedRunSimulationTool
    {
        private const string MenuPath = "MergeAndMarch/Generate Merge-Focused Run Report";
        private const int TargetRunCount = 5;
        private const float SimulationTimeScale = 14f;
        private const double MaxRunDurationSeconds = 150d;
        private const string ReportDirectory = "Assets/_MergeAndMarch/TestReports";
        private const string ReportFileName = "AutomatedRunReport_MergeSmart.md";
        private const string TriggerFileName = "AutomatedRunReport_MergeSmart.trigger";

        private static readonly List<Troop> TroopBuffer = new();
        private static readonly List<MergePair> MergeBuffer = new();
        private static readonly List<RunRecord> CompletedRuns = new();

        private static bool isRunning;
        private static bool playRequested;
        private static int currentRunIndex;
        private static double nextActionAt;
        private static double nextRunAt;
        private static string reportAssetPath;
        private static System.Random sessionRng;
        private static System.Random runRng;
        private static RunRecord activeRun;

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

            if (cardSystem.IsCardPickActive && cardSystem.CurrentChoices.Count > 0 && now >= nextActionAt)
            {
                SelectSmartCard();
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

            if (now >= nextActionAt && !cardSystem.IsCardPickActive)
            {
                bool merged = TrySmartMerge();
                nextActionAt = now + (merged ? 0.03d : Mathf.Lerp(0.06f, 0.14f, (float)runRng.NextDouble()));
            }
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

            currentRunIndex++;
            int seed = sessionRng.Next(1, int.MaxValue);
            runRng = new System.Random(seed);
            UnityEngine.Random.InitState(seed);

            activeRun = new RunRecord
            {
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

            if (!mergeController.TrySimulateMerge(pair.Source, pair.Target))
            {
                return false;
            }

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
            activeRun.Outcome = outcome;
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
            WriteReportFile();
            Debug.Log($"Automated run simulation report written to {reportAssetPath}");
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

        private sealed class RunRecord
        {
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
