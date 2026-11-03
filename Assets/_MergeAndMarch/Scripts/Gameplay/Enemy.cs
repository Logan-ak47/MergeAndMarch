using System.Collections;
using System.Collections.Generic;
using MergeAndMarch.Data;
using UnityEngine;
using UnityEngine.UI;

namespace MergeAndMarch.Gameplay
{
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(BoxCollider2D))]
    public class Enemy : MonoBehaviour
    {
        private static readonly List<Enemy> activeEnemyBuffer = new();
        private static Sprite fallbackSprite;
        private static Sprite runtimeSquareSprite;
        private static readonly Color HpBarFillColor = new(1f, 0.2667f, 0.2667f, 1f);

        private readonly List<Troop> troopBuffer = new();

        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private BoxCollider2D boxCollider;
        [SerializeField] private GameObject hpBarRoot;
        [SerializeField] private Image hpBarFillImage;

        private GameConfig gameConfig;
        private BattleGrid battleGrid;
        private EnemySpawner owner;
        private float currentHP;
        private float maxHP;
        private float attackDamage;
        private float attackTimer;
        private float failY;
        private bool hasEscaped;
        private bool isDying;
        private Vector2 baseSpriteSize = Vector2.one * 0.16f;
        private Coroutine flashRoutine;
        private Coroutine attackPulseRoutine;
        private Coroutine impactRoutine;
        private Coroutine deathRoutine;
        private Vector3 lanePosition;
        private int laneShiftTarget = -1;
        private bool laneShiftDescends;
        private bool lastStandDecided;
        private Troop zocHolder;
        private Vector3 configuredScale = Vector3.one;
        private float moveSpeed = 1f;
        private BeanVisual bean;
        public EnemyData Data { get; private set; }
        public int LaneColumn { get; private set; }
        /// <summary>Damage dealt to the base if this enemy leaks; defaults to EnemyData.baseDamage.</summary>
        public float BaseDamage { get; private set; }
        /// <summary>Kill energy this enemy grants toward the next draft; defaults to EnemyData.energyValue.</summary>
        public float EnergyValue { get; private set; }
        public float MaxHP => maxHP;
        public bool IsAlive => currentHP > 0.01f && !isDying;
        public static IReadOnlyList<Enemy> ActiveEnemies => activeEnemyBuffer;

        /// <summary>Half the body height in world units, including size scale.</summary>
        public float VisualHalfHeight => baseSpriteSize.y * 0.5f * Mathf.Abs(transform.lossyScale.y);

        /// <summary>
        /// Centre-to-centre distance at which this enemy stops in front of a troop: feet meet the
        /// troop's head, with a small overlap set by GameConfig.enemyContactOverlap.
        /// </summary>
        public float GetContactDistance(Troop troop)
        {
            if (troop == null || gameConfig == null)
            {
                return gameConfig != null ? gameConfig.enemyEngageDistance : 0.3f;
            }

            float contact = (VisualHalfHeight + troop.VisualHalfHeight) * gameConfig.enemyContactOverlap;
            return Mathf.Max(gameConfig.enemyEngageDistance, contact);
        }

        private void Reset()
        {
            CacheComponents();
            ResolveHpBarReferences();
            ConfigureCollider();
        }

        private void Awake()
        {
            CacheComponents();
            CaptureBaseSpriteSize();
            ResolveHpBarReferences();
            ConfigureCollider();
        }

        private void OnEnable()
        {
            if (!activeEnemyBuffer.Contains(this))
            {
                activeEnemyBuffer.Add(this);
            }
        }

        private void OnDisable()
        {
            activeEnemyBuffer.Remove(this);
        }

        private void Update()
        {
            if (Data == null || battleGrid == null || gameConfig == null || !IsAlive)
            {
                return;
            }

            if (TryTriggerBomberInRowZone())
            {
                return;
            }

            if (Data.enemyType == EnemyType.Boss && SlideToOccupiedLane())
            {
                return;
            }

            if (UpdateLaneShift())
            {
                return;
            }

            Troop blockingTroop = FindBlockingTroop();
            if (blockingTroop == null && TryStartZoneOfControlPull())
            {
                return;
            }

            if (blockingTroop == null)
            {
                TryLastStandTurn();
            }

            bean?.SetMarching(blockingTroop == null);
            if (blockingTroop == null)
            {
                MoveForward();
                return;
            }

            attackTimer -= Time.deltaTime;
            if (attackTimer > 0f)
            {
                return;
            }

            attackTimer = Data.attackInterval;
            if (bean != null)
            {
                bean.PlayAttack(blockingTroop.transform.position);
            }
            else
            {
                StartFlash(Color.Lerp(Data.tintColor, Color.white, 0.35f), 0.08f);
            }

            StartAttackPulse(blockingTroop.transform.position);
            SpawnAttackImpact(blockingTroop);

            bool troopDied = blockingTroop.ApplyDamage(attackDamage, this);
            ApplyKnightThorns(blockingTroop);
            if (troopDied)
            {
                battleGrid.RemoveTroop(blockingTroop);
                blockingTroop.Die();
            }
        }

        private bool TryTriggerBomberInRowZone()
        {
            battleGrid.GetTroops(troopBuffer);

            for (int i = 0; i < troopBuffer.Count; i++)
            {
                Troop troop = troopBuffer[i];
                if (troop == null
                    || !troop.IsSingleUse
                    || troop.HasExplodedThisWave
                    || troop.Column != LaneColumn)
                {
                    continue;
                }

                if (Data != null && Data.skipsFrontline && troop.Row == 0)
                {
                    continue;
                }

                // Enemies now stop at body contact, so the Bomber must trigger at least that far out.
                float triggerReach = Mathf.Max(gameConfig.bomberTriggerReach, GetContactDistance(troop) + 0.05f);
                float distanceToTroop = transform.position.y - troop.transform.position.y;
                if (distanceToTroop < -0.05f || distanceToTroop > triggerReach)
                {
                    continue;
                }

                troop.TriggerExplosion();
                return !IsAlive;
            }

            return false;
        }

        /// <summary>Overrides the leak damage and kill energy, e.g. for a mid-boss built from the boss data.</summary>
        public void SetRewards(float baseDamage, float energyValue)
        {
            BaseDamage = Mathf.Max(0f, baseDamage);
            EnergyValue = Mathf.Max(0f, energyValue);
        }

        public void Initialize(
            EnemyData enemyData,
            GameConfig config,
            BattleGrid grid,
            EnemySpawner enemySpawner,
            int laneColumn,
            Vector3 spawnPosition,
            float failLineY,
            float healthMultiplier = 1f,
            float attackMultiplier = 1f,
            float scaleMultiplier = 1f,
            float moveSpeedMultiplier = 1f)
        {
            Data = enemyData;
            BaseDamage = enemyData != null ? enemyData.baseDamage : 0f;
            EnergyValue = enemyData != null ? enemyData.energyValue : 0f;
            gameConfig = config;
            battleGrid = grid;
            owner = enemySpawner;
            LaneColumn = config != null ? Mathf.Clamp(laneColumn, 0, Mathf.Max(0, config.columns - 1)) : laneColumn;
            failY = failLineY;
            isDying = false;
            hasEscaped = false;
            maxHP = Data != null ? Data.baseHP * Mathf.Max(0.01f, healthMultiplier) : 0f;
            currentHP = maxHP;
            attackDamage = Data != null ? Data.baseAttack * Mathf.Max(0.01f, attackMultiplier) : 0f;
            moveSpeed = Data != null ? Data.moveSpeed * Mathf.Max(0.01f, moveSpeedMultiplier) : 0f;
            attackTimer = Random.Range(0f, Mathf.Max(0.05f, Data != null ? Data.attackInterval * 0.5f : 0.25f));

            name = Data != null ? $"{Data.displayName}_{LaneColumn}" : "Enemy";
            Vector3 visualSpawnPos = spawnPosition + new Vector3(0f, Data != null ? Data.renderYOffset : 0f, 0f);
            transform.position = visualSpawnPos;
            lanePosition = transform.position;

            if (spriteRenderer != null && Data != null && Data.HasBeanArt)
            {
                SetupBean();
            }
            else if (spriteRenderer != null && Data != null)
            {
                bool hasArtSprite = Data.sprite != null;
                spriteRenderer.sprite = hasArtSprite ? SpriteBackgroundCleaner.GetCleanedSprite(Data.sprite) : GetFallbackSprite();
                spriteRenderer.color = hasArtSprite ? Color.white : Data.tintColor;
                spriteRenderer.drawMode = SpriteDrawMode.Simple;
                spriteRenderer.size = Vector2.one;
                spriteRenderer.sortingLayerName = "Enemies";
                spriteRenderer.sortingOrder = 0;
                CaptureBaseSpriteSize();
            }

            configuredScale = Vector3.one * gameConfig.enemyBaseScale * Mathf.Max(0.1f, scaleMultiplier);
            transform.localScale = configuredScale;
            ConfigureCollider();
            boxCollider.enabled = true;
            ResolveHpBarReferences();
            UpdateHPBar();
        }

        /// <summary>
        /// Applies damage and plays the hit recipe (art bible Section 2: white flash, recoil,
        /// squash, sparks in the attacker's colour, damage number). Returns true if the enemy died.
        /// </summary>
        /// <param name="damage">Damage to apply.</param>
        /// <param name="source">The troop that dealt it, if any; tints sparks and enlarges tier-3 numbers.</param>
        public bool ApplyDamage(float damage, Troop source = null)
        {
            if (!IsAlive)
            {
                return true;
            }

            float appliedDamage = Mathf.Max(0f, damage);
            currentHP = Mathf.Max(0f, currentHP - appliedDamage);
            UpdateHPBar();
            if (bean != null)
            {
                PlayBeanHit(source);
            }
            else
            {
                StartFlash(Color.white, 0.1f);
            }

            if (appliedDamage > 0.01f)
            {
                bool emphasised = source != null && source.Tier >= 3;
                DamageNumber.Spawn(transform.position + (Vector3.up * 0.15f), appliedDamage, Color.white, emphasised);
            }
            if (!IsAlive)
            {
                owner?.NotifyEnemyDefeated(this);
                StartDeathSequence();
                return true;
            }

            return false;
        }

        public void PlayImpactFeedback()
        {
            // Bean enemies play their impact as part of the hit recipe in ApplyDamage.
            if (isDying || spriteRenderer == null || bean != null)
            {
                return;
            }

            if (impactRoutine != null)
            {
                StopCoroutine(impactRoutine);
                transform.localScale = configuredScale;
            }

            impactRoutine = StartCoroutine(ImpactRoutine());
        }

        /// <summary>
        /// Boss only: when its lane has no troops left, it slides sideways to the nearest lane that
        /// still has one instead of walking into the base, so the finale is a fight to the end.
        /// Returns true while sliding.
        /// </summary>
        private bool SlideToOccupiedLane()
        {
            if (LaneHasTroop(LaneColumn))
            {
                return false;
            }

            int best = -1;
            int bestDistance = int.MaxValue;
            int columns = Mathf.Max(1, gameConfig.columns);
            for (int column = 0; column < columns; column++)
            {
                int distance = Mathf.Abs(column - LaneColumn);
                if (distance < bestDistance && LaneHasTroop(column))
                {
                    best = column;
                    bestDistance = distance;
                }
            }

            if (best < 0)
            {
                return false;
            }

            float targetX = battleGrid.GetSlotWorldPosition(best, 0).x;
            Vector3 position = transform.position;
            position.x = Mathf.MoveTowards(position.x, targetX, moveSpeed * 2f * Time.deltaTime);
            transform.position = position;
            lanePosition = position;
            bean?.SetMarching(true);
            if (Mathf.Abs(position.x - targetX) < 0.01f)
            {
                LaneColumn = best;
            }

            return true;
        }

        /// <summary>True for enemies the lane-defence rules apply to (not flyers, not the boss).</summary>
        private bool FollowsLaneDefence => Data != null && !Data.skipsFrontline && Data.enemyType != EnemyType.Boss;

        /// <summary>
        /// Moves the enemy sideways toward <see cref="laneShiftTarget"/>: a quick pull (no descent) for
        /// zone of control, or a diagonal slide while descending for the last-stand turn. Returns true
        /// while a pull is in progress (the enemy does nothing else that frame).
        /// </summary>
        private bool UpdateLaneShift()
        {
            if (laneShiftTarget < 0)
            {
                return false;
            }

            if (!laneShiftDescends && (zocHolder == null || !zocHolder.IsCombatActive))
            {
                laneShiftTarget = -1;
                zocHolder = null;
                return false;
            }

            float targetX = battleGrid.GetSlotWorldPosition(laneShiftTarget, 0).x;
            float lateralSpeed = laneShiftDescends
                ? moveSpeed * gameConfig.lastStandLateralSpeedMult
                : gameConfig.cellSize / Mathf.Max(0.01f, gameConfig.zocPullDuration);
            Vector3 position = transform.position;
            position.x = Mathf.MoveTowards(position.x, targetX, lateralSpeed * Time.deltaTime);
            transform.position = position;
            lanePosition = position;
            if (Mathf.Abs(position.x - targetX) < 0.01f)
            {
                LaneColumn = laneShiftTarget;
                laneShiftTarget = -1;
            }

            // A last-stand slide keeps descending through the normal MoveForward below.
            if (laneShiftDescends)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Zone of control (design/specs/lane-defence.md, rule 1): a front-row Knight within
        /// <c>zocLanes</c> columns and contact range pulls this enemy into its lane, up to
        /// <c>zocCapacity</c> pulled enemies per Knight.
        /// </summary>
        private bool TryStartZoneOfControlPull()
        {
            if (!FollowsLaneDefence || gameConfig.zocLanes <= 0 || gameConfig.zocCapacity <= 0)
            {
                return false;
            }

            if (zocHolder != null && zocHolder.IsCombatActive && zocHolder.Column == LaneColumn)
            {
                return false;
            }

            zocHolder = null;
            battleGrid.GetTroops(troopBuffer);
            Troop best = null;
            int bestLanes = int.MaxValue;
            for (int i = 0; i < troopBuffer.Count; i++)
            {
                Troop troop = troopBuffer[i];
                if (troop == null || !troop.IsCombatActive || troop.Row != 0 || troop.Data == null || troop.Data.troopType != TroopType.Knight)
                {
                    continue;
                }

                int lanes = Mathf.Abs(troop.Column - LaneColumn);
                if (lanes == 0 || lanes > gameConfig.zocLanes || lanes >= bestLanes)
                {
                    continue;
                }

                float distance = transform.position.y - troop.transform.position.y;
                if (distance < -0.05f || distance > GetContactDistance(troop) + gameConfig.zocReachExtra)
                {
                    continue;
                }

                if (CountPulledBy(troop) >= gameConfig.zocCapacity)
                {
                    continue;
                }

                best = troop;
                bestLanes = lanes;
            }

            if (best == null)
            {
                return false;
            }

            zocHolder = best;
            laneShiftTarget = best.Column;
            laneShiftDescends = false;
            bean?.Squash(new Vector2(1.15f, 0.85f), 0.06f, 0.12f);
            return true;
        }

        private static int CountPulledBy(Troop knight)
        {
            int count = 0;
            for (int i = 0; i < activeEnemyBuffer.Count; i++)
            {
                Enemy enemy = activeEnemyBuffer[i];
                if (enemy != null && enemy.IsAlive && enemy.zocHolder == knight)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Last stand (design/specs/lane-defence.md, rule 2): with at most <c>lastStandOccupiedLanes</c>
        /// lanes holding a troop, an enemy in an empty lane decides once, at the halfway line, to slide
        /// to the lane beside the nearest troop, where zone of control can catch it.
        /// </summary>
        private void TryLastStandTurn()
        {
            if (lastStandDecided || !FollowsLaneDefence || laneShiftTarget >= 0)
            {
                return;
            }

            Bounds grid = battleGrid.GetGridWorldBounds();
            float lineY = grid.max.y + (gameConfig.enemySpawnYOffset * (1f - gameConfig.lastStandLineFraction));
            if (transform.position.y > lineY)
            {
                return;
            }

            lastStandDecided = true;
            int columns = Mathf.Max(1, gameConfig.columns);
            int occupied = 0;
            int nearest = -1;
            for (int column = 0; column < columns; column++)
            {
                if (!LaneHasTroop(column))
                {
                    continue;
                }

                occupied++;
                if (nearest < 0 || Mathf.Abs(column - LaneColumn) < Mathf.Abs(nearest - LaneColumn))
                {
                    nearest = column;
                }
            }

            if (occupied == 0 || occupied > gameConfig.lastStandOccupiedLanes || LaneHasTroop(LaneColumn))
            {
                return;
            }

            int offset = Mathf.Clamp(LaneColumn - nearest, -1, 1);
            int target = Mathf.Clamp(nearest + (gameConfig.zocLanes > 0 ? offset : 0), 0, columns - 1);
            if (target == LaneColumn)
            {
                return;
            }

            laneShiftTarget = target;
            laneShiftDescends = true;
        }

        private bool LaneHasTroop(int column)
        {
            battleGrid.GetTroops(troopBuffer);
            for (int i = 0; i < troopBuffer.Count; i++)
            {
                Troop troop = troopBuffer[i];
                if (troop != null && troop.IsCombatActive && troop.Column == column)
                {
                    return true;
                }
            }

            return false;
        }

        private void MoveForward()
        {
            transform.position += Vector3.down * (moveSpeed * Time.deltaTime);
            lanePosition = transform.position;
            if (!hasEscaped && transform.position.y < failY)
            {
                // Walked past the troops: hits the base once (WaveManager applies baseDamage) and is gone.
                hasEscaped = true;
                owner?.NotifyEnemyEscaped(this);
                Destroy(gameObject);
            }
        }

        private Troop FindBlockingTroop()
        {
            battleGrid.GetTroops(troopBuffer);

            Troop best = null;
            float bestY = float.NegativeInfinity;

            for (int i = 0; i < troopBuffer.Count; i++)
            {
                Troop troop = troopBuffer[i];
                if (troop == null || !troop.IsCombatActive || troop.Column != LaneColumn)
                {
                    continue;
                }

                if (Data != null && Data.skipsFrontline && troop.Row == 0)
                {
                    continue;
                }

                float distanceToTroop = transform.position.y - troop.transform.position.y;
                if (distanceToTroop < -0.05f || distanceToTroop > GetContactDistance(troop))
                {
                    continue;
                }

                float troopY = troop.transform.position.y;
                if (troopY > bestY)
                {
                    best = troop;
                    bestY = troopY;
                }
            }

            return best;
        }

        private void ApplyKnightThorns(Troop troop)
        {
            if (troop == null || troop.Data == null || troop.Data.troopType != TroopType.Knight)
            {
                return;
            }

            RunBuffs buffs = CardSystem.Instance != null ? CardSystem.Instance.runBuffs : null;
            if (buffs == null || buffs.knightThornsDamage <= 0f)
            {
                return;
            }

            ApplyDamage(buffs.knightThornsDamage);
        }

        private void StartFlash(Color flashColor, float duration)
        {
            if (spriteRenderer == null)
            {
                return;
            }

            if (flashRoutine != null)
            {
                StopCoroutine(flashRoutine);
            }

            flashRoutine = StartCoroutine(FlashRoutine(flashColor, duration));
        }

        private void StartAttackPulse(Vector3 targetPosition)
        {
            if (attackPulseRoutine != null)
            {
                StopCoroutine(attackPulseRoutine);
            }

            attackPulseRoutine = StartCoroutine(AttackPulseRoutine(targetPosition));
        }

        private void StartDeathSequence()
        {
            if (isDying)
            {
                return;
            }

            isDying = true;
            currentHP = 0f;
            boxCollider.enabled = false;

            if (attackPulseRoutine != null)
            {
                StopCoroutine(attackPulseRoutine);
                attackPulseRoutine = null;
            }

            if (impactRoutine != null)
            {
                StopCoroutine(impactRoutine);
                impactRoutine = null;
            }

            deathRoutine = StartCoroutine(bean != null ? BeanDeathRoutine() : DeathRoutine());
        }

        private void SetupBean()
        {
            spriteRenderer.sprite = null;
            spriteRenderer.enabled = false;
            if (bean == null)
            {
                JuiceFx fx = JuiceFx.Instance;
                bean = BeanVisual.Create(
                    transform,
                    Data.beanBody,
                    null,
                    null,
                    Data.beanFlash,
                    Data.beanWings,
                    true,
                    BeanPropAction.Jab,
                    fx != null ? fx.Config : null,
                    fx != null ? fx.Art : null,
                    "Enemies",
                    0);
            }

            JuiceConfig juice = bean.Config;
            bean.SetMarchRate(Data.enemyType == EnemyType.Rusher ? juice.rusherMarchRate : 1f);
            bean.SetMarching(true);
            baseSpriteSize = Data.beanBody.bounds.size;
            PlaceHpBarAboveBean(juice);

            if (Data.enemyType == EnemyType.Boss)
            {
                bean.Squash(juice.bossLandSquash, 0.08f, 0.25f);
                JuiceFx.Instance?.BossEntrance(transform.position);
            }
        }

        /// <summary>Enemy HP bar above the head: a pill 70% of body width, 14 px tall (art bible Section 7).</summary>
        private void PlaceHpBarAboveBean(JuiceConfig juice)
        {
            ResolveHpBarReferences();
            if (hpBarRoot == null || hpBarRoot.transform is not RectTransform barRect)
            {
                return;
            }

            float canvasScale = Mathf.Max(0.0001f, barRect.localScale.x);
            float bodyWidth = Data.beanBody.bounds.size.x * 0.55f;
            barRect.anchoredPosition = new Vector2(0f, bean.TopLocalY + 0.05f);
            barRect.sizeDelta = new Vector2(bodyWidth * 0.7f / canvasScale, juice.PxToWorld(14f) / canvasScale);
        }

        /// <summary>Pushes the enemy away from an explosion and back (art bible: Bomber knockback).</summary>
        public void Knockback(Vector3 direction)
        {
            if (bean == null || !IsAlive || direction.sqrMagnitude < 0.0001f)
            {
                return;
            }

            JuiceConfig juice = bean.Config;
            bean.Recoil(direction, juice.bomberKnockback, juice.bomberKnockbackDuration);
        }

        private void PlayBeanHit(Troop source)
        {
            JuiceConfig juice = bean.Config;
            bean.Flash(Color.white, juice.enemyHitFlashDuration);
            bean.Recoil(Vector3.up, juice.enemyRecoilPercent * bean.BodyHeight, juice.enemyHitSquashDuration);
            bean.Squash(juice.enemyHitSquash, juice.enemyHitSquashDuration * 0.4f, juice.enemyHitSquashDuration * 0.6f);
            bean.ShowFace(BeanFace.Hurt, juice.troopHurtFaceDuration);
            Color sparkColor = source != null && source.Data != null ? source.Data.troopColor : Color.white;
            JuiceFx.Instance?.HitSparks(bean.CenterPosition, sparkColor, juice.Count(juice.hitSparksMin, juice.hitSparksMax));
        }

        private IEnumerator BeanDeathRoutine()
        {
            yield return bean.PlayEnemyDeath();
            Color chunkColor = Data != null && Data.enemyType == EnemyType.Tank ? new Color(0.702f, 0.071f, 0.247f, 1f) : bean.Config.enemyRed;
            JuiceFx.Instance?.EnemyDeathBurst(transform.position, chunkColor, Data != null && Data.enemyType == EnemyType.Tank);
            Destroy(gameObject);
        }

        private void SpawnAttackImpact(Troop target)
        {
            if (target == null)
            {
                return;
            }

            bool isFlyer = Data != null && Data.skipsFrontline;
            GameObject impact = new(isFlyer ? "FlyerSlashImpact" : "EnemyMeleeImpact");
            impact.transform.position = target.transform.position + (Vector3.up * 0.12f);
            impact.transform.localScale = isFlyer ? Vector3.one * 1.05f : Vector3.one * 0.75f;

            Sprite square = GetRuntimeSquareSprite();
            Color color = isFlyer
                ? new Color(0.58f, 1f, 1f, 0.95f)
                : new Color(1f, 0.45f, 0.22f, 0.85f);

            if (isFlyer)
            {
                SpriteRenderer slashA = CreateImpactSprite(impact.transform, "SlashA", square, color, 210);
                slashA.transform.localRotation = Quaternion.Euler(0f, 0f, 35f);
                slashA.transform.localScale = new Vector3(0.52f, 0.055f, 1f);

                SpriteRenderer slashB = CreateImpactSprite(impact.transform, "SlashB", square, Color.white, 211);
                slashB.transform.localRotation = Quaternion.Euler(0f, 0f, -35f);
                slashB.transform.localScale = new Vector3(0.38f, 0.045f, 1f);
            }
            else
            {
                SpriteRenderer horizontal = CreateImpactSprite(impact.transform, "Horizontal", square, color, 210);
                horizontal.transform.localScale = new Vector3(0.28f, 0.06f, 1f);

                SpriteRenderer vertical = CreateImpactSprite(impact.transform, "Vertical", square, color, 210);
                vertical.transform.localScale = new Vector3(0.06f, 0.28f, 1f);
            }

            impact.AddComponent<CombatFeedbackFx>().PlayFade(isFlyer ? 0.3f : 0.22f);
        }

        private SpriteRenderer CreateImpactSprite(Transform parent, string name, Sprite sprite, Color color, int sortingOrder)
        {
            GameObject effectObject = new(name);
            effectObject.transform.SetParent(parent, false);

            SpriteRenderer renderer = effectObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingLayerName = "Effects";
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private IEnumerator FlashRoutine(Color flashColor, float duration)
        {
            Color original = spriteRenderer.color;
            spriteRenderer.color = flashColor;

            float elapsed = 0f;
            duration = Mathf.Max(0.01f, duration);
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                spriteRenderer.color = Color.Lerp(flashColor, original, t);
                yield return null;
            }

            spriteRenderer.color = original;
            flashRoutine = null;
        }

        private IEnumerator AttackPulseRoutine(Vector3 targetPosition)
        {
            Vector3 start = lanePosition;
            Vector3 direction = targetPosition - start;
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = Vector3.down;
            }

            Vector3 pulseTarget = start + (direction.normalized * 0.18f);
            float forwardDuration = 0.06f;
            float returnDuration = 0.08f;
            float elapsed = 0f;

            while (elapsed < forwardDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / forwardDuration);
                transform.position = Vector3.Lerp(start, pulseTarget, t);
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < returnDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / returnDuration);
                transform.position = Vector3.Lerp(pulseTarget, start, t);
                yield return null;
            }

            transform.position = start;
            attackPulseRoutine = null;
        }

        private IEnumerator ImpactRoutine()
        {
            Vector3 startScale = configuredScale;
            Vector3 punchScale = configuredScale * 1.15f;
            float duration = 0.1f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float split = t < 0.5f ? t / 0.5f : (t - 0.5f) / 0.5f;
                transform.localScale = t < 0.5f
                    ? Vector3.Lerp(startScale, punchScale, split)
                    : Vector3.Lerp(punchScale, startScale, split);
                yield return null;
            }

            transform.localScale = startScale;
            impactRoutine = null;
        }

        private IEnumerator DeathRoutine()
        {
            Vector3 startScale = transform.localScale;
            Color startColor = spriteRenderer.color;
            float duration = 0.15f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
                spriteRenderer.color = new Color(startColor.r, startColor.g, startColor.b, Mathf.Lerp(startColor.a, 0f, t));
                yield return null;
            }

            Destroy(gameObject);
        }

        private static Sprite GetFallbackSprite()
        {
            if (fallbackSprite != null)
            {
                return fallbackSprite;
            }

            Texture2D texture = new(16, 16, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[16 * 16];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.white;
            }

            texture.SetPixels(pixels);
            texture.Apply();
            fallbackSprite = Sprite.Create(texture, new Rect(0f, 0f, 16f, 16f), new Vector2(0.5f, 0.5f), 100f);
            fallbackSprite.name = "EnemyFallbackSprite";
            return fallbackSprite;
        }

        private static Sprite GetRuntimeSquareSprite()
        {
            if (runtimeSquareSprite != null)
            {
                return runtimeSquareSprite;
            }

            Texture2D texture = new(16, 16, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[16 * 16];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.white;
            }

            texture.SetPixels(pixels);
            texture.Apply();
            runtimeSquareSprite = Sprite.Create(texture, new Rect(0f, 0f, 16f, 16f), new Vector2(0.5f, 0.5f), 100f);
            runtimeSquareSprite.name = "EnemyRuntimeSquare";
            return runtimeSquareSprite;
        }

        private void CacheComponents()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            if (boxCollider == null)
            {
                boxCollider = GetComponent<BoxCollider2D>();
            }
        }

        private void CaptureBaseSpriteSize()
        {
            if (spriteRenderer != null && spriteRenderer.sprite != null)
            {
                baseSpriteSize = spriteRenderer.sprite.bounds.size;
            }
        }

        private void ConfigureCollider()
        {
            if (boxCollider == null)
            {
                return;
            }

            boxCollider.isTrigger = true;
            boxCollider.offset = Vector2.zero;
            boxCollider.size = baseSpriteSize * 0.9f;
        }

        private void ResolveHpBarReferences()
        {
            if (hpBarRoot == null)
            {
                hpBarRoot = transform.Find("HPBarRoot")?.gameObject;
            }

            if (hpBarFillImage == null && hpBarRoot != null)
            {
                hpBarFillImage = hpBarRoot.transform.Find("Fill")?.GetComponent<Image>();
            }
        }

        private void UpdateHPBar()
        {
            if (hpBarRoot == null || hpBarFillImage == null)
            {
                ResolveHpBarReferences();
            }

            if (hpBarRoot == null || hpBarFillImage == null)
            {
                return;
            }

            if (maxHP <= 0.01f)
            {
                hpBarRoot.SetActive(false);
                hpBarFillImage.fillAmount = 0f;
                return;
            }

            float ratio = Mathf.Clamp01(currentHP / maxHP);
            hpBarFillImage.fillAmount = ratio;
            hpBarFillImage.color = HpBarFillColor;

            bool shouldShow = currentHP > 0.01f && ratio < 0.999f && IsAlive;
            hpBarRoot.SetActive(shouldShow);

            Canvas canvas = hpBarRoot.GetComponent<Canvas>();
            if (canvas != null && spriteRenderer != null)
            {
                canvas.sortingLayerName = spriteRenderer.sortingLayerName;
                canvas.sortingOrder = spriteRenderer.sortingOrder + 2;
            }
        }

    }
}
