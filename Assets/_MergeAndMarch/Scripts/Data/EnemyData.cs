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
        [Tooltip("Damage dealt to the base when this enemy gets past the troops.")]
        [Min(0f)] public float baseDamage = 10f;
        public float renderYOffset;
        public Sprite sprite;

        [Header("Bean art (art bible Section 5)")]
        public Sprite beanBody;
        [Tooltip("White silhouette used for hit flashes.")]
        public Sprite beanFlash;
        [Tooltip("Flyer only: wing sprite drawn behind the body.")]
        public Sprite beanWings;

        public bool HasBeanArt => beanBody != null;
    }
}
