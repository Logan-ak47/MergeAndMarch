# MERGE & MARCH — Claude Code Context File
# UPDATE THIS FILE at the end of every session.
# Read this file at the START of every session.

---

## PROJECT STATUS

**Current Phase:** Phase 1 — Grid + Merge + Basic Combat
**Session Count:** 0
**Last Session Date:** —
**Last Session Summary:** —
**Next Task:** Build BattleGrid + place troops on screen

---

## WHAT IS THIS GAME

Hybrid-casual mobile game. Drag to merge same troops into stronger warriors during auto-battles. Pick roguelike cards between waves. Idle barracks trains troops offline.

**The 3-second ad creative:** Two small troops merge → slow-mo → glowing giant troop appears → enemies destroyed.

---

## TECH STACK

- Unity 2022.3 LTS, 2D URP
- DOTween (animations, especially merge pop)
- TextMeshPro (UI text)
- ScriptableObjects for all data/config
- Coplay Unity MCP for editor control
- Target: Mobile (Android/iOS)

## PROJECT STRUCTURE

```
Assets/_MergeAndMarch/
├── Scripts/
│   ├── Core/         — GameManager, RunManager, TimeScaleManager
│   ├── Gameplay/     — BattleGrid, Troop, Enemy, MergeController, AutoCombat,
│   │                   EnemySpawner, DeploymentSystem, WaveManager
│   ├── Data/         — GameConfig, TroopData, EnemyData (ScriptableObjects)
│   └── UI/           — BattleHUD
├── Prefabs/
├── Sprites/
├── ScriptableObjects/
└── Scenes/
    └── Game.unity
```

---

## PHASE 1 BUILD ORDER (Days 1-3)

Build one system at a time. Test each before moving on.

### Session 1: Grid + Troops on Screen
- [ ] Create GameConfig ScriptableObject (all tuning values centralized)
- [ ] Create TroopData ScriptableObject (type, stats, sprites)
- [ ] Build BattleGrid: 4 columns × 2 rows, cell size 1.5, grid offset (-4, -0.5)
- [ ] Create Troop component: SpriteRenderer, colored square placeholder
- [ ] Create TroopPrefab
- [ ] Create Knight TroopData (HP:100, Atk:15, Interval:2.0s, Melee)
- [ ] Create Archer TroopData (HP:40, Atk:12, Interval:0.8s, Ranged)
- [ ] Place 4 troops: 2 Knights at col 0 (front), 2 Archers at col 3 (back)
- [ ] Verify: can see 4 colored squares on a visible grid
- **TEST:** Do 4 troops render correctly on an 8-slot grid?

### Session 2: Drag to Merge + Slow-Mo
- [ ] MergeController: detect touch on troop, start drag
- [ ] TimeScaleManager: Time.timeScale → 0.2 on drag start, → 1.0 on release
- [ ] Drag troop visually follows finger/mouse
- [ ] Drop on same-type same-tier troop → MERGE:
  - Remove one troop, upgrade other to tier+1
  - Tier multipliers: T1=1×, T2=3×, T3=9× (HP and Attack)
  - Attack interval: 10% faster per tier
- [ ] Drop on different type/tier → SWAP positions
- [ ] Drop on empty slot → REPOSITION
- [ ] Drop outside grid → return to original slot
- [ ] Merge animation sequence (0.75s total, use DOTween, SetUpdate(true) for unscaled time):
  1. Slide together (0.15s, Ease.InQuad)
  2. Flash white (0.1s)
  3. Particle burst in troop color (0.2s)
  4. Pop in with scale overshoot (0.2s, Ease.OutBack, scale to 1.3× then settle)
  5. Screen shake (0.1s, intensity 0.08)
- [ ] One merge per drag, no chaining
- [ ] Tier visual: T1=0.8 scale, T2=0.95 scale + brighter, T3=1.1 scale + white tint
- **⚠️ KILL CHECK:** Is the merge gesture satisfying? Does slow-mo feel empowering?

### Session 3: Auto-Combat + Enemies
- [ ] Create EnemyData ScriptableObject
- [ ] Create Grunt EnemyData (HP:30, Atk:8, Speed:1.5, Interval:1.5s)
- [ ] Enemy component: spawns at x=8, walks left
- [ ] Enemy stops when reaching a column with a troop, attacks that troop
- [ ] If enemy passes x=-5.5 (past grid) → run fails
- [ ] AutoCombat system:
  - Each troop has independent attack timer
  - Timers staggered randomly at wave start (0 to 0.5×interval)
  - Targeting: nearest enemy
  - Knight: melee (own column + 1 ahead)
  - Archer: infinite range
  - Multiple troops CAN hit same target
- [ ] Damage formula: `baseDamage × tierMultiplier`
- [ ] Troop death: red flash → destroy → slot becomes empty
- [ ] Enemy death: white flash → destroy
- **TEST:** Do troops auto-attack? Does a Tier 2 feel noticeably stronger than Tier 1?

### Session 4: Waves + Deployment
- [ ] WaveManager: 15 waves + boss (wave 16)
- [ ] Enemy count per wave: [3,3,4,5,6,6,7,8,8,9,10,10,11,12,12]
- [ ] Enemy HP scales: baseHP × (1 + (wave-1) × 0.12) × zoneDifficulty
- [ ] Wave cooldown: 2 seconds between waves
- [ ] DeploymentSystem: at wave start, deploy 1 Tier 1 troop into random empty slot
  - Spawn pool weighted by starting lineup composition
  - Skip wave 1 (player starts with their lineup)
