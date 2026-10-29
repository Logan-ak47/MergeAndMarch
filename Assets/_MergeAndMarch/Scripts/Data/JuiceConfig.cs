using UnityEngine;

namespace MergeAndMarch.Data
{
    /// <summary>
    /// Every timing, amount and colour of the juice language, so feel can be tuned without
    /// code changes. Values implement the recipes in design/art/art-bible.md Section 2
    /// ("Juice &amp; FX language"). Distances given in px refer to the 1080-wide reference
    /// screen and are converted with <see cref="referencePixelsPerUnit"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "JuiceConfig", menuName = "Merge And March/Juice Config")]
    public class JuiceConfig : ScriptableObject
    {
        [Header("Accessibility")]
        [Tooltip("Reduce Motion: no shake, flashes capped, squash and confetti halved.")]
        public bool reduceMotion;
        public bool screenShakeEnabled = true;
        public bool damageNumbersEnabled = true;
        [Range(0f, 1f)] public float reducedMotionFlashCap = 0.3f;

        [Header("Reference")]
        [Tooltip("Screen px per world unit at the 1080-wide reference (measured, art bible 8.0).")]
        [Min(1f)] public float referencePixelsPerUnit = 189.5f;
        [Min(1)] public int maxLiveEffects = 128;

        [Header("Palette (art bible Section 4)")]
        public Color ink = new(0.114f, 0.078f, 0.259f, 1f);
        public Color gold = new(1f, 0.761f, 0.102f, 1f);
        public Color enemyRed = new(0.941f, 0.157f, 0.247f, 1f);
        public Color healMint = new(0.365f, 1f, 0.69f, 1f);
        public Color[] confettiColors =
        {
            new(1f, 0.761f, 0.102f, 1f),
            new(0.184f, 0.816f, 1f, 1f),
            new(1f, 0.435f, 0.82f, 1f),
            new(0.094f, 0.788f, 0.392f, 1f),
            Color.white
        };

        [Header("Idle")]
        [Range(0f, 0.2f)] public float idleBobPercent = 0.03f;
        [Min(0.05f)] public float idleBobPeriod = 1.2f;
        [Range(0f, 0.2f)] public float idleBreathAmount = 0.03f;
        [Min(0.1f)] public float blinkIntervalMin = 2f;
        [Min(0.1f)] public float blinkIntervalMax = 5f;
        [Min(0.01f)] public float blinkDuration = 0.1f;

        [Header("Enemy march")]
        [Range(0f, 20f)] public float marchTiltDegrees = 4f;
        [Min(0.05f)] public float marchHopPeriod = 0.5f;
        [Range(0f, 0.2f)] public float marchHopPercent = 0.04f;
        [Min(1f)] public float rusherMarchRate = 2f;
        [Range(0f, 45f)] public float flyerWingFlapDegrees = 15f;

        [Header("Troop attack")]
        public Vector2 attackAnticipation = new(0.9f, 1.1f);
        [Min(0.01f)] public float attackAnticipationDuration = 0.06f;
        public Vector2 attackRelease = new(1.1f, 0.9f);
        [Min(0.01f)] public float attackReleaseDuration = 0.08f;
        [Min(0.01f)] public float attackSettleDuration = 0.1f;
        [Range(0f, 0.5f)] public float propJabPercent = 0.1f;
        [Min(0.01f)] public float attackFaceDuration = 0.2f;

        [Header("Hit on enemy")]
        [Min(0.01f)] public float enemyHitFlashDuration = 0.05f;
        [Range(0f, 0.5f)] public float enemyRecoilPercent = 0.07f;
        public Vector2 enemyHitSquash = new(1.1f, 0.9f);
        [Min(0.01f)] public float enemyHitSquashDuration = 0.08f;
        [Min(0)] public int hitSparksMin = 3;
        [Min(0)] public int hitSparksMax = 5;
        [Min(0f)] public float hitSparkSpeed = 2.2f;
        [Min(0.01f)] public float hitSparkLifetime = 0.25f;
        [Min(0.01f)] public float hitSparkSize = 0.12f;

