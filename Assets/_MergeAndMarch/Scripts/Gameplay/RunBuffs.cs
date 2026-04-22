using System;

namespace MergeAndMarch.Gameplay
{
    [Serializable]
    public class RunBuffs
    {
        public float attackMultiplier = 1f;
        public float hpMultiplier = 1f;
        public float knightAttackMultiplier = 1f;
        public float archerAttackMultiplier = 1f;
        public float mageAttackMultiplier = 1f;
        public float healerPowerMultiplier = 1f;
        public float bomberAttackMultiplier = 1f;
        public float bomberRadiusMultiplier = 1f;
        public float archerSpeedMultiplier = 1f;
        public float coinMultiplier = 1f;
        public float knightThornsDamage = 0f;
        public float mergeHealPercent = 0f;
        public int extraDeployCount = 0;
        public bool nextMergeBoosted = false;
        public float archerVsTankMultiplier = 1f;
        public bool knightCanHitFlyers = false;
    }
}