- [ ] Bomber respawn: if bomber exists, re-enable at wave start
- [ ] Card pick placeholder: after waves 3, 6, 9, 12, 15 → pause 1.5s (Phase 2 adds real cards)
- [ ] Boss wave 16: single high-HP grunt (Phase 2 adds mechanics)
- [ ] Run end: Victory screen or Defeat screen (Phase 1: just log + auto-restart after 2s)
- [ ] Coins: 10 per wave cleared, 50 for boss kill, 40% on loss
- [ ] BattleHUD: wave counter, coins, troop count (TextMeshPro on Canvas)
- **TEST:** Does a full run take ~4.5 minutes? Do you want to do "one more run"?

---

## CRITICAL DESIGN RULES

**Never violate these — they define the game's identity:**

1. **Merging is ALWAYS worth it.** T2 > two T1s stat-wise. The decision is timing, not whether.
2. **One merge per drag.** Touch → slow-mo → one action → release → full speed. No chaining.
3. **Troops fight on their own.** Player NEVER controls attacks. Only merge, reposition, pick cards.
4. **Slow-mo on drag.** 20% time scale while finger is down. Snap back on release.
5. **Decision-based input only.** No continuous control, no reflex tests.
6. **Troop death = slot opens immediately.** Next wave deployment fills it. Tier investment is lost.

---

## NUMBERS QUICK REFERENCE

### Troop Base Stats (Tier 1, Level 1)

| Type | HP | Attack | Interval | Targeting |
|------|-----|--------|----------|-----------|
| Knight | 100 | 15 | 2.0s | Melee (col+1) |
| Archer | 40 | 12 | 0.8s | Nearest enemy (infinite) |
| Mage | 35 | 25 | 2.5s | AoE column (infinite) |
| Healer | 60 | 0 (heals 15) | 3.0s | Lowest-HP adjacent troop |
| Bomber | 20 | 80 | Instant | On contact (single use) |

### Tier Multipliers

| Tier | HP/Atk Multiplier | Atk Speed | Scale |
|------|-------------------|-----------|-------|
| 1 | 1.0× | 1.0× | 0.80 |
| 2 | 3.0× | 0.9× (10% faster) | 0.95 |
| 3 | 9.0× | 0.8× (20% faster) | 1.10 |

### Grid

- 4 columns × 2 rows = 8 slots
- Cell size: 1.5 units
- Grid offset: (-4, -0.5)
- Column 0 = frontline, Column 3 = backline
- Enemy spawn X: 8.0
- Grid fail X: -5.5 (enemy passes here = run over)

### Merge Animation (all unscaled time)

| Step | Duration | Effect |
|------|----------|--------|
| Slide together | 0.15s | Ease.InQuad |
| Flash white | 0.10s | Both troops |
| Particle burst | 0.20s | Troop color |
| Pop in | 0.20s | Ease.OutBack, 1.3× overshoot |
| Screen shake | 0.10s | Intensity 0.08 |
| **Total** | **0.75s** | |

### Waves

- 15 regular waves + 1 boss = 16 total
- Card pick after waves: 3, 6, 9, 12, 15
- Wave cooldown: 2 seconds
- Enemies per wave: 3,3,4,5,6,6,7,8,8,9,10,10,11,12,12
- HP scaling: baseHP × (1 + (wave-1) × 0.12)
- Coins: 10/wave, 50/boss, 40% multiplier on loss
- Target run time: ~4.5 min win, ~2-3 min loss

### Deployment

- 1 Tier 1 troop per wave into random empty slot
- Pool weighted by starting lineup (e.g., 3 archers in lineup → ~60% archer spawns)
- Bombers excluded from random deployment
- Starts from wave 2 (wave 1 uses starting lineup)

---

## PHASE 2 PREVIEW (don't build yet)

After Phase 1 passes kill criteria:
- CardSystem.cs: roguelike card selection (1 of 3, 5 picks per run)
- 15 starter card pool (stat boost, spawn, merge boost, heal, special, economy, deployment)
- Mage, Healer, Bomber troop types
- Rusher, Tank, Flyer, Boss enemy types
- Card reroll (3 gems)

## PHASE 3+ PREVIEW (don't build yet)

- Collection merging (4 dupes → unlock higher starting tier)
- Permanent level upgrades (20 levels, +5%/level, ×1.4 cost curve)
- Starting lineup selection (4→8 slots via player level)
- Deployment level upgrades (5 levels, coin-purchased)
- Gacha system

---

## SESSION LOG

Update this after every session. Format:
```
### Session N — YYYY-MM-DD
**Duration:** Xh
**Built:** [what was implemented]
**Issues:** [bugs, design problems discovered]
**Next:** [what to build next session]
**Kill criteria status:** [pass/fail/not yet testable]
```

(No sessions logged yet)

---

## REFERENCE FILES

- `MERGE_AND_MARCH_GDD_v2.md` — Full game design document (all systems, economy, retention)
- `MergeAndMarch_Phase1.zip` — Reference scripts (don't paste directly, use for logic comparison if stuck)
