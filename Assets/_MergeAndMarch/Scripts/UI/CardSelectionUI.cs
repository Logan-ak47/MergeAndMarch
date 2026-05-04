using System;
using System.Collections;
using MergeAndMarch.Data;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace MergeAndMarch.UI
{
    public class CardSelectionUI : MonoBehaviour
    {
        [SerializeField] private Canvas targetCanvas;
        [SerializeField] private GameObject parentPanel;
        [SerializeField] private GameObject cardPrefab;

        private Action<int> onCardSelected;
        private Coroutine entranceRoutine;

        public bool IsVisible => parentPanel != null && parentPanel.activeSelf;

        private void Awake()
        {
            ResolveCanvas();
            EnsureEventSystem();
            EnsurePanel();
            Hide();
        }

        public void ShowCards(CardData[] threeCards, Action<int> onSelected)
        {
            ResolveCanvas();
            EnsureEventSystem();
            EnsurePanel();

            onCardSelected = onSelected;
            parentPanel.SetActive(true);

            for (int i = 0; i < 3; i++)
            {
                Transform cardTransform = parentPanel.transform.Find($"Card_{i}");
                if (cardTransform == null)
                {
                    continue;
                }

                GameObject cardObject = cardTransform.gameObject;
                if (threeCards == null || i >= threeCards.Length || threeCards[i] == null)
                {
                    cardObject.SetActive(false);
                    continue;
                }

                cardObject.SetActive(true);
                ConfigureCard(cardObject, threeCards[i], i);
            }

            if (entranceRoutine != null)
            {
                StopCoroutine(entranceRoutine);
            }

            entranceRoutine = StartCoroutine(PlayEntranceRoutine());
        }

        public void Hide()
        {
            if (parentPanel != null)
            {
                parentPanel.SetActive(false);
            }
        }

        private void ConfigureCard(GameObject cardObject, CardData card, int index)
        {
            Image background = cardObject.GetComponent<Image>();
            if (background != null)
            {
                background.color = new Color(0.96f, 0.95f, 0.9f, 1f);
            }

            Button button = cardObject.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => onCardSelected?.Invoke(index));
            }

            TextMeshProUGUI title = cardObject.transform.Find("Title")?.GetComponent<TextMeshProUGUI>();
            if (title != null)
            {
                title.text = card.cardName;
                title.color = Color.white;
                title.enableAutoSizing = true;
                title.fontSizeMin = 16f;
                title.fontSizeMax = 24f;
                title.alignment = TextAlignmentOptions.MidlineLeft;
                title.margin = Vector4.zero;
            }

            TextMeshProUGUI description = cardObject.transform.Find("Description")?.GetComponent<TextMeshProUGUI>();
            if (description != null)
            {
                description.text = card.description;
                description.color = new Color(0.13f, 0.12f, 0.16f, 1f);
                description.enableAutoSizing = true;
                description.fontSizeMin = 16f;
                description.fontSizeMax = 22f;
                description.margin = new Vector4(8f, 0f, 8f, 0f);
            }

            Image header = cardObject.transform.Find("Header")?.GetComponent<Image>();
            if (header != null)
            {
                header.color = card.cardColor;
            }

            TextMeshProUGUI icon = cardObject.transform.Find("Icon")?.GetComponent<TextMeshProUGUI>();
            if (icon != null)
            {
                icon.text = GetCategoryIconText(card.category);
                icon.color = Color.white;
                icon.enableAutoSizing = true;
                icon.fontSizeMin = 10f;
                icon.fontSizeMax = 16f;
                icon.alignment = TextAlignmentOptions.Center;
            }

            ConfigureRarityGems(cardObject.transform, card.rarity);
        }

        private IEnumerator PlayEntranceRoutine()
        {
            const float duration = 0.34f;
            const float stagger = 0.07f;
            RectTransform[] cards = new RectTransform[3];
            Vector2[] endPositions = new Vector2[3];
            Vector2[] startPositions = new Vector2[3];

            for (int i = 0; i < cards.Length; i++)
            {
                cards[i] = parentPanel.transform.Find($"Card_{i}")?.GetComponent<RectTransform>();
                if (cards[i] == null || !cards[i].gameObject.activeSelf)
                {
                    continue;
                }

                endPositions[i] = new Vector2((i - 1) * 340f, 0f);
                startPositions[i] = endPositions[i] + new Vector2(0f, -520f);
                cards[i].anchoredPosition = startPositions[i];
            }

            float elapsed = 0f;
            float totalDuration = duration + (stagger * 2f);
            while (elapsed < totalDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                for (int i = 0; i < cards.Length; i++)
                {
                    if (cards[i] == null || !cards[i].gameObject.activeSelf)
                    {
                        continue;
                    }

                    float t = Mathf.Clamp01((elapsed - (i * stagger)) / duration);
                    float eased = Mathf.Sin(t * Mathf.PI * 0.5f) * 1.08f - Mathf.Max(0f, t * 0.08f);
                    cards[i].anchoredPosition = Vector2.LerpUnclamped(startPositions[i], endPositions[i], eased);
                }

                yield return null;
            }

            for (int i = 0; i < cards.Length; i++)
            {
                if (cards[i] != null)
                {
                    cards[i].anchoredPosition = endPositions[i];
                }
            }
        }

        private void EnsurePanel()
        {
            if (parentPanel != null)
            {
                for (int i = 0; i < 3; i++)
                {
                    CreateCard(parentPanel.transform, i);
                }

                return;
            }

            if (targetCanvas == null)
            {
                return;
            }

            parentPanel = new GameObject("CardSelectionPanel", typeof(RectTransform), typeof(Image));
            parentPanel.transform.SetParent(targetCanvas.transform, false);

            RectTransform panelRect = parentPanel.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            Image panelImage = parentPanel.GetComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 0.72f);
            panelImage.raycastTarget = true;

            for (int i = 0; i < 3; i++)
            {
                CreateCard(parentPanel.transform, i);
            }
        }

        private void CreateCard(Transform parent, int index)
        {
            Transform existing = parent.Find($"Card_{index}");
            GameObject cardObject = existing != null ? existing.gameObject : CreateCardObject(parent, index);
            cardObject.transform.SetParent(parent, false);

            RectTransform rect = cardObject.GetComponent<RectTransform>();
            if (rect == null)
            {
                rect = cardObject.AddComponent<RectTransform>();
            }

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(300f, 420f);
            rect.anchoredPosition = new Vector2((index - 1) * 340f, 0f);

            Image background = cardObject.GetComponent<Image>();
            if (background == null)
            {
                background = cardObject.AddComponent<Image>();
            }

            background.color = Color.white;
            background.raycastTarget = true;

            Button button = cardObject.GetComponent<Button>();
            if (button == null)
            {
                button = cardObject.AddComponent<Button>();
            }

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.92f);
            colors.pressedColor = new Color(0.9f, 0.9f, 0.9f, 0.92f);
            button.colors = colors;

            CreateCardPanel(cardObject.transform, "Header", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(0f, 78f), Color.white);
            CreateCardPanel(cardObject.transform, "Badge", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(39f, -39f), new Vector2(50f, 30f), new Color(0f, 0f, 0f, 0.18f));
            CreateText(cardObject.transform, "Icon", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(39f, -39f), new Vector2(46f, 24f), 14, FontStyles.Bold, TextAlignmentOptions.Center);
            CreateText(cardObject.transform, "Title", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(38f, -39f), new Vector2(-106f, 44f), 22, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            CreateText(cardObject.transform, "Description", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(236f, 170f), 20, FontStyles.Normal, TextAlignmentOptions.Center);
            FitTitleToHeader(cardObject.transform);

            for (int i = 0; i < 3; i++)
            {
                CreateCardPanel(cardObject.transform, $"Gem_{i}", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 28f, 34f), new Vector2(18f, 18f), Color.gray);
            }
        }

        private GameObject CreateCardObject(Transform parent, int index)
        {
            if (cardPrefab != null)
            {
                GameObject instance = Instantiate(cardPrefab, parent, false);
                instance.name = $"Card_{index}";
                if (instance.GetComponent<Image>() == null)
                {
                    instance.AddComponent<Image>();
                }

                if (instance.GetComponent<Button>() == null)
                {
                    instance.AddComponent<Button>();
                }

                return instance;
            }

            return new GameObject($"Card_{index}", typeof(RectTransform), typeof(Image), typeof(Button));
        }

        private Image CreateCardPanel(Transform parent, string objectName, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size, Color color)
        {
            Transform existing = parent.Find(objectName);
            GameObject panelObject = existing != null ? existing.gameObject : new GameObject(objectName, typeof(RectTransform), typeof(Image));
            panelObject.transform.SetParent(parent, false);

            RectTransform rect = panelObject.GetComponent<RectTransform>();
            if (rect == null)
            {
                rect = panelObject.AddComponent<RectTransform>();
            }

            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Image image = panelObject.GetComponent<Image>();
            if (image == null)
            {
                image = panelObject.AddComponent<Image>();
            }

            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private void CreateText(Transform parent, string objectName, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size, float fontSize, FontStyles fontStyle, TextAlignmentOptions alignment)
        {
            Transform existing = parent.Find(objectName);
            GameObject textObject = existing != null ? existing.gameObject : new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);

            RectTransform rect = textObject.GetComponent<RectTransform>();
            if (rect == null)
            {
                rect = textObject.AddComponent<RectTransform>();
            }

            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            if (text == null)
            {
                text = textObject.AddComponent<TextMeshProUGUI>();
            }

            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = alignment;
            text.color = Color.white;
            text.outlineColor = Color.black;
            text.outlineWidth = fontStyle == FontStyles.Bold ? 0.08f : 0f;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.text = string.Empty;
        }

        private void FitTitleToHeader(Transform cardTransform)
        {
            RectTransform title = cardTransform.Find("Title")?.GetComponent<RectTransform>();
            if (title == null)
            {
                return;
            }

            title.offsetMin = new Vector2(76f, title.offsetMin.y);
            title.offsetMax = new Vector2(-14f, title.offsetMax.y);
        }

        private void ConfigureRarityGems(Transform cardTransform, CardRarity rarity)
        {
            int visibleCount = rarity switch
            {
                CardRarity.Rare => 2,
                CardRarity.Epic => 3,
                _ => 1
            };

            Color gemColor = rarity switch
            {
                CardRarity.Rare => new Color(0.22f, 0.58f, 1f, 1f),
                CardRarity.Epic => new Color(0.68f, 0.3f, 1f, 1f),
                _ => new Color(0.55f, 0.58f, 0.62f, 1f)
            };

            for (int i = 0; i < 3; i++)
            {
                Image gem = cardTransform.Find($"Gem_{i}")?.GetComponent<Image>();
                if (gem == null)
                {
                    continue;
                }

                gem.gameObject.SetActive(i < visibleCount);
                gem.color = gemColor;
            }
        }

        private string GetCategoryIconText(CardCategory category)
        {
            return category switch
            {
                CardCategory.StatBoost => "ATK",
                CardCategory.Spawn => "+",
                CardCategory.MergeBoost => "UP",
                CardCategory.Heal => "HP",
                CardCategory.Special => "*",
                CardCategory.Economy => "$",
                CardCategory.Deployment => "DEF",
                _ => "?"
            };
        }

        private void ResolveCanvas()
        {
            if (targetCanvas != null)
            {
                return;
            }

            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            for (int i = 0; i < canvases.Length; i++)
            {
                if (canvases[i] != null && canvases[i].renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    targetCanvas = canvases[i];
                    break;
                }
            }
        }

        private void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }
    }
}
