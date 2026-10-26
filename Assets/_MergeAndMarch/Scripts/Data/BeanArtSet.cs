using UnityEngine;

namespace MergeAndMarch.Data
{
    /// <summary>Face states shared by every bean (design/art/art-bible.md Section 5).</summary>
    public enum BeanFace
    {
        Idle,
        Blink,
        Attack,
        Hurt,
        Dizzy,
        Happy
    }

    /// <summary>
    /// Shared sprites for the bean art direction: the face set, tier badges and the
    /// white flat-shape FX sprites that are tinted at runtime (art bible Sections 2, 5, 8).
    /// Per-unit sprites live on <see cref="TroopData"/> and <see cref="EnemyData"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "BeanArtSet", menuName = "Merge And March/Bean Art Set")]
    public class BeanArtSet : ScriptableObject
    {
        [Header("Ally faces")]
        public Sprite allyIdle;
        public Sprite allyBlink;
        public Sprite allyAttack;
        public Sprite allyHurt;
        public Sprite allyDizzy;
        public Sprite allyHappy;

        [Header("Enemy faces")]
        public Sprite enemyIdle;
        public Sprite enemyBlink;
        public Sprite enemyHurt;
        public Sprite enemyDizzy;

        [Header("Tier badges (1, 2, 3)")]
        public Sprite[] tierBadges = new Sprite[3];

        [Header("FX shapes (white, tinted at runtime)")]
        public Sprite fxCircle;
        public Sprite fxStar;
        public Sprite fxSparkle;
        public Sprite fxConfetti;
        public Sprite fxChunk;
        public Sprite fxRing;
        public Sprite fxPuff;
        public Sprite fxRays;
        public Sprite fxGlow;
        public Sprite fxShadow;
        public Sprite fxCoin;

        /// <summary>Returns the face sprite for a state; enemies fall back to the closest state they have.</summary>
        public Sprite GetFace(BeanFace face, bool enemy)
        {
            if (enemy)
            {
                return face switch
                {
                    BeanFace.Blink => enemyBlink,
                    BeanFace.Hurt => enemyHurt,
                    BeanFace.Dizzy => enemyDizzy,
                    _ => enemyIdle
                };
            }

            return face switch
            {
                BeanFace.Blink => allyBlink,
                BeanFace.Attack => allyAttack,
                BeanFace.Hurt => allyHurt,
                BeanFace.Dizzy => allyDizzy,
                BeanFace.Happy => allyHappy,
                _ => allyIdle
            };
        }

        /// <summary>Returns the badge sprite for tier 1–3, or null if it is missing.</summary>
        public Sprite GetBadge(int tier)
        {
            int index = Mathf.Clamp(tier, 1, 3) - 1;
            return tierBadges != null && index < tierBadges.Length ? tierBadges[index] : null;
        }
    }
}