        [Header("Damage numbers")]
        [Min(1f)] public float damageNumberPopScale = 1.3f;
        [Min(0.01f)] public float damageNumberPopDuration = 0.12f;
        [Min(0f)] public float damageNumberRisePx = 30f;
        [Min(0.05f)] public float damageNumberLifetime = 0.5f;
        [Min(1)] public int damageNumbersMaxOnScreen = 12;
        [Min(1f)] public float damageNumberTierThreeScale = 1.3f;
        [Min(1f)] public float damageNumberFontSize = 40f;

        [Header("Enemy death")]
        public Vector2 enemyDeathSquash = new(1.3f, 0.6f);
        [Min(0.01f)] public float enemyDeathSquashDuration = 0.08f;
        [Min(0)] public int enemyDeathChunksMin = 6;
        [Min(0)] public int enemyDeathChunksMax = 8;
        [Min(0f)] public float enemyDeathChunkSpeed = 3f;
        [Min(0.01f)] public float enemyDeathChunkLifetime = 0.35f;
        [Min(0f)] public float tankDeathShakePx = 2f;
        [Min(0f)] public float tankDeathShakeDuration = 0.08f;

        [Header("Troop hit")]
        public Color troopHitTint = new(1f, 0.45f, 0.5f, 1f);
        [Min(0.01f)] public float troopHitTintDuration = 0.06f;
        [Range(0.5f, 1f)] public float troopHitSquash = 0.92f;
        [Min(0.01f)] public float troopHurtFaceDuration = 0.15f;
        [Min(0)] public int troopHitSparksMin = 2;
        [Min(0)] public int troopHitSparksMax = 3;

        [Header("Troop death")]
        [Min(1f)] public float troopDeathStretch = 1.2f;
        [Min(0.01f)] public float troopDeathDuration = 0.2f;
        [Min(0f)] public float troopDeathShakePx = 3f;
        [Min(0f)] public float troopDeathShakeDuration = 0.12f;

        [Header("Drag")]
        [Min(1f)] public float dragPickupScale = 1.15f;
        [Range(0f, 30f)] public float dragWobbleDegrees = 6f;
        [Min(0.1f)] public float dragWobbleFrequency = 3f;
        [Min(1f)] public float dragShadowGrow = 1.4f;
        [Range(1f, 1.3f)] public float highlightRimScale = 1.08f;
        [Min(0.1f)] public float highlightPulsePeriod = 0.3f;

        [Header("Merge resolve")]
        [Min(0.01f)] public float mergePullDuration = 0.08f;
        [Min(0.01f)] public float mergeFlashDuration = 0.12f;
        [Range(0.1f, 1f)] public float mergeSpawnScale = 0.6f;
        [Min(1f)] public float mergeOvershoot = 1.25f;
        [Min(0.01f)] public float mergeSettleDuration = 0.22f;
        [Min(0)] public int mergeConfettiMin = 16;
        [Min(0)] public int mergeConfettiMax = 24;
        [Min(0f)] public float mergeConfettiSpeed = 4f;
        [Min(0.01f)] public float mergeConfettiLifetime = 0.55f;
        [Min(0f)] public float mergeHitStop = 0.06f;
        [Min(0f)] public float mergeShakePx = 4f;
        [Min(0f)] public float mergeShakeDuration = 0.15f;
        public string mergeTierTwoLabel = "TIER 2!";

        [Header("Tier-3 creation")]
        [Min(1f)] public float tierThreeOvershoot = 1.35f;
        [Min(1f)] public float tierThreeSecondBounce = 1.08f;
        [Min(0f)] public float tierThreeHitStop = 0.1f;
        [Min(0f)] public float tierThreeShakePx = 6f;
        [Min(0f)] public float tierThreeShakeDuration = 0.2f;
        [Min(0.01f)] public float tierThreeRaysDuration = 0.4f;
        [Range(0f, 1f)] public float tierThreeGlowAlpha = 0.55f;
        [Min(1f)] public float tierThreeGlowScale = 1.12f;
        public string mergeTierThreeLabel = "TIER 3!";

