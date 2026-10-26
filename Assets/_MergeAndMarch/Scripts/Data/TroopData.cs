using UnityEngine;

namespace MergeAndMarch.Data
{
    [CreateAssetMenu(fileName = "TroopData", menuName = "Merge And March/Troop Data")]
    public class TroopData : ScriptableObject
    {
        public TroopType troopType;
        public string displayName = "Troop";
        public Color troopColor = Color.white;
        [Min(1f)] public float baseHP = 50f;
        [Min(0f)] public float baseAttack = 10f;
        [Min(0.05f)] public float attackInterval = 1f;
        public TroopTargeting targeting = TroopTargeting.Melee;
        [Min(0f)] public float supportPower = 0f;
        public Sprite[] tierSprites = new Sprite[3];
        public Sprite sprite;

        [Header("Bean art (art bible Section 5; one sprite set for all tiers)")]
        public Sprite beanBody;
        public Sprite beanProp;
        [Tooltip("White overlay of the accessory region, tinted gold at tier 3.")]
        public Sprite beanAccent;
        [Tooltip("White silhouette used for hit flashes, the merge rim and the tier-3 glow.")]
        public Sprite beanFlash;

        public bool HasBeanArt => beanBody != null;

        public Sprite GetSpriteForTier(int tier)
        {
            int index = Mathf.Clamp(tier, 1, 3) - 1;
            if (tierSprites != null && index < tierSprites.Length && tierSprites[index] != null)
            {
                return tierSprites[index];
            }

            return sprite;
        }
    }
}
