using UnityEngine;

namespace MergeAndMarch.Data
{
    [CreateAssetMenu(fileName = "EnemyData", menuName = "Merge And March/Enemy Data")]
    public class EnemyData : ScriptableObject
    {
        public EnemyType enemyType;
        public string displayName = "Enemy";
        public Color tintColor = Color.red;
        [Min(1f)] public float baseHP = 30f;
        [Min(0f)] public float baseAttack = 8f;
        [Min(0.1f)] public float moveSpeed = 1.5f;
        [Min(0.05f)] public float attackInterval = 1.5f;
        [Min(0.1f)] public float sizeScale = 1f;
        public bool skipsFrontline;
        public float renderYOffset;
        public Sprite sprite;
    }
}
