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
        private bool runActive;
        private bool runEnded;
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
            battleGrid?.ResetTroopsForWaveStart();
            autoCombat?.SetCombatEnabled(true);
            autoCombat?.ResetAttackTimers();
            currentWave = 0;
            totalCoins = 0;
            mergeCount = 0;
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

            for (int waveNumber = 1; waveNumber <= 16; waveNumber++)
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
                    deploymentSystem?.DeployTroopsForNextWave();
                    autoCombat?.ResetAttackTimers();
                }

                currentWave = waveNumber;
                State = waveNumber == 16 ? WaveState.BossWave : WaveState.Combat;
                UpdateWaveCounter();
                enemySpawner?.SpawnWave(waveNumber);

                yield return new WaitUntil(() => runEnded || (enemySpawner != null && enemySpawner.WaveCleared));
                if (runEnded)
                {
                    yield break;
                }

                AwardCoins(waveNumber == 16 ? 50 : 10);
                if (waveNumber == 16)
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
            yield return PlayWaveClearedRoutine();

            if (IsCardPickWave(clearedWave) && cardSystem != null)
            {
                State = WaveState.CardPick;
                cardPickCompleted = false;
                cardSystem.StartCardPick();
                yield return new WaitUntil(() => runEnded || cardPickCompleted);
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
            totalCoins = Mathf.FloorToInt(totalCoins * 0.4f);
            UpdateCoinCounter();
            State = WaveState.Defeat;
            autoCombat?.SetCombatEnabled(false);
            ShowRunEnd("DEFEATED");
            if (AutoRestartOnRunEnd)
            {
                restartRoutine = StartCoroutine(RestartAfterDelay());
            }
        }

        private IEnumerator RestartAfterDelay()
        {
            yield return new WaitForSecondsRealtime(gameConfig.runEndRestartDelay);
            runManager?.RestartRun();
        }

        private void HandleEnemyEscaped()
        {
            TriggerDefeat();
        }

        private void HandleCardPickCompleted()
        {
            cardPickCompleted = true;
        }

        private bool IsCardPickWave(int waveNumber)
        {
            return waveNumber == 3 || waveNumber == 6 || waveNumber == 9 || waveNumber == 12 || waveNumber == 15;
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

            HideCenterTexts();
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

            if (currentWave <= 0)
            {
                waveCounterText.text = "Wave 1 / 15";
                waveCounterText.color = Color.white;
                waveCounterText.fontSize = 42;
                return;
            }

            bool isBoss = currentWave >= 16;
            waveCounterText.text = isBoss ? "BOSS" : $"Wave {currentWave} / 15";
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
