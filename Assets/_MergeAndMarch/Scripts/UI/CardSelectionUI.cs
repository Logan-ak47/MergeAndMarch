using System;
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

        private Action<int> onCardSelected;

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
                background.color = card.cardColor;
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
            }

            TextMeshProUGUI description = cardObject.transform.Find("Description")?.GetComponent<TextMeshProUGUI>();
            if (description != null)
            {
                description.text = card.description;
            }

            TextMeshProUGUI rarity = cardObject.transform.Find("Rarity")?.GetComponent<TextMeshProUGUI>();
            if (rarity != null)
            {
                rarity.text = card.rarity.ToString();
            }
        }

        private void EnsurePanel()
        {
            if (parentPanel != null)
            {
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
            GameObject cardObject = new($"Card_{index}", typeof(RectTransform), typeof(Image), typeof(Button));
            cardObject.transform.SetParent(parent, false);

            RectTransform rect = cardObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(300f, 420f);
            rect.anchoredPosition = new Vector2((index - 1) * 340f, 0f);

            Image background = cardObject.GetComponent<Image>();
            background.color = Color.white;
            background.raycastTarget = true;

            Button button = cardObject.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.92f);
            colors.pressedColor = new Color(0.9f, 0.9f, 0.9f, 0.92f);
            button.colors = colors;

            CreateText(cardObject.transform, "Title", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -46f), new Vector2(250f, 60f), 30, FontStyles.Bold, TextAlignmentOptions.Center);
            CreateText(cardObject.transform, "Description", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(240f, 170f), 22, FontStyles.Normal, TextAlignmentOptions.Center);
            CreateText(cardObject.transform, "Rarity", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(220f, 40f), 20, FontStyles.Normal, TextAlignmentOptions.Center);
        }

        private void CreateText(Transform parent, string objectName, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size, float fontSize, FontStyles fontStyle, TextAlignmentOptions alignment)
        {
            GameObject textObject = new(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);

            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = alignment;
            text.color = Color.white;
            text.enableWordWrapping = true;
            text.text = string.Empty;
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
