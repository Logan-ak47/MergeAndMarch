using System;
using MergeAndMarch.Data;
using UnityEngine;

namespace MergeAndMarch.Gameplay
{
    /// <summary>
    /// The base behind the troop grid. Enemies that get past the troops walk into it and deal their
    /// EnemyData.baseDamage; the run is lost when it reaches zero (design decision 2026-10-27,
    /// replacing "any leak ends the run"). Draws a placeholder world-space bar under the grid until
    /// the UI pass replaces it.
    /// </summary>
    public class BaseHealth : MonoBehaviour
    {
        private const float BarHeight = 0.14f;
        private const float BarGap = 0.32f;

        private GameConfig config;
        private BattleGrid battleGrid;
        private SpriteRenderer track;
        private SpriteRenderer fill;
        private SpriteRenderer flash;
        private float flashTimer;
        private float displayedRatio = 1f;

        public float MaxHP { get; private set; } = 100f;
        public float CurrentHP { get; private set; } = 100f;
        public bool IsDestroyed => CurrentHP <= 0.01f;

        /// <summary>Raised after damage with (damage, remaining HP).</summary>
        public event Action<float, float> Damaged;

        public void Initialize(GameConfig gameConfig, BattleGrid grid)
        {
            config = gameConfig;
            battleGrid = grid;
            EnsureVisuals();
            ResetHealth();
        }

        public void ResetHealth()
        {
            MaxHP = config != null ? Mathf.Max(1f, config.baseMaxHP) : 100f;
            CurrentHP = MaxHP;
            displayedRatio = 1f;
            UpdateBar();
        }

        /// <summary>Applies an escaped enemy's damage and plays the hit feedback.</summary>
        public void TakeHit(float damage, Vector3 hitPosition)
        {
            if (IsDestroyed || damage <= 0f)
            {
                return;
            }

            CurrentHP = Mathf.Max(0f, CurrentHP - damage);
            flashTimer = 0.25f;
            JuiceFx.Instance?.BaseHit(hitPosition, damage);
            Damaged?.Invoke(damage, CurrentHP);
        }

        private void LateUpdate()
        {
            if (fill == null)
            {
                return;
            }

            float target = MaxHP > 0f ? CurrentHP / MaxHP : 0f;
            displayedRatio = Mathf.MoveTowards(displayedRatio, target, Time.unscaledDeltaTime * 1.5f);
            flashTimer = Mathf.Max(0f, flashTimer - Time.unscaledDeltaTime);
            UpdateBar();
        }

        private void EnsureVisuals()
        {
            if (track != null)
            {
                return;
            }

            Sprite white = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 4f, 4f), new Vector2(0f, 0.5f), 4f);
            track = CreateBar("BaseTrack", white, 20);
            fill = CreateBar("BaseFill", white, 21);
            flash = CreateBar("BaseFlash", white, 22);
        }

        private SpriteRenderer CreateBar(string barName, Sprite sprite, int order)
        {
            GameObject bar = new(barName);
            bar.transform.SetParent(transform, false);
            SpriteRenderer renderer = bar.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingLayerName = "Grid";
            renderer.sortingOrder = order;
            return renderer;
        }

        private void UpdateBar()
        {
            if (track == null || battleGrid == null)
            {
                return;
            }

            Bounds grid = battleGrid.GetGridWorldBounds();
            float width = grid.size.x;
            Vector3 origin = new(grid.min.x, grid.min.y - BarGap, 0f);
            JuiceConfig juice = JuiceFx.Instance != null ? JuiceFx.Instance.Config : null;
            Color ink = juice != null ? juice.ink : new Color(0.114f, 0.078f, 0.259f, 1f);
            Color danger = juice != null ? juice.enemyRed : new Color(0.941f, 0.157f, 0.247f, 1f);

            Place(track, origin, width, ink);
            float ratio = Mathf.Clamp01(displayedRatio);
            Place(fill, origin, width * ratio, ratio > 0.3f ? Color.white : danger);
            Color flashColor = danger;
            flashColor.a = flashTimer / 0.25f * 0.8f;
            Place(flash, origin, width * ratio, flashColor);
            flash.enabled = flashTimer > 0f;
        }

        private static void Place(SpriteRenderer renderer, Vector3 origin, float width, Color color)
        {
            renderer.transform.position = origin;
            renderer.transform.localScale = new Vector3(Mathf.Max(0f, width), BarHeight, 1f);
            renderer.color = color;
        }
    }
}