        [Header("Deploy pop-in")]
        [Min(1f)] public float deployPeakScale = 1.2f;
        [Min(0.01f)] public float deployDuration = 0.18f;
        public Vector2 deployLandSquash = new(1.15f, 0.85f);
        [Min(0.01f)] public float deployLandDuration = 0.1f;

        [Header("Heal")]
        [Min(0.01f)] public float healFlashDuration = 0.12f;
        [Tooltip("Multiply tint on a healed troop; keeps the bean fully opaque.")]
        public Color healTint = new(0.72f, 1f, 0.84f, 1f);
        [Min(0.01f)] public float healRingSize = 1.5f;
        [Min(0.01f)] public float healRingDuration = 0.3f;

        [Header("Slow-mo overlay (art bible Sections 2 and 7)")]
        [Range(0f, 1f)] public float slowMoDimAlpha = 0.55f;
        [Range(0f, 1f)] public float slowMoVignetteAlpha = 0.5f;
        [Min(0.01f)] public float slowMoFadeIn = 0.1f;
        [Min(0.01f)] public float slowMoFadeOut = 0.15f;
        [Min(1f)] public float slowMoIconPx = 72f;
        [Tooltip("Distance of the hourglass below the top of the screen, in reference px (clears the HUD row).")]
        [Min(0f)] public float slowMoIconTopPx = 300f;

        [Header("Attack visuals")]
        [Min(0.01f)] public float knightSlashStartSize = 0.45f;
        [Min(0.01f)] public float knightSlashEndSize = 1.05f;
        [Min(0.01f)] public float knightSlashDuration = 0.2f;
        [Min(0.01f)] public float arrowSize = 0.42f;
        [Min(0.01f)] public float arrowFlightDuration = 0.18f;
        [Min(0.01f)] public float muzzlePopSize = 0.38f;
        [Min(0.01f)] public float mageWaveHeight = 0.9f;
        [Min(0.01f)] public float mageWaveDuration = 0.4f;

        [Header("Bomber explosion")]
        public Color bomberBlastColor = new(1f, 0.478f, 0f, 1f);
        public Color bomberBlastCore = new(1f, 0.882f, 0.302f, 1f);
        [Min(0f)] public float bomberShakePx = 5f;
        [Min(0f)] public float bomberShakeDuration = 0.25f;
        [Min(0f)] public float bomberKnockback = 0.3f;
        [Min(0.01f)] public float bomberKnockbackDuration = 0.2f;

        [Header("Boss entrance and defeat")]
        [Min(0f)] public float bossEntranceShakePx = 8f;
        [Min(0f)] public float bossEntranceShakeDuration = 0.3f;
        public Vector2 bossLandSquash = new(1.3f, 0.7f);
        public string bossLabel = "BOSS!";
        [Min(0f)] public float defeatShakePx = 6f;
        [Min(0f)] public float baseHitShakePx = 4f;
        [Min(0f)] public float baseHitShakeDuration = 0.18f;
        [Min(0f)] public float defeatShakeDuration = 0.4f;

        /// <summary>Converts a distance in 1080-reference screen px to world units.</summary>
        public float PxToWorld(float px)
        {
            return px / Mathf.Max(1f, referencePixelsPerUnit);
        }

        /// <summary>Scales an effect amount down when Reduce Motion is on.</summary>
        public float Motion(float amount)
        {
            return reduceMotion ? amount * 0.5f : amount;
        }

        /// <summary>Scales a squash/stretch vector toward 1 when Reduce Motion is on.</summary>
        public Vector2 Squash(Vector2 squash)
        {
            return reduceMotion ? Vector2.one + ((squash - Vector2.one) * 0.5f) : squash;
        }

        /// <summary>Returns a particle count, halved when Reduce Motion is on.</summary>
        public int Count(int min, int max)
        {
            int count = Random.Range(min, max + 1);
            return reduceMotion ? Mathf.Max(1, count / 2) : count;
        }

        /// <summary>Caps a flash opacity when Reduce Motion is on.</summary>
        public float FlashAlpha(float alpha)
        {
            return reduceMotion ? Mathf.Min(alpha, reducedMotionFlashCap) : alpha;
        }
    }
}
