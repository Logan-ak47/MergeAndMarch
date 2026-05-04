using MergeAndMarch.Data;
using System.Collections.Generic;
using UnityEngine;

namespace MergeAndMarch.Gameplay
{
    public class BattleGrid : MonoBehaviour
    {
        private static Sprite runtimeSquareSprite;
        private static Sprite runtimeSlotOutlineSprite;
        private static Sprite runtimeBackgroundGradientSprite;

        [SerializeField] private GameConfig config;
        [SerializeField] private Transform slotVisualRoot;
        [SerializeField] private Transform laneVisualRoot;

        private Troop[,] occupants;
        private SpriteRenderer[,] slotVisuals;

        public GameConfig Config => config;

        private void Awake()
        {
            EnsureOccupantArray();
            EnsureVisualRoots();
        }

        private void Start()
        {
            EnsureVisualRoots();
            EnsureGradientBackground();
            ResolveExistingSlotVisuals();
            RebuildLaneVisuals();
            RebuildOccupantsFromScene();
            UpdateSlotVisuals();
        }

        private void LateUpdate()
        {
            UpdateSlotVisuals();
        }

        public void SetConfig(GameConfig gameConfig)
        {
            config = gameConfig;
            EnsureOccupantArray();
        }

        public void SetSlotVisualRoot(Transform root)
        {
            slotVisualRoot = root;
        }

        public void SetLaneVisualRoot(Transform root)
        {
            laneVisualRoot = root;
        }

        public Vector3 GetSlotWorldPosition(int column, int row)
        {
            if (config == null)
            {
                return transform.position;
            }

            float x = config.gridOffset.x + (column * config.cellSize);
            float y = config.gridOffset.y + ((config.rows - 1 - row) * config.cellSize);
            return transform.position + new Vector3(x, y, 0f);
        }

        public Bounds GetGridWorldBounds()
        {
            if (config == null)
            {
                return new Bounds(transform.position, Vector3.one);
            }

            float width = config.columns * config.cellSize;
            float height = config.rows * config.cellSize;

            float left = transform.position.x + config.gridOffset.x - (config.cellSize * 0.5f);
            float top = transform.position.y + config.gridOffset.y + ((config.rows - 1) * config.cellSize) + (config.cellSize * 0.5f);

            Vector3 center = new(left + (width * 0.5f), top - (height * 0.5f), 0f);
            return new Bounds(center, new Vector3(width, height, 0f));
        }

        public bool IsWithinBounds(int column, int row)
        {
            return config != null && column >= 0 && row >= 0 && column < config.columns && row < config.rows;
        }

        public Troop GetTroopAt(int column, int row)
        {
            if (!IsWithinBounds(column, row))
            {
                return null;
            }

            EnsureOccupantArray();
            return occupants[column, row];
        }

        public void ClearOccupants()
        {
            EnsureOccupantArray();

            for (int column = 0; column < config.columns; column++)
            {
                for (int row = 0; row < config.rows; row++)
                {
                    occupants[column, row] = null;
                }
            }
        }

        public void RebuildOccupantsFromScene()
        {
            if (config == null)
            {
                return;
            }

            ClearOccupants();
            Troop[] troops = FindObjectsByType<Troop>(FindObjectsSortMode.None);
            for (int i = 0; i < troops.Length; i++)
            {
                Troop troop = troops[i];
                if (troop == null || !IsWithinBounds(troop.Column, troop.Row))
                {
                    continue;
                }

                RegisterTroop(troop, troop.Column, troop.Row, moveToSlot: false);
            }
        }

        public bool RegisterTroop(Troop troop, int column, int row, bool moveToSlot = true)
        {
            if (troop == null || !IsWithinBounds(column, row))
            {
                return false;
            }

            EnsureOccupantArray();

            Troop existing = occupants[column, row];
            if (existing != null && existing != troop)
            {
                return false;
            }

            if (IsWithinBounds(troop.Column, troop.Row) && occupants[troop.Column, troop.Row] == troop)
            {
                occupants[troop.Column, troop.Row] = null;
            }

            troop.SetBattleGrid(this);
            occupants[column, row] = troop;

            if (moveToSlot)
            {
                troop.SetGridPosition(GetSlotWorldPosition(column, row), column, row);
            }
            else
            {
                troop.SetGridPosition(troop.transform.position, column, row);
            }

            return true;
        }

