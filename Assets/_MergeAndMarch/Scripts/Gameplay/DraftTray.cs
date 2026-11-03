using System.Collections;
using System.Collections.Generic;
using MergeAndMarch.Core;
using MergeAndMarch.Data;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MergeAndMarch.Gameplay
{
    /// <summary>
    /// The offer tray under the board (design/specs/offer-tray.md): a shelf with the energy meter on
    /// its top edge and up to N troop cards. Presentation plus card-drag input; every rule lives in
    /// <see cref="DraftSystem"/>, which this calls to deploy, merge or replace. Placeholder visuals
    /// until the UI kit lands; cards are deliberately card-shaped so they never read as grid slots.
    /// </summary>
    public class DraftTray : MonoBehaviour
    {
        private const string SortingLayer = "UI";
        private const float MeterHeight = 0.16f;
        private const float EntranceDuration = 0.28f;
        private const float EntranceStagger = 0.06f;
        private const float DragScale = 1.12f;

        private GameConfig gameConfig;
        private BattleGrid battleGrid;
        private DraftSystem draft;
        private MergeController mergeController;
        private Camera targetCamera;

        private Transform root;
        private SpriteRenderer shelf;
        private SpriteRenderer meterTrack;
        private SpriteRenderer meterFill;
        private readonly List<Card> cards = new();
        private float energyRatio;
        private int draggingIndex = -1;
        private Vector3 dragOffset;

        private sealed class Card
        {
            public Transform Root;
            public SpriteRenderer Plate;
            public SpriteRenderer Header;
            public SpriteRenderer Body;
            public SpriteRenderer Prop;
            public SpriteRenderer Face;
            public TextMeshPro Label;
            public Vector3 Home;
            public bool Active;
        }

        /// <summary>Injects dependencies and subscribes to the draft.</summary>
        public void Initialize(GameConfig config, BattleGrid grid, DraftSystem draftSystem, MergeController merges)
        {
            gameConfig = config;
            battleGrid = grid;
            mergeController = merges;
            if (draft != draftSystem)
            {
                Unsubscribe();
                draft = draftSystem;
                if (draft != null)
                {
                    draft.DraftOpened += HandleOpened;
                    draft.DraftClosed += HandleClosed;
                    draft.EnergyChanged += HandleEnergyChanged;
                }
            }

            EnsureVisuals();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void Unsubscribe()
        {
            if (draft == null)
            {
                return;
            }

            draft.DraftOpened -= HandleOpened;
            draft.DraftClosed -= HandleClosed;
            draft.EnergyChanged -= HandleEnergyChanged;
        }

        private void HandleEnergyChanged(float energy, float threshold)
        {
            energyRatio = threshold > 0f ? Mathf.Clamp01(energy / threshold) : 0f;
        }

        private void HandleOpened(IReadOnlyList<TroopData> offers)
        {
            CancelDrag(returnCard: false);
            BeanArtSet art = JuiceFx.Instance != null ? JuiceFx.Instance.Art : null;
            for (int i = 0; i < cards.Count; i++)
            {
                Card card = cards[i];
                card.Active = i < offers.Count && offers[i] != null;
                if (!card.Active)
                {
                    continue;
                }

                TroopData data = offers[i];
                card.Header.color = data.troopColor;
                SetSprite(card.Body, data.beanBody != null ? data.beanBody : data.sprite);
                SetSprite(card.Prop, data.beanProp);
                SetSprite(card.Face, art != null && data.beanBody != null ? art.GetFace(BeanFace.Idle, false) : null);
                card.Label.text = data.displayName;
            }

            StartCoroutine(PlayEntrance());
        }

        private void HandleClosed()
        {
            CancelDrag(returnCard: false);
            for (int i = 0; i < cards.Count; i++)
            {
                cards[i].Active = false;
            }
        }

        private void Update()
        {
            if (draft == null || battleGrid == null)
            {
                return;
            }

            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            if (targetCamera != null)
            {
                HandleInput();
            }
        }

        private void LateUpdate()
        {
            if (root == null || battleGrid == null || gameConfig == null)
            {
                return;
            }

            Layout();
            for (int i = 0; i < cards.Count; i++)
            {
                ApplyCardState(cards[i], i == draggingIndex);
            }
        }

        private void HandleInput()
        {
            if (draggingIndex < 0)
            {
                if (draft.IsOpen && !mergeController.IsBusy && TryGetPointerDown(out Vector3 down))
                {
                    int index = CardAt(down);
                    if (index >= 0)
                    {
                        BeginDrag(index, down);
                    }
                }

                return;
            }

            if (TryGetPointer(out Vector3 pointer))
            {
                cards[draggingIndex].Root.position = pointer + dragOffset;
            }

            if (TryGetPointerUp(out Vector3 up))
            {
                Drop(up);
            }
        }

        private void BeginDrag(int index, Vector3 pointer)
        {
            draggingIndex = index;
            dragOffset = cards[index].Root.position - pointer;
            mergeController.InputLocked = true;
            TimeScaleManager.Instance?.SetGameplaySlowMo(gameConfig.tacticalSlowTimeScale);
            JuiceFx.Instance?.SetSlowMo(true);
            mergeController.HighlightOfferTargets(draft.Offers[index].troopType);
        }

        private void Drop(Vector3 pointer)
        {
            int index = draggingIndex;
            bool placed = false;
            if (battleGrid.TryGetNearestSlot(pointer, out int column, out int row))
            {
                Troop target = battleGrid.GetTroopAt(column, row);
                if (target == null)
                {
                    placed = draft.TryDeploy(index, column, row);
                }
                else if (draft.CanMergeInto(index, target))
                {
                    placed = draft.TryMerge(index, target);
                }
                else if (draft.IsGridFull())
                {
                    placed = draft.TryReplace(index, target);
                }
            }

            CancelDrag(returnCard: !placed);
        }

        private void CancelDrag(bool returnCard)
        {
            if (draggingIndex < 0)
            {
                return;
            }

            if (returnCard && draggingIndex < cards.Count)
            {
                cards[draggingIndex].Root.position = cards[draggingIndex].Home;
            }

            draggingIndex = -1;
            if (mergeController != null)
            {
                mergeController.InputLocked = false;
                mergeController.ClearOfferHighlights();
            }

            JuiceFx.Instance?.SetSlowMo(false);
            if (draft == null || !draft.IsSlowMoPulseActive)
            {
                TimeScaleManager.Instance?.ResetTimeScale();
            }
        }

        private int CardAt(Vector3 world)
        {
            float halfW = gameConfig.trayCardWidth * 0.5f;
            float halfH = gameConfig.trayCardHeight * 0.5f;
            for (int i = 0; i < cards.Count; i++)
            {
                if (!cards[i].Active)
                {
                    continue;
                }

                Vector3 c = cards[i].Home;
                if (Mathf.Abs(world.x - c.x) <= halfW && Mathf.Abs(world.y - c.y) <= halfH)
                {
                    return i;
                }
            }

            return -1;
        }

        private void Layout()
        {
            Bounds grid = battleGrid.GetGridWorldBounds();
            float centreY = grid.min.y - gameConfig.trayOffset;
            float width = grid.size.x + (gameConfig.cellSize * 0.2f);
            float shelfHeight = gameConfig.trayCardHeight + 0.3f;

            shelf.transform.position = new Vector3(grid.center.x, centreY, 0f);
            shelf.transform.localScale = new Vector3(width, shelfHeight, 1f);

            float meterY = centreY + (shelfHeight * 0.5f);
            float meterLeft = grid.center.x - (width * 0.5f);
            meterTrack.transform.position = new Vector3(meterLeft, meterY, 0f);
            meterTrack.transform.localScale = new Vector3(width, MeterHeight, 1f);
            meterFill.transform.position = new Vector3(meterLeft, meterY, 0f);
            meterFill.transform.localScale = new Vector3(width * energyRatio, MeterHeight * 0.7f, 1f);
            bool waiting = draft != null && draft.IsOpen;
            float pulse = waiting ? 0.75f + (0.25f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4f))) : 1f;
            meterFill.color = new Color(pulse, pulse, pulse, 1f);

            int count = cards.Count;
            float spacing = width / count;
            for (int i = 0; i < count; i++)
            {
                cards[i].Home = new Vector3(meterLeft + (spacing * (i + 0.5f)), centreY, 0f);
            }
        }

        private void ApplyCardState(Card card, bool dragging)
        {
            if (dragging)
            {
                card.Root.localScale = Vector3.one * DragScale;
            }
            else
            {
                card.Root.position = card.Home;
            }

            // Idle tray: dim card-back silhouettes, never empty slot outlines.
            Color plateColor = card.Active ? new Color(0.98f, 0.96f, 0.9f, 1f) : new Color(0.114f, 0.078f, 0.259f, 0.55f);
            card.Plate.color = plateColor;
            card.Header.enabled = card.Active;
            card.Body.enabled = card.Active && card.Body.sprite != null;
            card.Prop.enabled = card.Active && card.Prop.sprite != null;
            card.Face.enabled = card.Active && card.Face.sprite != null;
            card.Label.enabled = card.Active;
            int order = dragging ? 40 : 10;
            card.Plate.sortingOrder = order;
            card.Header.sortingOrder = order + 1;
            card.Body.sortingOrder = order + 2;
            card.Prop.sortingOrder = order + 3;
            card.Face.sortingOrder = order + 4;
            card.Label.sortingOrder = order + 5;
        }

        private IEnumerator PlayEntrance()
        {
            float elapsed = 0f;
            float total = EntranceDuration + (EntranceStagger * cards.Count);
            while (elapsed < total)
            {
                elapsed += Time.unscaledDeltaTime;
                for (int i = 0; i < cards.Count; i++)
                {
                    if (i == draggingIndex)
                    {
                        continue;
                    }

                    float t = Mathf.Clamp01((elapsed - (i * EntranceStagger)) / EntranceDuration);
                    float overshoot = 1f + (Mathf.Sin(t * Mathf.PI) * 0.15f);
                    cards[i].Root.localScale = Vector3.one * Mathf.Max(0.001f, t * overshoot);
                }

                yield return null;
            }

            for (int i = 0; i < cards.Count; i++)
            {
                if (i != draggingIndex)
                {
                    cards[i].Root.localScale = Vector3.one;
                }
            }
        }

        private void EnsureVisuals()
        {
            if (root != null || gameConfig == null)
            {
                return;
            }

            root = new GameObject("OfferTray").transform;
            root.SetParent(transform, false);
            shelf = CreateRenderer(root, "Shelf", BattleGrid.GetRuntimeRoundedFillSprite(), new Color(0.114f, 0.078f, 0.259f, 0.45f), 1);
            meterTrack = CreateRenderer(root, "EnergyTrack", GetPixel(), new Color(0.114f, 0.078f, 0.259f, 1f), 2);
            meterFill = CreateRenderer(root, "EnergyFill", GetPixel(), Color.white, 3);

            int count = Mathf.Max(1, gameConfig.draftOfferCount);
            for (int i = 0; i < count; i++)
            {
                cards.Add(CreateCard(i));
            }
        }

        private Card CreateCard(int index)
        {
            float w = gameConfig.trayCardWidth * 0.92f;
            float h = gameConfig.trayCardHeight * 0.92f;
            Transform cardRoot = new GameObject($"OfferCard_{index}").transform;
            cardRoot.SetParent(root, false);

            SpriteRenderer plate = CreateRenderer(cardRoot, "Plate", BattleGrid.GetRuntimeRoundedFillSprite(), Color.white, 10);
            plate.transform.localScale = new Vector3(w, h, 1f);
            SpriteRenderer header = CreateRenderer(cardRoot, "Header", GetPixel(), Color.white, 11);
            header.transform.localPosition = new Vector3(-w * 0.42f, h * 0.36f, 0f);
            header.transform.localScale = new Vector3(w * 0.84f, h * 0.16f, 1f);

            float bean = h * 0.95f;
            Vector3 beanPos = new(0f, -h * 0.5f, 0f);
            SpriteRenderer body = CreateRenderer(cardRoot, "Body", null, Color.white, 12);
            SpriteRenderer prop = CreateRenderer(cardRoot, "Prop", null, Color.white, 13);
            SpriteRenderer face = CreateRenderer(cardRoot, "Face", null, Color.white, 14);
            foreach (SpriteRenderer layer in new[] { body, prop, face })
            {
                // Bean sprites are 1 world unit tall at PPU 256 with the pivot at the feet.
                layer.transform.localPosition = beanPos;
                layer.transform.localScale = Vector3.one * bean;
            }

            GameObject labelObject = new("Label");
            labelObject.transform.SetParent(cardRoot, false);
            labelObject.transform.localPosition = new Vector3(0f, h * 0.36f, 0f);
            TextMeshPro label = labelObject.AddComponent<TextMeshPro>();
            label.font = TMP_Settings.defaultFontAsset;
            label.fontSize = 1.5f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.outlineColor = new Color32(29, 20, 66, 255);
            label.outlineWidth = 0.2f;
            label.rectTransform.sizeDelta = new Vector2(w, h * 0.2f);
            label.sortingLayerID = UnityEngine.SortingLayer.NameToID(SortingLayer);

            cardRoot.localScale = Vector3.one * 0.001f;
            return new Card { Root = cardRoot, Plate = plate, Header = header, Body = body, Prop = prop, Face = face, Label = label };
        }

        private static SpriteRenderer CreateRenderer(Transform parent, string objectName, Sprite sprite, Color color, int order)
        {
            GameObject go = new(objectName);
            go.transform.SetParent(parent, false);
            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingLayerName = SortingLayer;
            renderer.sortingOrder = order;
            return renderer;
        }

        private static void SetSprite(SpriteRenderer renderer, Sprite sprite)
        {
            renderer.sprite = sprite;
        }

        private static Sprite pixel;

        /// <summary>1x1 white sprite pivoted at its left edge, so x scale is the width.</summary>
        private static Sprite GetPixel()
        {
            if (pixel != null)
            {
                return pixel;
            }

            Texture2D texture = new(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            pixel = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0f, 0.5f), 1f);
            pixel.name = "OfferTrayPixel";
            return pixel;
        }

        private bool TryGetPointer(out Vector3 world)
        {
            world = Vector3.zero;
            Vector2 screen;
            // On the release frame the touch is no longer pressed but its position is still valid.
            if (Touchscreen.current != null && (Touchscreen.current.primaryTouch.press.isPressed || Touchscreen.current.primaryTouch.press.wasReleasedThisFrame))
            {
                screen = Touchscreen.current.primaryTouch.position.ReadValue();
            }
            else if (Mouse.current != null)
            {
                screen = Mouse.current.position.ReadValue();
            }
            else
            {
                return false;
            }

            world = targetCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, Mathf.Abs(targetCamera.transform.position.z)));
            world.z = 0f;
            return true;
        }

        private bool TryGetPointerDown(out Vector3 world)
        {
            bool down = Touchscreen.current != null
                ? Touchscreen.current.primaryTouch.press.wasPressedThisFrame
                : Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
            world = Vector3.zero;
            return down && TryGetPointer(out world);
        }

        private bool TryGetPointerUp(out Vector3 world)
        {
            bool up = Touchscreen.current != null
                ? Touchscreen.current.primaryTouch.press.wasReleasedThisFrame
                : Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame;
            world = Vector3.zero;
            return up && TryGetPointer(out world);
        }
    }
}
