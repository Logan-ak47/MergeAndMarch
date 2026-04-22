using System;
using System.Collections.Generic;
using MergeAndMarch.Data;
using MergeAndMarch.UI;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MergeAndMarch.Gameplay
{
    public class CardSystem : MonoBehaviour
    {
        private readonly List<CardData> currentChoices = new();
        private readonly List<Troop> troopBuffer = new();
        private readonly List<float> maxHpBuffer = new();

        [SerializeField] private List<CardData> allCards = new();
        [SerializeField] private BattleGrid battleGrid;
        [SerializeField] private DeploymentSystem deploymentSystem;
        [SerializeField] private AutoCombat autoCombat;
        [SerializeField] private CardSelectionUI cardSelectionUI;

        private CardData selectedCard;

        public static CardSystem Instance { get; private set; }
        public RunBuffs runBuffs = new();
        public event Action CardPickCompleted;
        public event Action RunBuffsChanged;

        public IReadOnlyList<CardData> AllCards => allCards;
        public IReadOnlyList<CardData> CurrentChoices => currentChoices;
        public bool IsCardPickActive => cardSelectionUI != null && cardSelectionUI.IsVisible;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            ResolveReferences();
            LoadCardsIfNeeded();
        }

        public void ResetRunBuffs()
        {
            runBuffs = new RunBuffs();
            selectedCard = null;
            cardSelectionUI?.Hide();
            NotifyRunBuffsChanged();
        }

        public void StartCardPick()
        {
            ResolveReferences();
            LoadCardsIfNeeded();

            if (allCards == null || allCards.Count == 0 || cardSelectionUI == null)
            {
                CardPickCompleted?.Invoke();
                return;
            }

            currentChoices.Clear();
            BuildChoiceSet();
            if (currentChoices.Count == 0)
            {
                CardPickCompleted?.Invoke();
                return;
            }

            selectedCard = null;
            cardSelectionUI.ShowCards(currentChoices.ToArray(), OnCardSelected);
        }

        public void OnCardSelected(int index)
        {
            if (index < 0 || index >= currentChoices.Count)
            {
                return;
            }

            selectedCard = currentChoices[index];
            ApplyCard(selectedCard);
            cardSelectionUI?.Hide();
            CardPickCompleted?.Invoke();
        }

        public void ApplyCard(CardData card)
        {
            if (card == null)
            {
                return;
            }

            switch (card.effectType)
            {
                case CardEffectType.AttackBoostAll:
                    runBuffs.attackMultiplier += card.effectValue;
                    break;
                case CardEffectType.HPBoostAll:
                    ApplyHpBoost(card.effectValue);
                    break;
                case CardEffectType.AttackBoostKnights:
                    runBuffs.knightAttackMultiplier += card.effectValue;
                    break;
                case CardEffectType.AttackBoostArchers:
                    runBuffs.archerAttackMultiplier += card.effectValue;
                    break;
                case CardEffectType.AttackBoostMages:
                    runBuffs.mageAttackMultiplier += card.effectValue;
                    break;
                case CardEffectType.HealBoostHealers:
                case CardEffectType.HealerBoost:
                    runBuffs.healerPowerMultiplier += card.effectValue;
                    break;
                case CardEffectType.AttackBoostBombers:
                    runBuffs.bomberAttackMultiplier += card.effectValue;
                    break;
                case CardEffectType.SpawnRandomTroop:
                    deploymentSystem?.SpawnTroopInEmptySlot(null);
                    break;
                case CardEffectType.SpawnKnight:
                    deploymentSystem?.SpawnTroopInEmptySlot(TroopType.Knight);
                    break;
                case CardEffectType.SpawnArcher:
                    deploymentSystem?.SpawnTroopInEmptySlot(TroopType.Archer);
                    break;
                case CardEffectType.SpawnMage:
                    deploymentSystem?.SpawnTroopInEmptySlot(TroopType.Mage);
                    break;
                case CardEffectType.SpawnHealer:
                    deploymentSystem?.SpawnTroopInEmptySlot(TroopType.Healer);
                    break;
                case CardEffectType.SpawnBomber:
                    deploymentSystem?.SpawnTroopInEmptySlot(TroopType.Bomber);
                    break;
                case CardEffectType.BomberRadius:
                    runBuffs.bomberRadiusMultiplier += card.effectValue;
                    break;
                case CardEffectType.MergeBoostNextTier:
                    runBuffs.nextMergeBoosted = true;
                    break;
                case CardEffectType.HealAllTroops:
                    HealAllTroops(card.effectValue);
                    break;
                case CardEffectType.ReviveOneTroop:
                    deploymentSystem?.SpawnTroopInEmptySlot(null);
                    break;
                case CardEffectType.ArcherSpeedBoost:
                    runBuffs.archerSpeedMultiplier *= (1f - card.effectValue);
                    autoCombat?.ResetAttackTimers();
                    break;
                case CardEffectType.KnightThorns:
                    runBuffs.knightThornsDamage += card.effectValue;
                    break;
                case CardEffectType.DoubleCoins:
                    runBuffs.coinMultiplier = card.effectValue;
                    break;
                case CardEffectType.ExtraDeploy:
                    runBuffs.extraDeployCount += Mathf.RoundToInt(card.effectValue);
                    break;
                case CardEffectType.MergeHeal:
                    runBuffs.mergeHealPercent = card.effectValue;
                    break;
                case CardEffectType.ArcherDoubleDamageVsTank:
                    runBuffs.archerVsTankMultiplier = card.effectValue;
                    break;
                case CardEffectType.KnightDamageFlyers:
                    runBuffs.knightCanHitFlyers = true;
                    break;
            }

            NotifyRunBuffsChanged();
        }

        public bool ConsumeNextMergeBoost()
        {
            if (!runBuffs.nextMergeBoosted)
            {
                return false;
            }

            runBuffs.nextMergeBoosted = false;
            NotifyRunBuffsChanged();
            return true;
        }

        public string GetActiveBuffSummary()
        {
            List<string> lines = new() { "Active Buffs:" };

            AddPercentLine(lines, runBuffs.attackMultiplier - 1f, "ATK");
            AddPercentLine(lines, runBuffs.hpMultiplier - 1f, "HP");
            AddPercentLine(lines, runBuffs.knightAttackMultiplier - 1f, "Knight ATK");
            AddPercentLine(lines, runBuffs.archerAttackMultiplier - 1f, "Archer ATK");
            AddPercentLine(lines, runBuffs.mageAttackMultiplier - 1f, "Mage ATK");
            AddPercentLine(lines, runBuffs.healerPowerMultiplier - 1f, "Healer Power");
            AddPercentLine(lines, runBuffs.bomberAttackMultiplier - 1f, "Bomber ATK");
            AddPercentLine(lines, runBuffs.bomberRadiusMultiplier - 1f, "Bomber Radius");

            if (runBuffs.archerSpeedMultiplier < 0.999f)
            {
                AddPercentLine(lines, (1f / runBuffs.archerSpeedMultiplier) - 1f, "Archer Speed");
            }

            if (runBuffs.knightThornsDamage > 0f)
            {
                lines.Add($"Knight Thorns +{Mathf.RoundToInt(runBuffs.knightThornsDamage)}");
            }

            if (runBuffs.mergeHealPercent > 0f)
            {
                lines.Add($"Merge Heal +{Mathf.RoundToInt(runBuffs.mergeHealPercent * 100f)}%");
            }

            if (runBuffs.extraDeployCount > 0)
            {
                lines.Add($"+{runBuffs.extraDeployCount} Deploy");
            }

            if (runBuffs.nextMergeBoosted)
            {
                lines.Add("Next Merge +1 Tier");
            }

            if (!Mathf.Approximately(runBuffs.coinMultiplier, 1f))
            {
                lines.Add($"{runBuffs.coinMultiplier:0.##}x Coins");
            }

            if (runBuffs.archerVsTankMultiplier > 1.01f)
            {
                lines.Add($"Piercing Arrow {runBuffs.archerVsTankMultiplier:0.##}x vs Tank");
            }

            if (runBuffs.knightCanHitFlyers)
            {
                lines.Add("Knights hit Flyers");
            }

            if (lines.Count == 1)
            {
                lines.Add("None");
            }

            return string.Join("\n", lines);
        }

        private void ApplyHpBoost(float effectValue)
        {
            if (battleGrid == null)
            {
                return;
            }

            battleGrid.GetTroops(troopBuffer);
            maxHpBuffer.Clear();
            for (int i = 0; i < troopBuffer.Count; i++)
            {
                maxHpBuffer.Add(troopBuffer[i] != null ? troopBuffer[i].MaxHP : 0f);
            }

            runBuffs.hpMultiplier += effectValue;

            for (int i = 0; i < troopBuffer.Count; i++)
            {
                Troop troop = troopBuffer[i];
                if (troop == null)
                {
                    continue;
                }

                troop.RefreshCurrentHpForMaxHpChange(maxHpBuffer[i], true);
            }
        }

        private void HealAllTroops(float healPercent)
        {
            if (battleGrid == null)
            {
                return;
            }

            battleGrid.GetTroops(troopBuffer);
            for (int i = 0; i < troopBuffer.Count; i++)
            {
                troopBuffer[i]?.HealPercent(healPercent);
            }
        }

        private void BuildChoiceSet()
        {
            List<CardData> remaining = new(allCards);
            int desiredCount = Mathf.Min(3, remaining.Count);

            while (currentChoices.Count < desiredCount && remaining.Count > 0)
            {
                CardRarity rarity = RollRarity();
                List<CardData> rarityPool = remaining.FindAll(card => card != null && card.rarity == rarity);

                if (rarityPool.Count == 0)
                {
                    rarityPool = remaining.FindAll(card => card != null);
                }

                if (rarityPool.Count == 0)
                {
                    break;
                }

                CardData selected = rarityPool[UnityEngine.Random.Range(0, rarityPool.Count)];
                currentChoices.Add(selected);
                remaining.Remove(selected);
            }
        }

        private CardRarity RollRarity()
        {
            int roll = UnityEngine.Random.Range(0, 100);
            if (roll < 60)
            {
                return CardRarity.Common;
            }

            if (roll < 90)
            {
                return CardRarity.Rare;
            }

            return CardRarity.Epic;
        }

        private void ResolveReferences()
        {
            if (battleGrid == null)
            {
                battleGrid = FindFirstObjectByType<BattleGrid>();
            }

            if (deploymentSystem == null)
            {
                deploymentSystem = FindFirstObjectByType<DeploymentSystem>();
            }

            if (autoCombat == null)
            {
                autoCombat = FindFirstObjectByType<AutoCombat>();
            }

            if (cardSelectionUI == null)
            {
                cardSelectionUI = FindFirstObjectByType<CardSelectionUI>();
                if (cardSelectionUI == null)
                {
                    cardSelectionUI = gameObject.AddComponent<CardSelectionUI>();
                }
            }
        }

        private void LoadCardsIfNeeded()
        {
            if (allCards != null && allCards.Count > 0)
            {
                return;
            }

#if UNITY_EDITOR
            string[] guids = AssetDatabase.FindAssets("t:CardData", new[] { "Assets/_MergeAndMarch/ScriptableObjects/Cards" });
            allCards = new List<CardData>(guids.Length);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                CardData card = AssetDatabase.LoadAssetAtPath<CardData>(path);
                if (card != null)
                {
                    allCards.Add(card);
                }
            }
#endif
        }

        private void NotifyRunBuffsChanged()
        {
            RunBuffsChanged?.Invoke();
        }

        private void AddPercentLine(List<string> lines, float amount, string label)
        {
            if (amount <= 0.001f)
            {
                return;
            }

            lines.Add($"+{Mathf.RoundToInt(amount * 100f)}% {label}");
        }
    }
}
