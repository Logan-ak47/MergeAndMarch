using System.Collections;
using MergeAndMarch.Core;
using MergeAndMarch.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MergeAndMarch.Gameplay
{
    public class WaveManager : MonoBehaviour
    {
        public enum WaveState
        {
            Idle,
            Deploying,
            Combat,
            WaveCleared,
            CardPick,
            BetweenWaves,
            BossWave,
            Victory,
            Defeat
        }

        [SerializeField] private GameConfig gameConfig;
        [SerializeField] private BattleGrid battleGrid;
        [SerializeField] private EnemySpawner enemySpawner;
        [SerializeField] private DeploymentSystem deploymentSystem;
        [SerializeField] private AutoCombat autoCombat;
        [SerializeField] private RunManager runManager;
        [SerializeField] private CardSystem cardSystem;
        [SerializeField] private DraftSystem draft;

        private MergeController mergeController;
        private DraftTray tray;
        private Coroutine runRoutine;
        private Coroutine restartRoutine;
        private Canvas hudCanvas;
        private Image waveCounterBar;
        private Image coinCounterIcon;
        private Image activeBuffsPanel;
        private TextMeshProUGUI waveCounterText;
        private TextMeshProUGUI coinCounterText;
        private TextMeshProUGUI mergeCounterText;
        private TextMeshProUGUI activeBuffsText;
        private TextMeshProUGUI waveClearedText;
        private TextMeshProUGUI runEndText;
        private readonly System.Collections.Generic.List<Image> wavePips = new();
        private bool runActive;
        private bool runEnded;
        private BaseHealth baseHealth;
        private bool cardPickCompleted;
        private int currentWave;
        private int totalCoins;
        private int mergeCount;

        public WaveState State { get; private set; } = WaveState.Idle;
        public int CurrentWave => currentWave;
        public int TotalCoins => totalCoins;
        public int MergeCount => mergeCount;
        public bool IsRunActive => runActive;
        public bool HasRunEnded => runEnded;
        public bool AutoRestartOnRunEnd { get; set; } = true;
        public DraftSystem Draft => draft;

        private void Awake()
        {
            ResolveReferences();
            EnsureHud();
            if (cardSystem != null)
            {
                cardSystem.CardPickCompleted += HandleCardPickCompleted;
                cardSystem.RunBuffsChanged += UpdateActiveBuffs;
            }
        }

        private void OnDisable()
        {
            if (enemySpawner != null)
            {
                enemySpawner.EnemyEscapedEvent -= HandleEnemyEscaped;
            }

            if (cardSystem != null)
            {
                cardSystem.CardPickCompleted -= HandleCardPickCompleted;
                cardSystem.RunBuffsChanged -= UpdateActiveBuffs;
            }
        }

        private void Update()
        {
            if (!runActive || runEnded || battleGrid == null)
            {
                return;
            }

            if (!battleGrid.HasAnyTroops())
            {
                TriggerDefeat();
            }
        }

        public void BeginRun()
        {
            ResolveReferences();
            EnsureHud();
            HideCenterTexts();

            if (runRoutine != null)
            {
                StopCoroutine(runRoutine);
            }

            if (restartRoutine != null)
            {
                StopCoroutine(restartRoutine);
            }

            if (enemySpawner != null)
            {
                enemySpawner.EnemyEscapedEvent -= HandleEnemyEscaped;
                enemySpawner.EnemyEscapedEvent += HandleEnemyEscaped;
                enemySpawner.ResetSpawner();
            }

            cardSystem?.ResetRunBuffs();
            draft?.ResetRun();
            if (draft != null)
            {
                draft.AcceptingDrafts = true;
            }

            battleGrid?.ResetTroopsForWaveStart();
            autoCombat?.SetCombatEnabled(true);
            autoCombat?.ResetAttackTimers();
            currentWave = 0;
            totalCoins = 0;
            mergeCount = 0;
            EnsureBaseHealth().ResetHealth();
            runActive = true;
            runEnded = false;
            State = WaveState.Idle;
            UpdateCoinCounter();
            UpdateWaveCounter();
            UpdateMergeCounter();
            UpdateActiveBuffs();
            runRoutine = StartCoroutine(RunLoop());
        }

        public void RegisterMerge()
        {
            if (!runActive || runEnded)
            {
                return;
            }

            mergeCount++;
            UpdateMergeCounter();
        }

        private IEnumerator RunLoop()
        {
            if (gameConfig != null && gameConfig.startWaveDelay > 0f)
            {
                yield return new WaitForSecondsRealtime(gameConfig.startWaveDelay);
            }

            int bossWave = enemySpawner != null ? enemySpawner.BossWave : 16;
            for (int waveNumber = 1; waveNumber <= bossWave; waveNumber++)
            {
                if (waveNumber > 1)
                {
                    State = WaveState.BetweenWaves;
                    yield return new WaitForSecondsRealtime(gameConfig.timeBetweenWaves);

                    if (runEnded)
                    {
                        yield break;
                    }

                    State = WaveState.Deploying;
                    battleGrid?.ResetTroopsForWaveStart();
                    deploymentSystem?.DeployCardReinforcements();
                    autoCombat?.ResetAttackTimers();
                }

                currentWave = waveNumber;
                if (draft != null)
                {
                    draft.AcceptingDrafts = true;
                }

                State = waveNumber == bossWave ? WaveState.BossWave : WaveState.Combat;
                UpdateWaveCounter();
                enemySpawner?.SpawnWave(waveNumber);

                yield return new WaitUntil(() => runEnded || (enemySpawner != null && enemySpawner.WaveCleared));
                if (runEnded)
                {
                    yield break;
                }

                AwardCoins(waveNumber == bossWave ? 50 : 10);
                if (waveNumber == bossWave)
                {
                    TriggerVictory();
                    yield break;
                }

                yield return RunPostWaveSequence(waveNumber);
                if (runEnded)
                {
                    yield break;
                }
            }
        }

        private IEnumerator RunPostWaveSequence(int clearedWave)
        {
            State = WaveState.WaveCleared;
            EnsureBaseHealth().Heal(gameConfig != null ? gameConfig.baseHealOnWaveClear : 0f);
            draft?.GrantWaveClearEnergy();
            yield return PlayWaveClearedRoutine();

            if (IsCardPickWave(clearedWave) && cardSystem != null)
            {
                // A troop draft still open from the last kills finishes before the card pick starts.
                yield return new WaitUntil(() => runEnded || draft == null || !draft.IsOpen);
                if (runEnded)
                {
                    yield break;
                }

                if (draft != null)
                {
                    draft.AcceptingDrafts = false;
                }

                State = WaveState.CardPick;
                cardPickCompleted = false;
                cardSystem.StartCardPick();
                yield return new WaitUntil(() => runEnded || cardPickCompleted);
                if (draft != null && !runEnded)
                {
                    draft.AcceptingDrafts = true;
                }
            }
        }

        private void AwardCoins(int amount)
        {
            float multiplier = cardSystem != null ? cardSystem.runBuffs.coinMultiplier : 1f;
            totalCoins += Mathf.RoundToInt(amount * multiplier);
            UpdateCoinCounter();
        }

        private void TriggerVictory()
        {
            if (runEnded)
            {
                return;
            }

            runEnded = true;
            runActive = false;
            StopDrafts();
            State = WaveState.Victory;
            autoCombat?.SetCombatEnabled(false);
            ShowRunEnd("VICTORY!");
            if (AutoRestartOnRunEnd)
            {
                restartRoutine = StartCoroutine(RestartAfterDelay());
            }
        }

        private void TriggerDefeat()
        {
            if (runEnded)
            {
                return;
            }

            runEnded = true;
            runActive = false;
            StopDrafts();
            totalCoins = Mathf.FloorToInt(totalCoins * 0.4f);
            UpdateCoinCounter();
            State = WaveState.Defeat;
            JuiceFx.Instance?.DefeatShake();
            autoCombat?.SetCombatEnabled(false);
            ShowRunEnd("DEFEATED");
            if (AutoRestartOnRunEnd)
            {
                restartRoutine = StartCoroutine(RestartAfterDelay());
            }
        }

        private void StopDrafts()
        {
            if (draft != null)
            {
                draft.AcceptingDrafts = false;
                draft.Cancel();
            }
        }

        private IEnumerator RestartAfterDelay()
        {
            yield return new WaitForSecondsRealtime(gameConfig.runEndRestartDelay);
            runManager?.RestartRun();
        }

        private void HandleEnemyEscaped(Enemy enemy)
        {
            if (runEnded)
            {
                return;
            }

            float damage = enemy != null ? enemy.BaseDamage : 10f;
            Vector3 hitPosition = enemy != null ? enemy.transform.position : Vector3.zero;
            EnsureBaseHealth().TakeHit(damage, hitPosition);
            if (baseHealth.IsDestroyed)
            {
                TriggerDefeat();
            }
        }

        /// <summary>The base's health (created on first use).</summary>
        public BaseHealth Base => EnsureBaseHealth();

        private BaseHealth EnsureBaseHealth()
        {
            if (baseHealth == null)
            {
                baseHealth = GetComponent<BaseHealth>();
                if (baseHealth == null)
                {
                    baseHealth = gameObject.AddComponent<BaseHealth>();
                }

                baseHealth.Initialize(gameConfig, battleGrid);
            }

            return baseHealth;
        }

        private void HandleCardPickCompleted()
        {
            cardPickCompleted = true;
        }

        private bool IsCardPickWave(int waveNumber)
        {
            return gameConfig != null && gameConfig.cardPickWaves != null && System.Array.IndexOf(gameConfig.cardPickWaves, waveNumber) >= 0;
        }

        private void ResolveReferences()
        {
            if (runManager == null)
            {
                runManager = FindFirstObjectByType<RunManager>();
            }

            if (battleGrid == null)
            {
                battleGrid = FindFirstObjectByType<BattleGrid>();
            }

            if (gameConfig == null)
            {
                gameConfig = battleGrid != null ? battleGrid.Config : (runManager != null ? runManager.Config : null);
            }

            if (enemySpawner == null)
            {
                enemySpawner = FindFirstObjectByType<EnemySpawner>();
            }

            if (deploymentSystem == null && runManager != null)
            {
                deploymentSystem = runManager.GetComponent<DeploymentSystem>();
                if (deploymentSystem == null)
                {
                    deploymentSystem = runManager.gameObject.AddComponent<DeploymentSystem>();
                }
            }

            if (autoCombat == null)
            {
                autoCombat = FindFirstObjectByType<AutoCombat>();
            }

            if (cardSystem == null && runManager != null)
            {
                cardSystem = runManager.GetComponent<CardSystem>();
                if (cardSystem == null)
                {
                    cardSystem = runManager.gameObject.AddComponent<CardSystem>();
                }
            }

            ResolveDraftAndTray();
        }

        /// <summary>Creates and wires the kill-energy offers and the offer tray (design/specs/offer-tray.md).</summary>
        private void ResolveDraftAndTray()
        {
            GameObject host = runManager != null ? runManager.gameObject : gameObject;
            if (draft == null)
            {
                draft = host.GetComponent<DraftSystem>();
                if (draft == null)
                {
                    draft = host.AddComponent<DraftSystem>();
                }
            }

            if (tray == null)
            {
                tray = host.GetComponent<DraftTray>();
                if (tray == null)
                {
                    tray = host.AddComponent<DraftTray>();
                }
            }

            if (mergeController == null)
            {
                mergeController = FindFirstObjectByType<MergeController>();
            }

            draft.Initialize(gameConfig, battleGrid, deploymentSystem, mergeController, enemySpawner);
            tray.Initialize(gameConfig, battleGrid, draft, mergeController);
            if (mergeController != null)
            {
                mergeController.SetDraft(draft);
            }
        }

        private void EnsureHud()
        {
            if (hudCanvas == null)
            {
                Canvas existingCanvas = FindFirstObjectByType<Canvas>();
                if (existingCanvas != null && existingCanvas.renderMode == RenderMode.ScreenSpaceOverlay && existingCanvas.name == "BattleHUD")
                {
                    hudCanvas = existingCanvas;
                }
            }

            if (hudCanvas == null)
            {
                GameObject canvasObject = new("BattleHUD");
                hudCanvas = canvasObject.AddComponent<Canvas>();
                hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObject.AddComponent<CanvasScaler>();
                canvasObject.AddComponent<GraphicRaycaster>();
            }

            if (waveCounterText == null)
            {
                waveCounterBar = CreatePanel("WaveCounterBar", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -44f), new Vector2(360f, 62f), new Color(0f, 0f, 0f, 0.6f));
                waveCounterText = CreateText("WaveCounter", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -42f), new Vector2(320f, 58f), 42, TextAlignmentOptions.Center);
            }

            if (coinCounterText == null)
            {
                coinCounterIcon = CreatePanel("CoinIcon", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-180f, -40f), new Vector2(32f, 32f), new Color(1f, 0.78f, 0.16f, 1f));
                coinCounterText = CreateText("CoinCounter", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-36f, -40f), new Vector2(150f, 48f), 32, TextAlignmentOptions.Right);
            }

            if (mergeCounterText == null)
            {
                mergeCounterText = CreateText("MergeCounter", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -78f), new Vector2(260f, 38f), 22, TextAlignmentOptions.Center);
            }

            if (activeBuffsText == null)
            {
                activeBuffsPanel = CreatePanel("ActiveBuffsPanel", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -18f), new Vector2(310f, 150f), new Color(0f, 0f, 0f, 0.36f));
                activeBuffsText = CreateText("ActiveBuffsText", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(34f, -28f), new Vector2(280f, 130f), 22, TextAlignmentOptions.TopLeft);
                activeBuffsText.textWrappingMode = TextWrappingModes.NoWrap;
            }

            if (waveClearedText == null)
            {
                waveClearedText = CreateText("WaveClearedText", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(680f, 110f), 60, TextAlignmentOptions.Center);
                waveClearedText.text = "WAVE CLEARED";
                waveClearedText.color = new Color(1f, 0.843f, 0f, 1f);
            }

            if (runEndText == null)
            {
                runEndText = CreateText("RunEndText", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600f, 120f), 52, TextAlignmentOptions.Center);
            }

            // Publisher-test HUD (direction 2026-11-03): wave progress only. Coins, merges and the buffs
            // panel stay tracked but hidden during a run.
            SetHudVisible(coinCounterIcon, false);
            SetHudVisible(coinCounterText, false);
            SetHudVisible(mergeCounterText, false);
            SetHudVisible(activeBuffsPanel, false);
            SetHudVisible(activeBuffsText, false);
            EnsureWavePips();

            HideCenterTexts();
        }

        private static void SetHudVisible(Graphic graphic, bool visible)
        {
            if (graphic != null)
            {
                graphic.gameObject.SetActive(visible);
            }
        }

        /// <summary>One pip per wave under the wave pill; boss waves are larger and red.</summary>
        private void EnsureWavePips()
        {
            int bossWave = enemySpawner != null ? enemySpawner.BossWave : 8;
            if (wavePips.Count == bossWave)
            {
                return;
            }

            foreach (Image pip in wavePips)
            {
                if (pip != null)
                {
                    Destroy(pip.gameObject);
                }
            }

            wavePips.Clear();
            const float spacing = 38f;
            for (int wave = 1; wave <= bossWave; wave++)
            {
                bool boss = IsBossPip(wave, bossWave);
                float size = boss ? 30f : 20f;
                float x = (wave - ((bossWave + 1) * 0.5f)) * spacing;
                wavePips.Add(CreatePanel($"WavePip_{wave}", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(x, -112f), new Vector2(size, size), Color.white));
            }
        }

        private bool IsBossPip(int wave, int bossWave)
        {
            return wave == bossWave || (enemySpawner != null && enemySpawner.Waves.IsMidBossWave(wave));
        }

        private void UpdateWavePips()
        {
            EnsureWavePips();
            int bossWave = enemySpawner != null ? enemySpawner.BossWave : 8;
            Color done = Color.white;
            Color current = new(1f, 0.761f, 0.102f, 1f);
            Color upcoming = new(0.114f, 0.078f, 0.259f, 0.55f);
            Color boss = new(0.941f, 0.157f, 0.247f, 1f);
            for (int i = 0; i < wavePips.Count; i++)
            {
                int wave = i + 1;
                Color color = wave < currentWave ? done : wave == currentWave ? current : upcoming;
                if (IsBossPip(wave, bossWave) && wave >= currentWave)
                {
                    color = wave == currentWave ? current : boss;
                }

                wavePips[i].color = color;
            }
        }

        private TextMeshProUGUI CreateText(string objectName, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size, int fontSize, TextAlignmentOptions alignment)
        {
            Transform existing = hudCanvas.transform.Find(objectName);
            GameObject textObject = existing != null ? existing.gameObject : new GameObject(objectName);
            textObject.transform.SetParent(hudCanvas.transform, false);

            RectTransform rectTransform = textObject.GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                rectTransform = textObject.AddComponent<RectTransform>();
            }

            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.pivot = pivot;
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = size;

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            if (text == null)
            {
                text = textObject.AddComponent<TextMeshProUGUI>();
            }

            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = fontSize;
            text.fontStyle = FontStyles.Bold;
            text.alignment = alignment;
            text.color = Color.white;
            text.outlineColor = Color.black;
            text.outlineWidth = 0.18f;
            text.text = string.Empty;
            return text;
        }

        private Image CreatePanel(string objectName, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size, Color color)
        {
            Transform existing = hudCanvas.transform.Find(objectName);
            GameObject panelObject = existing != null ? existing.gameObject : new GameObject(objectName);
            panelObject.transform.SetParent(hudCanvas.transform, false);

            RectTransform rectTransform = panelObject.GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                rectTransform = panelObject.AddComponent<RectTransform>();
            }

            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.pivot = pivot;
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = size;

            Image image = panelObject.GetComponent<Image>();
            if (image == null)
            {
                image = panelObject.AddComponent<Image>();
            }

            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private void UpdateWaveCounter()
        {
            if (waveCounterText == null)
            {
                return;
            }

            UpdateWavePips();
            int bossWave = enemySpawner != null ? enemySpawner.BossWave : 8;
            if (currentWave <= 0)
            {
                waveCounterText.text = $"WAVE 1/{bossWave}";
                waveCounterText.color = Color.white;
                waveCounterText.fontSize = 42;
                return;
            }

            bool isBoss = currentWave >= bossWave;
            bool isMidBoss = enemySpawner != null && enemySpawner.Waves.IsMidBossWave(currentWave);
            waveCounterText.text = isBoss ? "BOSS" : isMidBoss ? "MID-BOSS" : $"WAVE {currentWave}/{bossWave}";
            waveCounterText.color = isBoss ? new Color(1f, 0.78f, 0.16f, 1f) : Color.white;
            waveCounterText.fontSize = isBoss ? 48 : 42;
        }

        private void UpdateCoinCounter()
        {
            if (coinCounterText != null)
            {
                coinCounterText.text = totalCoins.ToString();
            }
        }

        private void UpdateMergeCounter()
        {
            if (mergeCounterText != null)
            {
                mergeCounterText.text = $"Merges: {mergeCount}";
            }
        }

        private void UpdateActiveBuffs()
        {
            if (activeBuffsText == null)
            {
                return;
            }

            activeBuffsText.text = cardSystem != null ? cardSystem.GetActiveBuffSummary() : "Active Buffs:\nNone";
        }

        private IEnumerator PlayWaveClearedRoutine()
        {
            if (waveClearedText == null)
            {
                yield break;
            }

            waveClearedText.gameObject.SetActive(true);
            waveClearedText.color = new Color(1f, 0.843f, 0f, 1f);
            waveClearedText.transform.localScale = Vector3.zero;

            float elapsed = 0f;
            while (elapsed < 0.2f)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / 0.2f);
                waveClearedText.transform.localScale = Vector3.one * Mathf.Lerp(0f, 1.2f, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < 0.1f)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / 0.1f);
                waveClearedText.transform.localScale = Vector3.one * Mathf.Lerp(1.2f, 1f, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }

            yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, gameConfig.waveClearedBannerDuration));

            elapsed = 0f;
            Color startColor = waveClearedText.color;
            while (elapsed < 0.3f)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / 0.3f);
                waveClearedText.color = new Color(startColor.r, startColor.g, startColor.b, Mathf.Lerp(1f, 0f, t));
                yield return null;
            }

            waveClearedText.gameObject.SetActive(false);
            waveClearedText.transform.localScale = Vector3.one;
        }

        private void ShowRunEnd(string message)
        {
            HideCenterTexts();
            runEndText.text = message;
            runEndText.gameObject.SetActive(true);
        }

        private void HideCenterTexts()
        {
            if (waveClearedText != null)
            {
                waveClearedText.gameObject.SetActive(false);
            }

            if (runEndText != null)
            {
                runEndText.gameObject.SetActive(false);
            }
        }
    }
}
