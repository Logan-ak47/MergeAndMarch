# MERGE & MARCH — Phase 1 Setup Guide
# How to wire up these scripts in Unity 2022.3 LTS

---

## Prerequisites

1. **Unity 2022.3 LTS** with 2D URP template
2. **DOTween** — Install via Asset Store or `https://dotween.demigiant.com/`
   - After import: Tools → Demigiant → DOTween Utility Panel → Setup DOTween
3. **TextMeshPro** — Import TMP Essentials when prompted

---

## Step 1: Project Setup

1. Create new Unity project: **2D (URP)** template
2. Create folder: `Assets/_MergeAndMarch/`
3. Copy all scripts into `Assets/_MergeAndMarch/Scripts/` maintaining the subfolder structure:
   ```
   Scripts/
   ├── Core/       (GameManager, RunManager, TimeScaleManager)
   ├── Gameplay/   (BattleGrid, Troop, Enemy, MergeController, AutoCombat,
   │                EnemySpawner, DeploymentSystem, WaveManager)
   ├── Data/       (GameConfig, TroopData, EnemyData)
   └── UI/         (BattleHUD)
   ```

---

## Step 2: Create ScriptableObject Assets

### GameConfig
1. Right-click in Project → Create → MergeAndMarch → GameConfig
2. Name it `GameConfig`
3. Default values are already set in the script — adjust if needed

### TroopData (create 2 for Phase 1)

**Knight:**
- Right-click → Create → MergeAndMarch → TroopData
- Name: `Knight`
- troopType: Knight
- displayName: Knight
- troopColor: (#4488FF blue)
- baseHP: 100
- baseAttack: 15
- attackInterval: 2.0
- targeting: Melee
- meleeRange: 1

**Archer:**
- Name: `Archer`
- troopType: Archer
- displayName: Archer
- troopColor: (#44FF88 green)
- baseHP: 40
- baseAttack: 12
- attackInterval: 0.8
- targeting: Ranged

### EnemyData

**Grunt:**
- Right-click → Create → MergeAndMarch → EnemyData
- Name: `Grunt`
- enemyType: Grunt
- displayName: Grunt
- tintColor: (#FF4444 red)
- baseHP: 30
- baseAttack: 8
- moveSpeed: 1.5
- attackInterval: 1.5

---

## Step 3: Create Prefabs

### Troop Prefab
1. Create empty GameObject, name it `TroopPrefab`
2. Add SpriteRenderer (assign a placeholder square sprite, any color)
3. Add `Troop` component (the script — don't set any serialized fields, they're set at runtime via Init)
4. Set SpriteRenderer sorting layer to "Troops" (create this layer)
5. Drag to `Assets/_MergeAndMarch/Prefabs/` to make it a prefab
6. Delete from scene

### Enemy Prefab
1. Create empty GameObject, name it `EnemyPrefab`
2. Add SpriteRenderer (placeholder circle sprite, red tint)
3. Add `Enemy` component
4. Set sorting layer to "Enemies"
5. Save as prefab, delete from scene

### Slot Highlight Prefab (optional)
1. Create empty GameObject with SpriteRenderer
2. Use a subtle square sprite, low alpha (#FFFFFF20)
3. Save as `SlotHighlight` prefab

---

## Step 4: Scene Setup

### Create the Game Scene

1. Create new scene: `Game.unity`
2. Set camera to Orthographic, Size = 5, Background = dark (#1A1A2E)

### Create Manager GameObject
1. Create empty GameObject: `_Managers`
2. Add these components:
   - `GameManager` → assign GameConfig
   - `RunManager` → assign GameConfig, TroopPrefab, Knight data, Archer data
   - `WaveManager` → assign GameConfig
   - `AutoCombat` → assign GameConfig
   - `TimeScaleManager`

### Create Grid GameObject
1. Create empty GameObject: `BattleGrid`
2. Add `BattleGrid` component → assign GameConfig, SlotHighlight prefab (optional)

### Create Spawner GameObjects
1. `EnemySpawner` empty GO → add `EnemySpawner` component → assign GameConfig, EnemyPrefab, Grunt data
2. `DeploymentSystem` empty GO → add `DeploymentSystem` component → assign GameConfig, TroopPrefab, Knight data, Archer data (leave Mage/Healer null for Phase 1)

### Create Merge Controller
1. `MergeController` empty GO → add `MergeController` component → assign GameConfig
   - mergeParticlePrefab: optional (create a simple particle system prefab if desired)

### Create HUD (optional but recommended)
1. Create Canvas (Screen Space - Overlay)
2. Add 4 TextMeshPro text elements in corners:
   - Top-left: Wave counter
   - Top-right: Coins
   - Bottom-left: Troop count
   - Bottom-center: State text
3. Add `BattleHUD` component to Canvas → assign all 4 text references

---

## Step 5: Sorting Layers

Create these sorting layers (Edit → Project Settings → Tags and Layers):
1. Background (0)
2. Grid (1)
3. Enemies (2)
4. Troops (3)
5. Effects (4)
6. UI (5)

---

## Step 6: Play & Test!

Press Play. The game should:
1. Place 2 Knights (front, col 0) and 2 Archers (back, col 3)
2. Wave 1 begins: 3 grunts march from the right
3. Knights and Archers auto-attack the nearest enemy
4. You can drag a Knight onto the other Knight → they merge into Tier 2 (with slow-mo!)
5. Between waves, a new Tier 1 troop deploys into an empty slot
6. Waves progress through 15 + boss

### What to Test (Day 1-2 Kill Criteria)
- [ ] Does dragging feel responsive?
- [ ] Does slow-mo on drag feel empowering (not annoying)?
- [ ] Does the merge animation "pop" feel satisfying?
- [ ] Is auto-combat readable — can you tell what's happening?
- [ ] Does a Tier 2 troop feel noticeably stronger?
- [ ] Do you instinctively want to merge more?

---

## Placeholder Art Tips

For Phase 1, use Unity's built-in shapes:
- **Troops:** Colored squares (Knight=blue, Archer=green)
- **Enemies:** Red circles
- **Tier visual:** Increase scale per tier (already handled in Troop.cs)
- **Grid slots:** Semi-transparent white squares

You can replace these with AI-generated chibi sprites in Phase 3.

---

## Next Steps (Phase 2)

After Phase 1 feels right:
1. Add CardSystem.cs — roguelike card selection between waves
2. Add remaining 3 troop types (Mage, Healer, Bomber)
3. Add 4 more enemy types (Rusher, Tank, Flyer, Boss with mechanics)
4. Implement the 15-card starter pool
5. Add score/coin tracking UI
