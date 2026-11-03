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
        private static Color runtimeBackgroundTop;
        private static Color runtimeBackgroundBottom;

        [SerializeField] private GameConfig config;
        [SerializeField] private Transform slotVisualRoot;
        [SerializeField] private Transform laneVisualRoot;

        private Troop[,] occupants;
        private readonly List<SpriteRenderer> laneStrips = new();
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
            UpdateLaneThreats();
        }

        /// <summary>
        /// Threat telegraph (design/specs/lane-defence.md, rule 3): a lane with an enemy in it and no
        /// troop to stop it pulses red.
        /// </summary>
        private void UpdateLaneThreats()
        {
            if (config == null || laneStrips.Count == 0)
            {
                return;
            }

            float bottom = GetGridWorldBounds().min.y;
            float pulse = 0.5f + (0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / Mathf.Max(0.05f, config.laneThreatPulsePeriod)));
            IReadOnlyList<Enemy> enemies = Enemy.ActiveEnemies;
            for (int column = 0; column < laneStrips.Count && column < config.columns; column++)
            {
                SpriteRenderer strip = laneStrips[column];
                if (strip == null)
                {
                    continue;
                }

                bool defended = false;
                for (int row = 0; row < config.rows && !defended; row++)
                {
                    Troop troop = GetTroopAt(column, row);
                    defended = troop != null && troop.IsCombatActive;
                }

                bool threatened = false;
                for (int i = 0; !defended && i < enemies.Count && !threatened; i++)
                {
                    Enemy enemy = enemies[i];
                    threatened = enemy != null && enemy.IsAlive && enemy.LaneColumn == column && enemy.transform.position.y > bottom;
                }

                strip.color = threatened ? Color.Lerp(config.laneGuideTint, config.laneThreatTint, pulse) : config.laneGuideTint;
            }
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
            laneStrips.Clear();

            // Lanes as ground strips from the spawn gate down to the grid, so the battlefield reads as
            // a place (art-director review 2026-11-03), replacing the faint dotted guides.
            Sprite strip = GetRuntimeRoundedFillSprite();
            Bounds bounds = GetGridWorldBounds();
            // Strips run through the grid down to the egg so a leak visibly follows its lane.
            float bottom = bounds.min.y - config.eggOffset;
            float gateY = bounds.max.y + config.enemySpawnYOffset + config.laneGateOffset;
            float height = Mathf.Max(0.5f, gateY - bottom);
            float laneWidth = config.cellSize * config.laneStripWidthScale;

            for (int column = 0; column < config.columns; column++)
            {
                Vector3 laneCenter = GetSlotWorldPosition(column, 0);
                SpriteRenderer lane = CreateLaneRenderer($"LaneStrip_{column}", strip, config.laneGuideTint, -3);
                lane.transform.position = new Vector3(laneCenter.x, bottom + (height * 0.5f), 0f);
                lane.transform.localScale = new Vector3(laneWidth, height, 1f);
                laneStrips.Add(lane);

                SpriteRenderer portal = CreateLaneRenderer($"LanePortal_{column}", strip, config.laneGateMouthTint, -1);
                portal.transform.position = new Vector3(laneCenter.x, gateY, 0f);
                portal.transform.localScale = new Vector3(laneWidth * 0.8f, config.laneGateHeight * 0.6f, 1f);
            }

            SpriteRenderer gate = CreateLaneRenderer("SpawnGate", strip, config.laneGateTint, -2);
            gate.transform.position = new Vector3(bounds.center.x, gateY, 0f);
            gate.transform.localScale = new Vector3(bounds.size.x + (config.cellSize * 0.3f), config.laneGateHeight, 1f);
        }

        private SpriteRenderer CreateLaneRenderer(string objectName, Sprite sprite, Color color, int order)
        {
            GameObject go = new(objectName);
            go.transform.SetParent(laneVisualRoot, false);
            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingLayerName = "Grid";
            renderer.sortingOrder = order;
            return renderer;
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

            // A little oversized so screen shake never reveals the camera's clear colour.
            float height = camera.orthographicSize * 2f * 1.1f;
            float width = height * camera.aspect;
            backgroundRenderer.sprite = config != null
                ? GetRuntimeBackgroundGradientSprite(config.arenaTopColor, config.arenaBottomColor)
                : GetRuntimeBackgroundGradientSprite(new Color(0.051f, 0.051f, 0.102f, 1f), new Color(0.122f, 0.122f, 0.208f, 1f));
            backgroundRenderer.color = Color.white;
            backgroundRenderer.sortingLayerName = "Background";
            backgroundRenderer.sortingOrder = -100;
            backgroundRenderer.transform.localPosition = new Vector3(0f, 0f, 20f);

            // The gradient sprite is 1 px wide, so scale by its real bounds rather than assuming 1 unit.
            Vector3 spriteSize = backgroundRenderer.sprite.bounds.size;
            backgroundRenderer.transform.localScale = new Vector3(
                width / Mathf.Max(0.0001f, spriteSize.x),
                height / Mathf.Max(0.0001f, spriteSize.y),
                1f);
        }

        private void UpdateSlotVisuals()
        {
            if (config == null || slotVisuals == null || occupants == null)
            {
                return;
            }

            float pulse = 0.5f + (Mathf.Sin(Time.time * (Mathf.PI * 2f / 1.5f)) * 0.5f);
            float alpha = Mathf.Lerp(config.slotTint.a * 0.85f, config.slotTint.a * 1.15f, pulse);
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

        internal static Sprite GetRuntimeSlotOutlineSprite()
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

        private static Sprite runtimeRoundedFillSprite;

        /// <summary>Filled rounded rectangle, 1 world unit square, centre pivot (tray shelf and cards).</summary>
        internal static Sprite GetRuntimeRoundedFillSprite()
        {
            if (runtimeRoundedFillSprite != null)
            {
                return runtimeRoundedFillSprite;
            }

            const int size = 64;
            const float radius = 14f;
            Texture2D texture = new(size, size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    pixels[(y * size) + x] = IsInsideRoundedRect(new Vector2(x + 0.5f, y + 0.5f), size, radius) ? Color.white : Color.clear;
                }
            }

            texture.SetPixels(pixels);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.Apply();
            runtimeRoundedFillSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            runtimeRoundedFillSprite.name = "RuntimeRoundedFill";
            return runtimeRoundedFillSprite;
        }

        /// <summary>Arena gradient (art bible Sections 4 and 6), rebuilt if the configured colours change.</summary>
        private static Sprite GetRuntimeBackgroundGradientSprite(Color top, Color bottom)
        {
            if (runtimeBackgroundGradientSprite != null && runtimeBackgroundTop == top && runtimeBackgroundBottom == bottom)
            {
                return runtimeBackgroundGradientSprite;
            }

            runtimeBackgroundTop = top;
            runtimeBackgroundBottom = bottom;
            const int height = 512;
            Texture2D texture = new(1, height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp
            };
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
