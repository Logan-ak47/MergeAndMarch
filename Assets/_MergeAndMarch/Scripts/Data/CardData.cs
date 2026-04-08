using UnityEngine;

namespace MergeAndMarch.Data
{
    [CreateAssetMenu(fileName = "CardData", menuName = "Merge And March/Card Data")]
    public class CardData : ScriptableObject
    {
        public string cardName = "New Card";
        [TextArea(2, 4)] public string description = "Card description.";
        public Sprite icon;
        public CardCategory category;
        public CardRarity rarity = CardRarity.Common;
        public Color cardColor = Color.white;
        public CardEffectType effectType;
        public float effectValue;
    }
}