        public void RemoveTroop(Troop troop)
        {
            if (troop == null)
            {
                return;
            }

            EnsureOccupantArray();

            if (IsWithinBounds(troop.Column, troop.Row) && occupants[troop.Column, troop.Row] == troop)
            {
                occupants[troop.Column, troop.Row] = null;
            }
        }


        public void GetEmptySlots(List<Vector2Int> results)
        {
            if (results == null)
            {
                return;
            }

            results.Clear();
            EnsureOccupantArray();

            if (config == null || occupants == null)
            {
                return;
            }

            for (int column = 0; column < config.columns; column++)
            {
                for (int row = 0; row < config.rows; row++)
                {
                    if (occupants[column, row] == null)
                    {
                        results.Add(new Vector2Int(column, row));
                    }
                }
            }
        }
        public void GetTroops(List<Troop> results)
        {
            if (results == null)
            {
                return;
            }

            results.Clear();
            EnsureOccupantArray();

            if (config == null || occupants == null)
            {
                return;
            }

            for (int column = 0; column < config.columns; column++)
            {
                for (int row = 0; row < config.rows; row++)
                {
                    Troop troop = occupants[column, row];
                    if (troop != null)
                    {
                        results.Add(troop);
                    }
                }
            }
        }

        public bool HasAnyTroops()
        {
            EnsureOccupantArray();

            if (config == null || occupants == null)
            {
                return false;
            }

            for (int column = 0; column < config.columns; column++)
            {
                for (int row = 0; row < config.rows; row++)
                {
                    if (occupants[column, row] != null)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public void ResetTroopsForWaveStart()
        {
            EnsureOccupantArray();

            if (config == null || occupants == null)
            {
                return;
            }

            for (int column = 0; column < config.columns; column++)
            {
                for (int row = 0; row < config.rows; row++)
                {
                    occupants[column, row]?.ResetForWaveStart();
                }
            }
        }

        public bool TryGetNearestSlot(Vector3 worldPosition, out int column, out int row)
        {
            column = -1;
            row = -1;

            if (config == null)
            {
                return false;
            }

            float minX = transform.position.x + config.gridOffset.x - (config.cellSize * 0.5f);
            float maxX = minX + (config.columns * config.cellSize);
            float minY = transform.position.y + config.gridOffset.y - (config.cellSize * 0.5f);
            float maxY = minY + (config.rows * config.cellSize);

            if (worldPosition.x < minX || worldPosition.x > maxX || worldPosition.y < minY || worldPosition.y > maxY)
            {
                return false;
            }

            float localX = worldPosition.x - (transform.position.x + config.gridOffset.x);
            float localY = worldPosition.y - (transform.position.y + config.gridOffset.y);

            column = Mathf.Clamp(Mathf.RoundToInt(localX / config.cellSize), 0, config.columns - 1);
            int invertedRow = Mathf.Clamp(Mathf.RoundToInt(localY / config.cellSize), 0, config.rows - 1);
            row = (config.rows - 1) - invertedRow;

            return IsWithinBounds(column, row);
        }

        public void RebuildSlotVisuals(Sprite slotSprite)
        {
            if (config == null || slotVisualRoot == null)
            {
                return;
            }

            ClearVisualChildren(slotVisualRoot);
            EnsureOccupantArray();

            for (int row = 0; row < config.rows; row++)
            {
                for (int column = 0; column < config.columns; column++)
                {
                    GameObject slot = new($"Slot_{column}_{row}");
                    slot.transform.SetParent(slotVisualRoot, false);
                    slot.transform.position = GetSlotWorldPosition(column, row);
                    slot.transform.localScale = Vector3.one * (config.cellSize * config.slotVisualScale);

                    SpriteRenderer renderer = slot.AddComponent<SpriteRenderer>();
                    renderer.sprite = GetRuntimeSlotOutlineSprite();
                    renderer.color = config.slotTint;
                    renderer.sortingLayerName = "Grid";
                    renderer.sortingOrder = 0;
                    slotVisuals[column, row] = renderer;
                }
            }
        }

        public void RebuildLaneVisuals()
        {
            if (config == null)
            {
                return;
            }

            EnsureVisualRoots();
            if (laneVisualRoot == null)
            {
                return;
            }

            ClearVisualChildren(laneVisualRoot);

            Sprite sprite = GetRuntimeSquareSprite();
            Bounds bounds = GetGridWorldBounds();
            float lineBottom = bounds.max.y + (config.cellSize * 0.2f);
            float lineTop = lineBottom + config.laneGuideHeight;
            float lineHeight = Mathf.Max(0.5f, lineTop - lineBottom);
            float lineWidth = Mathf.Max(0.04f, config.cellSize * config.laneGuideWidthScale);
            float markerSize = Mathf.Max(0.08f, config.cellSize * config.laneGuideMarkerScale);
            const int segmentCount = 3;

            for (int column = 0; column < config.columns; column++)
            {
                Vector3 laneCenter = GetSlotWorldPosition(column, 0);

                for (int segment = 0; segment < segmentCount; segment++)
                {
                    float segmentHeight = lineHeight / segmentCount;
                    float segmentCenterY = lineBottom + (segmentHeight * (segment + 0.5f));
                    float fade = 1f - (segment * 0.28f);

                    GameObject line = new($"LaneGuide_{column}_{segment}");
                    line.transform.SetParent(laneVisualRoot, false);
                    line.transform.position = new Vector3(laneCenter.x, segmentCenterY, 0f);
                    line.transform.localScale = new Vector3(lineWidth, segmentHeight * 0.92f, 1f);

                    SpriteRenderer lineRenderer = line.AddComponent<SpriteRenderer>();
                    lineRenderer.sprite = sprite;
                    lineRenderer.color = WithAlpha(config.laneGuideTint, config.laneGuideTint.a * fade);
                    lineRenderer.sortingLayerName = "Grid";
                    lineRenderer.sortingOrder = -2;
                }

                GameObject marker = new($"LaneMarker_{column}");
                marker.transform.SetParent(laneVisualRoot, false);
                marker.transform.position = new Vector3(laneCenter.x, lineTop, 0f);
                marker.transform.localScale = Vector3.one * markerSize;

                SpriteRenderer markerRenderer = marker.AddComponent<SpriteRenderer>();
                markerRenderer.sprite = sprite;
                markerRenderer.color = WithAlpha(config.laneGuideMarkerTint, config.laneGuideMarkerTint.a * 0.7f);
                markerRenderer.sortingLayerName = "Grid";
                markerRenderer.sortingOrder = -1;
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (config == null)
            {
                return;
            }

            Gizmos.color = new Color(1f, 1f, 1f, 0.35f);
            Bounds bounds = GetGridWorldBounds();
            Gizmos.DrawWireCube(bounds.center, bounds.size);
        }

        private void EnsureGradientBackground()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            const string backgroundName = "RuntimeGradientBackground";
            Transform existing = camera.transform.Find(backgroundName);
            SpriteRenderer backgroundRenderer = existing != null ? existing.GetComponent<SpriteRenderer>() : null;
            if (backgroundRenderer == null)
            {
                GameObject background = new(backgroundName);
                background.transform.SetParent(camera.transform, false);
                backgroundRenderer = background.AddComponent<SpriteRenderer>();
            }

            float height = camera.orthographicSize * 2f;
            float width = height * camera.aspect;
            backgroundRenderer.sprite = GetRuntimeBackgroundGradientSprite();
            backgroundRenderer.color = Color.white;
            backgroundRenderer.sortingLayerName = "Background";
            backgroundRenderer.sortingOrder = -100;
            backgroundRenderer.transform.localPosition = new Vector3(0f, 0f, 20f);
            backgroundRenderer.transform.localScale = new Vector3(width, height, 1f);
        }

        private void UpdateSlotVisuals()
        {
            if (config == null || slotVisuals == null || occupants == null)
            {
                return;
            }

            float pulse = 0.5f + (Mathf.Sin(Time.time * (Mathf.PI * 2f / 1.5f)) * 0.5f);
            float alpha = Mathf.Lerp(0.2f, 0.35f, pulse);
            for (int column = 0; column < config.columns; column++)
            {
                for (int row = 0; row < config.rows; row++)
                {
                    SpriteRenderer renderer = slotVisuals[column, row];
                    if (renderer == null)
                    {
                        continue;
                    }

                    renderer.enabled = occupants[column, row] == null;
                    renderer.color = WithAlpha(config.slotTint, alpha);
                }
            }
        }

        private void ResolveExistingSlotVisuals()
        {
            if (config == null || slotVisualRoot == null)
            {
                return;
            }

            EnsureOccupantArray();
            for (int column = 0; column < config.columns; column++)
            {
                for (int row = 0; row < config.rows; row++)
                {
                    Transform child = slotVisualRoot.Find($"Slot_{column}_{row}");
                    if (child != null)
                    {
                        slotVisuals[column, row] = child.GetComponent<SpriteRenderer>();
                        if (slotVisuals[column, row] != null)
                        {
                            slotVisuals[column, row].sprite = GetRuntimeSlotOutlineSprite();
                        }
                    }
                }
            }
        }

        private void EnsureOccupantArray()
        {
            if (config == null)
            {
                return;
            }

            if (occupants == null || occupants.GetLength(0) != config.columns || occupants.GetLength(1) != config.rows)
            {
                occupants = new Troop[config.columns, config.rows];
            }

            if (slotVisuals == null || slotVisuals.GetLength(0) != config.columns || slotVisuals.GetLength(1) != config.rows)
            {
                slotVisuals = new SpriteRenderer[config.columns, config.rows];
            }
        }

        private void EnsureVisualRoots()
        {
            if (slotVisualRoot == null)
            {
                Transform existingSlots = transform.Find("SlotVisuals");
                if (existingSlots != null)
                {
                    slotVisualRoot = existingSlots;
                }
            }

            if (laneVisualRoot == null)
            {
                Transform existingLanes = transform.Find("LaneGuides");
                if (existingLanes != null)
                {
                    laneVisualRoot = existingLanes;
                }
                else
                {
                    GameObject lanes = new("LaneGuides");
                    lanes.transform.SetParent(transform, false);
                    laneVisualRoot = lanes.transform;
                }
            }
        }

        private void ClearVisualChildren(Transform root)
        {
            if (root == null)
            {
                return;
            }

            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Transform child = root.GetChild(i);
                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }

        private static Sprite GetRuntimeSquareSprite()
        {
            if (runtimeSquareSprite != null)
            {
                return runtimeSquareSprite;
            }

            Texture2D texture = new(16, 16, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[16 * 16];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.white;
            }

            texture.SetPixels(pixels);
            texture.Apply();
            runtimeSquareSprite = Sprite.Create(texture, new Rect(0f, 0f, 16f, 16f), new Vector2(0.5f, 0.5f), 100f);
            runtimeSquareSprite.name = "BattleGridRuntimeSquare";
            return runtimeSquareSprite;
        }

        private static Sprite GetRuntimeSlotOutlineSprite()
        {
            if (runtimeSlotOutlineSprite != null)
            {
                return runtimeSlotOutlineSprite;
            }

            const int size = 64;
            const float radius = 10f;
            const float border = 4f;
            Texture2D texture = new(size, size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 point = new(x + 0.5f, y + 0.5f);
                    bool outer = IsInsideRoundedRect(point, size, radius);
                    bool inner = IsInsideRoundedRect(point, size - (border * 2f), Mathf.Max(0f, radius - border), border);
                    pixels[(y * size) + x] = outer && !inner ? Color.white : Color.clear;
                }
            }

            texture.SetPixels(pixels);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.Apply();
            runtimeSlotOutlineSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            runtimeSlotOutlineSprite.name = "BattleGridSlotOutline";
            return runtimeSlotOutlineSprite;
        }

        private static Sprite GetRuntimeBackgroundGradientSprite()
        {
            if (runtimeBackgroundGradientSprite != null)
            {
                return runtimeBackgroundGradientSprite;
            }

            const int height = 512;
            Texture2D texture = new(1, height, TextureFormat.RGBA32, false);
            Color top = new(0.051f, 0.051f, 0.102f, 1f);
            Color bottom = new(0.122f, 0.122f, 0.208f, 1f);
            for (int y = 0; y < height; y++)
            {
                float t = y / (height - 1f);
                texture.SetPixel(0, y, Color.Lerp(bottom, top, t));
            }

            texture.Apply();
            runtimeBackgroundGradientSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, height), new Vector2(0.5f, 0.5f), height);
            runtimeBackgroundGradientSprite.name = "RuntimeBackgroundGradient";
            return runtimeBackgroundGradientSprite;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private static bool IsInsideRoundedRect(Vector2 point, float size, float radius, float inset = 0f)
        {
            float min = inset;
            float max = inset + size;
            if (point.x < min || point.x > max || point.y < min || point.y > max)
            {
                return false;
            }

            float clampedX = Mathf.Clamp(point.x, min + radius, max - radius);
            float clampedY = Mathf.Clamp(point.y, min + radius, max - radius);
            Vector2 nearest = new(clampedX, clampedY);
            return (point - nearest).sqrMagnitude <= radius * radius;
        }
    }
}
