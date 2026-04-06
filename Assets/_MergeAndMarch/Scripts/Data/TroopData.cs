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
        public Sprite sprite;
    }
}
