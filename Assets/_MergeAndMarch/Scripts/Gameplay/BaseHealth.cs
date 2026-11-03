using System;
using MergeAndMarch.Data;
using UnityEngine;

namespace MergeAndMarch.Gameplay
{
    /// <summary>
    /// The base behind the troop grid. Enemies that get past the troops walk into it and deal their
    /// EnemyData.baseDamage; the run is lost when it reaches zero (design decision 2026-10-27,
    /// replacing "any leak ends the run"). Shown as the protected egg under the grid, which cracks
    /// as HP falls, with a small HP pill on its nest (direction 2026-11-03).
    /// </summary>
    public class BaseHealth : MonoBehaviour
    {
        private const float BarHeight = 0.14f;
        private const float HitDuration = 0.25f;

        private GameConfig config;
        private BattleGrid battleGrid;
        private SpriteRenderer track;
        private SpriteRenderer fill;
        private SpriteRenderer flash;
        private SpriteRenderer egg;
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

        /// <summary>Restores base health, e.g. on wave clear (GameConfig.baseHealOnWaveClear).</summary>
        public void Heal(float amount)
        {
            if (IsDestroyed || amount <= 0f)
            {
                return;
            }

            CurrentHP = Mathf.Min(MaxHP, CurrentHP + amount);
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
            flashTimer = HitDuration;
            // Leaks land on the egg so the player sees what was hit (design/specs/lane-defence.md, rule 3).
            JuiceFx.Instance?.BaseHit(egg != null ? egg.transform.position : hitPosition, damage);
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
            egg = CreateBar("Egg", null, 19);
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
            float eggSize = config != null ? config.eggSize : 0.95f;
            Vector3 eggCentre = new(grid.center.x, grid.min.y - (config != null ? config.eggOffset : 0.5f), 0f);
            UpdateEgg(eggCentre, eggSize);

            float width = eggSize * 1.1f;
            Vector3 origin = new(eggCentre.x - (width * 0.5f), eggCentre.y - (eggSize * 0.36f), 0f);
            JuiceConfig juice = JuiceFx.Instance != null ? JuiceFx.Instance.Config : null;
            Color ink = juice != null ? juice.ink : new Color(0.114f, 0.078f, 0.259f, 1f);
            Color danger = juice != null ? juice.enemyRed : new Color(0.941f, 0.157f, 0.247f, 1f);

            Place(track, origin, width, ink);
            float ratio = Mathf.Clamp01(displayedRatio);
            Place(fill, origin, width * ratio, ratio > 0.3f ? Color.white : danger);
            Color flashColor = danger;
            flashColor.a = flashTimer / HitDuration * 0.8f;
            Place(flash, origin, width * ratio, flashColor);
            flash.enabled = flashTimer > 0f;
        }

        /// <summary>Picks the crack state from HP and wobbles and reddens the egg while a hit is fresh.</summary>
        private void UpdateEgg(Vector3 centre, float size)
        {
            BeanArtSet art = JuiceFx.Instance != null ? JuiceFx.Instance.Art : null;
            float ratio = MaxHP > 0f ? CurrentHP / MaxHP : 0f;
            int state = 0;
            float[] cracks = config != null ? config.eggCrackRatios : null;
            for (int i = 0; cracks != null && i < cracks.Length; i++)
            {
                if (ratio <= cracks[i])
                {
                    state = i + 1;
                }
            }

            if (art != null && art.eggStates != null && art.eggStates.Length > 0)
            {
                egg.sprite = art.eggStates[Mathf.Clamp(state, 0, art.eggStates.Length - 1)];
            }

            float hit = flashTimer / HitDuration;
            egg.transform.position = centre;
            egg.transform.localScale = new Vector3(size * (1f + (0.08f * hit)), size * (1f - (0.08f * hit)), 1f);
            egg.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.unscaledTime * 60f) * 8f * hit);
            egg.color = Color.Lerp(Color.white, new Color(1f, 0.55f, 0.6f, 1f), hit);
        }

        private static void Place(SpriteRenderer renderer, Vector3 origin, float width, Color color)
        {
            renderer.transform.position = origin;
            renderer.transform.localScale = new Vector3(Mathf.Max(0f, width), BarHeight, 1f);
            renderer.color = color;
        }
    }
}
