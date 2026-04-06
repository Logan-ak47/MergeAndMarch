# MERGE & MARCH — Game Design Document v2.0
# Complete GDD with all paper-design decisions resolved
# Last updated: March 2026

---

## HOW TO USE THIS FILE

Paste this at the start of a new Claude Code / Claude chat session:

"I'm building a hybrid-casual mobile game called Merge & March. Read this entire file — it contains the complete game design document with all systems designed, economy balanced, and implementation-ready specs. I want to continue prototyping from Phase 1."

---

## PART 1: PROJECT CONTEXT

### Who I Am
- Unity game developer, 7+ years experience (shipped Ludo King, Champion Chase)
- Solo dev / indie — building hybrid-casual games for Low DAU / High ARPU
- Using Claude Code + Unity MCP (Coplay) for rapid prototyping
- NOT great with art — need AI-generable assets or geometric styles
- Need procedural/infinite content (minimal manual content creation)
- Preferred input: decision-based (tap to merge/choose), NOT continuous control
- Want: active play sessions + idle auto-earn between sessions
- Tech: Unity 2022.3 LTS, 2D URP, DOTween, TextMeshPro

### Lessons from Gravity Sort (Failed Prototype)
1. Twist must be VISIBLE in a 3-second ad creative
2. Merged mechanics need different TYPES of thinking (not same skill + pressure)
3. If core loop isn't fun by Day 3, pivot immediately
4. Write kill criteria BEFORE coding
5. AI-assisted dev makes prototyping cheap — 20 hours to confident kill decision

### Why This Game
- Merge has the #1 ARPU of any casual subgenre at $14.83
- 101.5 rewarded videos per user (players WANT to watch ads for merge benefits)
- Roguelike auto-battle is Habby's entire portfolio ($5.7M/month across Archero, Capybara Go, Survivor.io, SOULS)
- Combining the highest-ARPU mechanic (merge) with the highest-revenue genre (roguelike auto-battle) creates the strongest possible foundation
- The 3-second ad creative: two small troops merge into glowing giant → enemies destroyed

---

## PART 2: CORE CONCEPT

**One-liner:** Drag to merge same troops into stronger warriors during auto-battles, pick roguelike upgrade cards between waves, and build an idle barracks that trains troops while you sleep.

**Genre:** Merge + Roguelike Auto-Battle + Idle
**Input:** Drag to merge (primary) + tap to choose cards (secondary)
**Session:** 3-5 minutes per run, then idle earnings accumulate offline
**Art style:** Cute/chibi characters, top-down or side-view. AI-generable sprites.

---

## PART 3: CORE LOOP

### ACTIVE PLAY (Per Run — ~4.5 minutes win, ~2-3 minutes loss)

1. Player selects starting lineup (4-8 troops based on unlocked slots)
2. Battle starts: troops placed on a 4-column × 2-row grid (8 slots)
3. Enemy waves march toward your troops from the right
4. Your troops AUTO-ATTACK — they fight on their own
5. Your ONLY actions during combat:
   - DRAG a troop onto another same-type, same-tier troop → they MERGE into a stronger version
   - DRAG troops to reposition them on the grid (front line vs back line)
   - Merging triggers **Tactical Slow-Mo**: combat slows to 20% speed while dragging, snaps back on release
6. Every 3 waves: pick 1 of 3 random upgrade cards (roguelike) — 5 card picks per run total
7. 15 waves + 1 boss wave (16 total)
8. Run ends when: all enemies cleared (WIN) or all your troops die (LOSE)
9. Win or lose, you keep earned coins + troop XP

### BETWEEN RUNS
- Spend coins to upgrade troop base stats (permanent)
- Open gacha chests for random troop cards
- Merge duplicate troop cards to unlock higher starting tiers
- Choose starting lineup for next run
- Progress to next zone on world map

### IDLE LAYER
- Barracks trains troops automatically over time
- Patrol missions: assign spare troops for timed coin/gem rewards
- War Chest generates passive coins
- Come back → collect trained troops + idle earnings → start a new run stronger

---

## PART 4: BATTLE GRID

### Layout: 4 Columns × 2 Rows (8 slots)

```
         Column 1    Column 2    Column 3    Column 4
         (Front)                              (Back)
Row 1:  [  Slot  ]  [  Slot  ]  [  Slot  ]  [  Slot  ]
Row 2:  [  Slot  ]  [  Slot  ]  [  Slot  ]  [  Slot  ]

                                              ← ENEMIES MARCH FROM RIGHT
```

- Columns represent range from enemies: Column 1 = frontline, Column 4 = backline
- Rows are two lanes; troops in the same column are "adjacent" for Healer purposes
- Player's grid is on the left side of the screen, enemies march in from the right
- Thumb-friendly: dragging happens in the lower half of the screen

### Starting Slots (Progression-Based)

- New players start with **4 of 8 slots filled** (left 2 + right 2 columns)
- Slot 5 unlocks at Player Level 3 (~run 5-6)
- Slot 6 unlocks at Player Level 5
- Slot 7 unlocks at Player Level 8
- Slot 8 unlocks at Player Level 12
- Each unlock is a noticeable power spike (e.g., slot 5 = first merge on wave 1)

---

## PART 5: MERGE SYSTEM

### Primary Input: Drag to Merge

- Touch a troop → drag onto another troop of SAME TYPE and SAME TIER
- Same type + same tier → they merge into next tier (Tier 1+1 = Tier 2)
- Different type or different tier → swap positions instead
- **One merge per drag.** Touch → slow-mo → merge or reposition → release → full speed. No chaining.

### Tactical Slow-Mo

When the player's finger touches a troop and starts dragging:
- Combat slows to 20% speed (Time.timeScale = 0.2f)
- Enemies still creep forward in slow motion (feels alive)
- Player calmly decides: merge or reposition
- On release: combat snaps back to full speed (Time.timeScale = 1.0f)
- The newly merged troop immediately impacts the battle

**Why this works:**
- Ad creative: combat happening → player drags → slow-mo → merge flash → BOOM → giant troop wrecks enemies
- Player feel: decision-based, not reflex-based
- Power fantasy: slow-mo → merge → speed snap = every merge feels like a special move
- Casual floor: players who prefer calm play can merge between waves instead

### Merge Tiers (3 tiers per type = 15 total troop visuals)

| Tier | How to Get | Power Multiplier | Atk Speed Bonus | Visual Change |
|------|-----------|-------------------|-----------------|---------------|
| Tier 1 | Spawned from barracks/gacha/deployment | 1× | 0% | Small, basic |
| Tier 2 | Merge two Tier 1 same type | 3× HP & Attack | 10% faster | Bigger, glowing outline |
| Tier 3 | Merge two Tier 2 same type | 9× HP & Attack | 20% faster | Large, full glow + particle aura |

**Key design rule:** Merging is ALWAYS worth it stat-wise. A Tier 2 is stronger than two separate Tier 1s. The merge decision is: "should I merge NOW or wait for a better merge opportunity?"

### Merge Animation (THE key visual moment — 0.75 seconds total)

1. Two troops slide toward each other (0.15s)
2. Both flash white (0.1s)
3. Small explosion of particles in troop color (0.2s)
4. New higher-tier troop POPS in with scale overshoot (0.2s, Ease.OutBack)
5. Subtle screen shake (0.1s)

This MUST feel amazing — it's the core dopamine hit.

---

## PART 6: TROOP REPLENISHMENT — DEPLOYMENT SYSTEM

### Problem Solved
Merging consumes troops (8 → 7 → 6...), combat kills troops. Without inflow, every run is a countdown to an empty grid.

### Solution: "Barracks Deployment" — Upgradeable Wave Reinforcements

At the start of each wave, the barracks "deploys" troops into empty grid slots.

### Deployment Level (Permanent Upgrade, Coin-Purchased)

| Level | Troops per Wave | Tier 2 Chance | Unlock Cost |
|-------|----------------|---------------|-------------|
| 1 (default) | 1 | 0% | Free |
| 2 | 1 | 20% | 500 coins |
| 3 | 2 | 20% | 2,000 coins |
| 4 | 2 | 30% | 8,000 coins |
| 5 | 2 | 50% | 25,000 coins |

### Deployment Type Selection

Default pool is **weighted by starting lineup composition:**
- If you brought 3 Archers and 1 Knight → spawn pool is ~60% Archer, ~20% Knight, ~10% Mage, ~10% Healer
- Bombers excluded from random spawns (single-use, too swingy)
- Roguelike cards override the pool (e.g., "All deployments are Archers this run")

### Monetization Touchpoint: Premium Deploy

When a reinforcement spawns, a small button flashes for 2 seconds: "Watch ad → upgrade this troop to next tier." Optional, non-intrusive, one per wave maximum. This is the rewarded video touchpoint that feeds the 101.5 rewarded videos per user benchmark.

---

## PART 7: TROOP DEATH

### When a Troop Dies Mid-Run:

1. Death animation plays
2. Grid slot is immediately marked **empty**
3. Next wave-start deployment treats it like any other empty slot
4. **Tier investment is lost** — you get a Tier 1 replacement, not the same tier

### Emotional Design

- Losing a Tier 1: "Whatever, I'll get another."
- Losing a Tier 2: "Ugh, that hurts but I can rebuild."
- Losing a Tier 3: "Disaster — I need to play around this."

This makes the Knight's front-row blocking role genuinely important (protecting your back-row Tier 3s).

### Monetization Touchpoint: Revive Ad

When a Tier 2 or Tier 3 troop dies, a small optional prompt flashes for 3 seconds: "Revive? Watch ad." Troop returns at full HP, same tier. Limited to **once per run**. High emotional willingness to watch — the player just lost something they invested multiple merges into.

---

## PART 8: TROOP SYSTEM

### 5 Base Troop Types

| Type | Role | Base HP | Base Attack | Atk Interval | Range | Special |
|------|------|---------|-------------|-------------|-------|---------|
| Knight | Tank | 100 | 15 | 2.0s | Melee (own column + 1 ahead) | Blocks enemies in front row |
| Archer | DPS | 40 | 12 | 0.8s | Infinite | Attacks from back row |
| Mage | AoE | 35 | 25 | 2.5s | Infinite (hits all enemies in nearest occupied column) | Area damage |
| Healer | Support | 60 | 0 | 3.0s (heal pulse) | Adjacent troops | Heals lowest-HP adjacent troop |
| Bomber | Burst | 20 | 80 | Instant on contact | On contact | Explodes (single use, respawns next wave) |

### Permanent Level Upgrades (Coin Sink)

- 20 levels per troop type
- Each level: +5% base HP and Attack
- Level 20 = 2.0× base stats (before tier multiplier)
- Applied before tier multiplier, so upgrades compound through merges

**Cost curve (×1.4 exponential):**
50, 75, 105, 147, 206, 288, 403, 565, 791, 1107, 1550, 2170, 3038, 4253, 5955, 8337, 11671, 16340, 22876

Total to max one type: ~81,000 coins. All five types: ~405,000 coins.

### Example: Maxed Tier 3 Archer

- Base: 40 HP, 12 Attack, 0.8s interval
- Level 20: 80 HP, 24 Attack
- Tier 3: 720 HP, 216 Attack, 0.65s interval
- DPS: 332 (vs Tier 1 Level 1 DPS of 15 — 22× stronger)

### Collection Merging (Meta-Merge Outside Runs)

Merge duplicate troop cards to permanently unlock higher starting tiers:

| Starting Tier | Cards Required | Cumulative Total |
|--------------|----------------|------------------|
| Tier 1 | 1 (own the card) | 1 |
| Tier 2 | 4 copies | 4 |
| Tier 3 | 4 Tier 2 tokens | 16 |

Starting a run with a Tier 3 Archer requires 16 Archer cards collected over time. Makes every gacha pull relevant — duplicates are fuel, not waste.

---

## PART 9: COMBAT RESOLUTION

### Targeting
- Each troop attacks the **nearest enemy** within range
- "Nearest" = shortest column distance (for ranged: any enemy on field)
- Ties broken by lowest row number

### Attack Loop
- Each troop has an independent timer based on its attack interval
- When timer reaches zero: play attack animation → deal damage → reset timer
- Timers start **staggered randomly** (0 to half-interval) at wave start to prevent sync pulsing

### Range by Type
- **Knight:** Own column + 1 column ahead (melee)
- **Archer:** Any enemy on the field (infinite range)
- **Mage:** All enemies in the nearest occupied enemy column (AoE)
- **Healer:** Adjacent troops (same column ± 1 row, or same row ± 1 column)
- **Bomber:** Enemy that reaches bomber's column triggers explosion

### Multiple Troops on Same Target
- **Yes.** All in-range troops can attack the same enemy.
- This ensures merged high-tier troops don't waste damage on already-dead enemies.

### Damage Formula (v1 — simple, tuneable)
```
damage = baseDamage × tierMultiplier × (1 + sumOfCardBonuses)
```
No defense stat for v1. Enemies just have HP. Armor can be added later as a tuning knob.

### Enemy Movement & Fail State
- Enemies spawn at right edge, walk left at their movement speed
- When an enemy reaches a column with a troop, it stops and attacks that troop
- If the troop dies, enemy resumes walking left
- **If any enemy reaches column 0 (past the grid), the run ends**
- This creates a natural fail state without a "lives" system

---

## PART 10: ENEMY SYSTEM

### 5 Enemy Types (Procedurally Combined)

| Enemy | Behavior | Counter |
|-------|----------|---------|
| Grunt | Walks forward, basic attack | Any troop |
| Rusher | Fast movement, low HP | Bomber or AoE |
| Tank | Slow, high HP, high damage | Focused DPS (merged archers) |
| Flyer | Skips front row, attacks back row | Mage or repositioned archers |
| Boss | Appears at wave 16, unique mechanics | High-tier merged troops |

### Procedural Wave Generation
Enemies are procedurally generated per wave: random combination of types, random count, scaling stats based on wave number and zone difficulty multiplier. **No hand-designed waves.**

### Boss (Wave 16)
First boss: High HP + AoE slam that damages entire front row every 5 seconds. Future zones introduce boss variants with different mechanics (shield phases, summon adds, enrage timers).

---

## PART 11: RUN STRUCTURE

### 15 Waves + Boss (Targeting ~4.5 min win, ~2-3 min loss)

| Phase | Waves | Enemies | Per Wave | Purpose |
|-------|-------|---------|----------|---------|
| Warmup | 1-3 | Grunts only | 3-5 enemies, ~8-10s combat | Fill grid via deployments, first merge opportunity |
| Escalation | 4-7 | Rushers (w4), Tanks (w6) | 6-8 enemies, ~10-12s combat | First Tier 2 merges, cards start mattering |
| Pressure | 8-11 | Flyers introduced | 8-10 enemies, ~12-15s combat | Tier 2 required to keep up, first Tier 3 possible |
| Final Push | 12-15 | All types combined | 10-12 enemies, ~12-15s combat | Underpowered players start losing troops |
| Boss | 16 | Single boss | ~30s combat | No card pick — fight with what you've built |

**Card picks after waves: 3, 6, 9, 12, 15** (5 total per run). Each pick maps to a strategic phase transition.

### Difficulty Scaling: Zone Multiplier

- Zone 1: 1.0× enemy stats
- Each zone adds +0.07×
- Zone 10: 1.63×, Zone 20: 2.33×, Zone 50: 4.43×
- Same 15+boss wave pattern, just harder numbers — zero hand-crafted content per zone

---

## PART 12: ROGUELIKE CARD SYSTEM

### Mechanic
After waves 3, 6, 9, 12, and 15: pick 1 of 3 random cards from the pool.

### Card Categories

| Category | Examples |
|----------|---------|
| Stat Boost | "+15% attack all troops", "+20% HP knights only" |
| Spawn | "Add a free Tier 1 [random type] to the grid" |
| Merge Boost | "Next merge creates +1 tier" (T1+T1 = T3 instead of T2) |
| Heal | "All troops heal 30%", "Revive one dead troop" |
| Special | "All archers attack 2× speed this wave", "Bomber doesn't die on explosion this wave" |
| Economy | "+50% coins earned this run", "Double loot from boss" |
| Deployment | "Deploy +1 troop this wave", "All deployments are Tier 2 this run", "Deploy a troop of your choice" |

### Pool Progression
- Starter pool: 15 cards
- Expands to 30+ as player unlocks troop types and Card Shop upgrades
- Cards are the primary driver of run variety — same troops + different card draws = completely different experiences

### Monetization: Card Reroll
Don't like any of the 3 cards? Pay 3 gems to reroll for 3 new options. Small but frequent gem sink.

---

## PART 13: CURRENCY ARCHITECTURE

### Three Currencies Only

**Coins (Soft Currency)**
- Earned: every run, idle patrol missions, War Chest, daily rewards
- Spent: troop stat upgrades, barracks upgrades, deployment level upgrades, armory
- Always scarce — the grind currency

**Gems (Hard Currency)**
- Earned (free): zone milestones, achievements, battle pass free track, daily ad chest
- Purchased: IAP gem packs
- Spent: gacha chest pulls, card rerolls, instant barracks collect, premium season pass
- Gems accelerate, they never gate. Anything gems buy, coins/time can eventually achieve.

**Troop Cards (Inventory Currency)**
- Earned: gacha chests, patrol missions, zone rewards
- "Spent": collection merging (duplicate cards → higher starting tiers), assigning to lineup
- Duplicates are always useful because of the merge system

---

## PART 14: UPGRADE SYSTEMS & SINKS

### Coin Sinks

| System | What It Does | Levels | Total Cost |
|--------|-------------|--------|------------|
| Troop Base Stats | +5% HP & Attack per level, per type | 20 per type (×5 types) | ~405,000 coins |
| Deployment Level | Better wave reinforcements | 5 | ~35,500 coins |
| Barracks Level | Faster troop training (8hr → 30min) | 10 | ~50,000 coins |
| Armory | Global +2% all stats per level | 20 | ~810,000 coins |
| War Chest | Passive coin generation (10→50 coins/hr) | 10 | ~100,000 coins |

**Total coin sink to max everything: ~1.4 million coins.** Months of play for free players.

### Gem Sinks

| System | Cost | Frequency |
|--------|------|-----------|
| Gacha Chest Pull | 50 gems (450 for 10-pull) | Primary gem sink |
| Card Reroll | 3 gems | Per use, mid-run |
| Instant Barracks Collect | 10 gems | Per use |
| Season Pass Premium | 500 gems | Monthly |

---

## PART 15: IDLE META SYSTEMS

### Barracks (Troop Training)

| Level | Training Time | Upgrade Cost |
|-------|--------------|-------------|
| 1 | 8 hours | Free |
| 2 | 6 hours | 1,000 coins |
| ... | ... | ×1.5 scaling |
| 10 | 30 minutes | ~50,000 coins |

Trains 1 random Tier 1 troop per cycle. Barracks level also connects to in-run deployment: higher barracks level can unlock higher base Deployment Level.

### Patrol Board

- Assign troops NOT in your starting lineup to timed missions
- 4-hour, 8-hour, or 12-hour missions (longer = better rewards)
- Rewards: coins, gems, or rare troop cards
- Gives purpose to extra/duplicate troops
- 3 mission slots always available

### War Chest

| Level | Passive Coins/Hour | Upgrade Cost |
|-------|-------------------|-------------|
| 1 | 10 | 500 coins |
| ... | ... | ×1.4 scaling |
| 10 | 50 | ~100,000 total |

### Card Shop
One-time unlocks that add new roguelike cards to the pool. Coin-purchased. Each unlock expands strategic options for future runs.

### Offline Earnings
On return, calculate: (hours offline × War Chest rate) + completed patrol missions + completed barracks training. Cap offline earnings at 12 hours to prevent indefinite accumulation.

---

## PART 16: ZONE / WORLD PROGRESSION

### Structure: Linear Zone Map with Branching Paths

- Each node on the map = one run (15 waves + boss)
- Every 5 zones: path branches into 2 options (harder with better rewards vs. easier and safer)
- Both branches converge at the next boss zone

### Zone Rewards

**Per-run earnings:**
- Win: 100 coins × zone multiplier (Zone 15 ≈ 200 coins)
- Lose: 40% of win amount
- Replaying cleared zones: 60% of first-clear reward

**Milestone zones (every 5th zone):**
- Harder boss + 10-25 gems + 1 gacha chest
- "I need to reach the next milestone" carrot

### Star System (Replay Value)
- 1 star: Clear the zone
- 2 stars: Clear with no troop deaths
- 3 stars: Clear under a time limit
- Stars unlock cosmetic rewards at thresholds (50 stars = grid skin, 100 stars = troop color variant)
- Stars never required for progression — purely completionist

### World Themes (Every 10 Zones)

| Zones | Theme | New Enemy Variant |
|-------|-------|-------------------|
| 1-10 | Grasslands | Base enemies |
| 11-20 | Desert | Sandstorm Rusher (periodic speed burst) |
| 21-30 | Snow | Ice Tank (slows attacked troops) |
| 31-40 | Lava | Fire Flyer (damages troops it passes over) |
| 41-50 | Shadow | Shadow Grunt (splits into 2 weaker copies on death) |

Minimal art cost (background recolors + 1 enemy variant per world), maximum sense of progression.

---

## PART 17: ECONOMY BALANCE

### Coin Earn Rates

| Source | Amount | Frequency |
|--------|--------|-----------|
| Run win | 100 × zone multiplier | Per run |
| Run loss | 40% of win | Per run |
| War Chest | 10-50/hr (by level) | Passive |
| Patrol missions | 50-200 per mission | 4h/8h/12h cycles |
| Daily login | 100 flat | Daily |
| Daily missions (all 3) | 200 total (50 each + 50 bonus) | Daily |

**Typical free player (6 runs/day, ~25 min play):** ~1,540 coins/day

**Spending rate feel:**
- Early game: multiple upgrades per day (fast, exciting)
- Mid game: one upgrade every 2-3 days (feels like a goal)
- Late game: one upgrade per week (drives IAP consideration)

### Gem Earn Rates (Free)

| Source | Amount | Frequency |
|--------|--------|-----------|
| Zone milestones | 10-25 | Every 5 zones |
| Achievements | 5-50 | One-time |
| Battle pass free track | ~150 total | Over 30 days |
| Daily ad chest | 5 | Daily (watch ad) |

**Free player total: ~300-400 gems/month.** One 10-pull gacha costs 450 gems → one 10-pull per 5-6 weeks. Industry standard for hybrid-casual.

---

## PART 18: MONETIZATION

### Revenue Placements

| Placement | Type | Details |
|-----------|------|---------|
| Troop gacha | IAP + Ads | Open 1 free every 4 hours; buy with gems (50/pull, 450/10-pull); watch ad for 1 extra |
| Card reroll | IAP (3 gems) | Reroll all 3 card options mid-run |
| Continue run | Rewarded ad | Died? Watch ad to revive all troops and continue |
| Troop revive | Rewarded ad | Tier 2+ troop died? Watch ad to revive it. Once per run, 3-second window |
| Premium deploy | Rewarded ad | Upgrade a deployment to next tier. Once per wave |
| Double idle earnings | Rewarded ad | 2× barracks/patrol/War Chest output |
| Gem packs | IAP | 50 ($0.99), 250 ($4.99), 500 ($9.99) |
| Starter Pack | IAP ($1.99) | One-time at Zone 3: 100 gems + 5,000 coins + 1 rare troop card |
| Season Pass | IAP ($4.99/month) | 500 gems. Exclusive skin, 2× barracks speed, pick 1 of 4 cards instead of 3 |
| Ad removal | IAP ($3.99) | Remove interstitials only, keep rewarded as optional |

### Why Gacha Works Here
The merge system creates natural demand for duplicates. You WANT 4+ of the same troop to collection-merge into higher starting tiers. Gacha pulls feel useful even for dupes.

---

## PART 19: RETENTION HOOKS

### Daily Login Calendar (7-Day Cycle, Repeating)

| Day | Reward |
|-----|--------|
| 1 | 100 coins |
| 2 | 1 gacha ticket |
| 3 | 200 coins |
| 4 | 5 gems |
| 5 | 1 rare gacha ticket |
| 6 | 500 coins |
| 7 | 25 gems + 1 epic gacha ticket |

Missing a day resets the streak.

### Daily Missions (3 per day, midnight refresh)

Always the same structure:
- "Complete X runs" (1-3)
- "Merge X troops in battle" (5-15)
- "Earn X coins" (scales with zone)

Each mission: 50 coins. All 3 complete: bonus chest (1 random troop card + 50 coins).

### Battle Pass (30-Day Season)

- 30 levels, ~1 level per day of normal play
- Each level requires 100 "battle points"
  - 50 per run win, 20 per loss
  - 30 per completed daily mission set
- **Free track:** coins, troop cards, small gem drops
- **Premium track (500 gems):** exclusive troop skin (immediate), 2× barracks speed, pick 1 of 4 cards, guaranteed epic troop card at level 30

### Weekly Boss Challenge (Saturday-Sunday)

- Special boss tougher than any zone boss
- 3 attempts per day (6 total over weekend)
- Leaderboard based on damage dealt
- Top rewards: 100 gems + exclusive troop card variant
- Participation reward: 25 gems + coins

---

## PART 20: TUTORIAL & ONBOARDING

### Zone 0: "Training Ground" (~2 minutes, scripted first run)

**Wave 1:** 2 troops pre-placed (1 Knight, 1 Archer). One grunt walks in. Troops auto-kill it. Text: "Your troops fight on their own."

**Wave 2:** Two grunts. They die. Player does nothing. Builds confidence.

**Wave 3:** Third troop deploys (another Knight). Pulsing arrow from new Knight to existing Knight. Player drags. **First merge.** Full animation + slow-mo. Text: "Same troops merge into stronger warriors!" Tier 2 Knight visibly wrecks next enemies faster.

**Wave 4:** Card selection introduced. Only 2 cards (not 3). One obviously better. Player taps. Text: "Choose upgrades between waves."

**Wave 5:** Mini-boss (high-HP grunt). Tier 2 Knight handles it. Win screen. Coins awarded. Taken to upgrade screen. One upgrade highlighted. Player taps. Zone 1 unlocked. Tutorial complete.

### Meta System Unlock Cadence

| Zone Clear | System Unlocked |
|-----------|----------------|
| Zone 0 | Core loop (merge + combat + cards) |
| Zone 1 | Gacha (one free chest given immediately) |
| Zone 3 | Barracks (first troop trains immediately, 30-min timer) |
| Zone 5 | Patrol Board |
| Zone 8 | Card Shop |
| Zone 10 | Battle Pass |
| Zone 15 | Weekly Boss |

First session (~20-30 min) covers Zones 0-3, unlocking core loop + gacha + barracks. Player leaves with a barracks timer ticking — the hook to come back.

---

## PART 21: SESSION FLOW & SCREEN MAP

### Screen 1: Main Menu / Zone Map (Home)
- Zone map dominates screen — winding path of nodes, current zone highlighted
- Bottom tab bar: **Battle** (zone map, default) | **Troops** | **Barracks** | **Shop**
- Tap zone node → Screen 2

### Screen 2: Lineup Select
- Shows 4-8 slots (based on progression)
- Drag troops from collection into slots
- "March!" button at bottom → Screen 3

### Screen 3: Battle
- Grid on left, enemies from right
- Top bar: wave counter, coins earned
- Minimal HUD — no clutter
- Merge by dragging (triggers slow-mo)
- Every 3 waves → Screen 3a

### Screen 3a: Card Selection (Overlay)
- 3 cards rise from bottom
- Player taps one
- Cards disappear, next wave starts

### Screen 4: Run Complete
- Wave reached, coins earned, star rating, troop card drops
- "Claim" button collects rewards
- Optional: "Watch ad → 2× coins" button
- Claim → back to Screen 1 (next zone unlocked if won)

### Screen 5: Troops Tab
- Grid of all owned troop types
- Tap troop → detail view: level, stats, upgrade cost, collection merge progress
- Upgrade button (coins), Merge button (if enough duplicates)

### Screen 6: Barracks Tab
- Visual barracks building with timer ("Next troop in 2:14:30")
- Patrol Board below: 3 mission slots
- Collect button when timers complete

### Screen 7: Shop Tab
- Gacha chests at top (free chest timer + gem purchase)
- IAP packs below
- Season Pass banner if active

### Transitions
- All screen transitions: quick slide (0.2s)
- Entering battle: 0.5s "marching" animation (loading mask)
- Back button always returns to Zone Map
- Push notifications: barracks ready, patrol complete, daily reset

---

## PART 22: VISUAL DESIGN

### Art Style: "Chibi Warriors"
- Characters: Small, cute, round proportions (head = 50% of body)
- 5 troop types × 3 tiers = 15 unique character sprites
- 5 enemy types × 3 size variants (recolors) = 15 enemy sprites
- Total: ~30 unique sprites + recolors

### AI Art Generation Plan
- **Troop sprites:** Midjourney or Ludo.ai
  - Prompt: "Chibi warrior character, [type], [tier description], front-facing sprite, flat colors, white outline, transparent background, mobile game style, cute proportions"
  - Generate all 30 sprites in one session for style consistency
  - Use Scenario.com to train custom model on first batch for style lock
- **UI icons:** Recraft.ai (card icons, currency icons)
- **Sound effects:** Freesound.org + ElevenLabs Sound Effects
- **Music:** Suno.ai ("epic fantasy mobile game music, lo-fi, 60 second loop")

### Colors
- Background: Dark gradient (#1A1A2E → #0D0D1A)
- Grid: Subtle lines (#2A2A4A)
- UI: White text, gold accents (#FFD84D)
- Merge flash: White → troop color
- Tier 1: Normal colors
- Tier 2: Brighter + white outline glow
- Tier 3: Full glow aura + particle effects

---

## PART 23: SOUND DESIGN

| Event | Sound |
|-------|-------|
| Drag troop | Soft "pick up" sound |
| Slow-mo activate | Subtle low-frequency whoosh |
| Merge (Tier 1→2) | Bright chime + "power up" |
| Merge (Tier 2→3) | Epic chord + explosion |
| Invalid merge | Dull "thud" |
| Slow-mo release | Quick speed-up whoosh |
| Troop attacks | Quick "slash/arrow/magic" per type |
| Enemy dies | Small pop |
| Boss appears | Dramatic horn |
| Wave complete | Victory chime |
| Card selected | Satisfying "stamp" sound |
| Deployment arrives | Short march + placement thud |
| Run complete (win) | Celebration fanfare |
| Run failed | Sad descending notes |
| Gacha open | Chest unlock + reveal |
| Barracks collection | Marching footsteps + coin sound |
| Background music | Epic but chill fantasy loop |

---

## PART 24: TECHNICAL ARCHITECTURE

```
Assets/_MergeAndMarch/
├── Scripts/
│   ├── Core/
│   │   ├── GameManager.cs         — State machine singleton
│   │   ├── RunManager.cs          — Single run lifecycle
│   │   ├── AudioManager.cs        
│   │   ├── CurrencyManager.cs     — Coins, Gems, Troop Cards
│   │   └── TimeScaleManager.cs    — Tactical slow-mo control
│   ├── Gameplay/
│   │   ├── BattleGrid.cs          — 4×2 grid, troop positions, slot management
│   │   ├── Troop.cs               — Individual troop component
│   │   ├── TroopData.cs           — ScriptableObject: type, tier, stats
│   │   ├── MergeController.cs     — Drag detection, merge validation, slow-mo trigger, animation
│   │   ├── AutoCombat.cs          — Troop auto-attack logic, type-based intervals, targeting
│   │   ├── DeploymentSystem.cs    — Wave-start reinforcements, pool weighting, level scaling
│   │   ├── EnemySpawner.cs        — Wave generation, difficulty scaling, zone multiplier
│   │   ├── Enemy.cs               — Enemy component, movement, attack AI
│   │   ├── WaveManager.cs         — 15+boss wave progression, phase transitions
│   │   └── CardSystem.cs          — Roguelike card pool, selection UI, reroll
│   ├── IdleSystem/
│   │   ├── Barracks.cs            — Troop training over time
│   │   ├── PatrolMission.cs       — Send troops on timed missions
│   │   ├── WarChest.cs            — Passive coin generation
│   │   ├── OfflineCalculator.cs   — Earnings since last session (12hr cap)
│   │   └── IdleUI.cs              — Barracks + patrol + war chest screens
│   ├── Meta/
│   │   ├── TroopCollection.cs     — All owned troops, inventory, collection merge
│   │   ├── GachaSystem.cs         — Chest opening, rarity rolls
│   │   ├── UpgradeManager.cs      — Permanent stat upgrades, deployment level, armory
│   │   ├── LineupSelector.cs      — Choose starting troops, slot progression
│   │   ├── ZoneManager.cs         — World map, zone progression, difficulty scaling
│   │   └── StarSystem.cs          — Per-zone star tracking, cosmetic unlocks
│   ├── Retention/
│   │   ├── DailyLogin.cs          — 7-day calendar, streak tracking
│   │   ├── DailyMissions.cs       — 3 daily missions, tracking, rewards
│   │   ├── BattlePass.cs          — Season pass, battle points, free/premium tracks
│   │   └── WeeklyBoss.cs          — Weekend event, leaderboard, attempts
│   ├── UI/
│   │   ├── BattleHUD.cs           — Wave counter, troop HP bars, coins
│   │   ├── CardSelectionUI.cs     — 3-card choice between waves
│   │   ├── RunCompleteUI.cs       — Results, rewards, continue/revive prompts
│   │   ├── MainMenuUI.cs          — Zone map, tab navigation
│   │   ├── LineupUI.cs            — Starting troop selection screen
│   │   ├── TroopDetailUI.cs       — Stats, upgrade, collection merge
│   │   ├── GachaUI.cs             — Chest opening animation
│   │   ├── ShopUI.cs              — IAP packs, gem store
│   │   └── TutorialUI.cs          — Zone 0 scripted overlays
│   └── Data/
│       ├── GameConfig.cs          — Global tuning values
│       ├── TroopDatabase.cs       — All troop types, tiers, base stats
│       ├── CardDatabase.cs        — All roguelike cards
│       ├── EnemyDatabase.cs       — All enemy types, per-wave scaling formulas
│       ├── ZoneDatabase.cs        — Zone multipliers, rewards, themes
│       ├── EconomyConfig.cs       — Earn rates, costs, upgrade curves
│       └── PlayerProgress.cs      — Save data (local + cloud sync)
├── Prefabs/
│   ├── Troops/ (15 prefabs: 5 types × 3 tiers)
│   ├── Enemies/ (5 base + recolors per world theme)
│   └── Particles/
│       ├── MergeFX.prefab
│       ├── AttackFX.prefab
│       ├── DeathFX.prefab
│       └── DeployFX.prefab
├── Sprites/
│   ├── Troops/ (AI-generated)
│   ├── Enemies/ (AI-generated)
│   ├── Cards/ (simple icons)
│   └── UI/
├── Audio/
│   ├── SFX/
│   └── Music/
├── ScriptableObjects/
│   ├── Troops/ (5 types × 3 tiers = 15 TroopData assets)
│   ├── Cards/ (15 starter + expansion cards)
│   ├── Enemies/ (5 types + world variants)
│   └── Zones/ (50 zone configs)
└── Scenes/
    ├── Game.unity (battle + card selection)
    ├── Meta.unity (zone map + troops + barracks + shop)
    └── Tutorial.unity (Zone 0 scripted run)
```

---

## PART 25: DEVELOPMENT PHASES

### Phase 1 (Days 1-3): Grid + Merge + Basic Combat
- 4×2 battle grid with troop placement
- Drag-to-merge mechanic (same type, same tier → next tier)
- Tactical slow-mo on drag (Time.timeScale = 0.2f)
- Merge animation (the satisfying "pop" — 0.75s sequence)
- Basic auto-combat: type-based intervals, nearest-enemy targeting
- Simple enemy spawner: waves of grunts
- Deployment system: 1 Tier 1 troop per wave into empty slots
- ✅ TEST: Is the merge gesture satisfying? Does slow-mo feel empowering? Does auto-combat feel engaging to watch?

### Phase 2 (Days 4-6): Roguelike Cards + Enemy Variety + Run Structure
- 15-wave + boss structure with phase pacing
- Card selection every 3 waves (1 of 3 cards, 5 picks per run)
- 15-card starter pool
- 5 enemy types with different behaviors
- Boss at wave 16
- Zone difficulty multiplier
- Score + coin rewards (win/loss differentiated)
- ✅ TEST: Do card choices create meaningful decisions? Does each run feel different? Is 4.5 minutes the right length?

### Phase 3 (Days 7-9): Troop System + Collection + Progression
- 5 troop types fully implemented with unique attack patterns
- 3 merge tiers per type with visual differences
- Permanent level upgrades (coin sink)
- Collection merging (meta-merge for higher starting tiers)
- Starting lineup selection (4 slots initially)
- Slot unlock progression
- Deployment level upgrades
- ✅ TEST: Does Tier 3 feel like a power fantasy? Does collection drive retention? Does upgrading feel impactful?

### Phase 4 (Days 10-12): Idle Layer + Meta Systems
- Barracks: auto-train troops over time (10 levels)
- Patrol missions: assign spare troops for rewards
- War Chest: passive coin generation
- Offline earnings calculation (12hr cap)
- Zone map with 10 zones (Grasslands world)
- Star system per zone
- Gacha system (chest opening, rarity rolls)
- Notification: "Your barracks trained 3 new troops!"
- ✅ TEST: Does the idle loop make you want to come back? Does the zone map provide clear goals?

### Phase 5 (Days 13-14): Retention + Polish + Flow
- Daily login calendar (7-day cycle)
- Daily missions (3 per day)
- Battle pass structure (free + premium tracks)
- Complete game flow: menu → lineup → battle → rewards → barracks → repeat
- Tutorial (Zone 0 scripted run)
- All sound effects + music
- Particle effects on merge, attacks, deaths, deployments
- Haptic feedback on merge
- UI polish + screen transitions
- Rewarded ad placements (revive, premium deploy, double idle)
- ✅ TEST: Record 3 minutes of gameplay. Does it look like an App Store game? Play for 30 minutes — do you want to keep going?

---

## PART 26: KILL CRITERIA

### KEEP GOING if:
- The merge gesture + slow-mo feels genuinely satisfying (Day 2)
- Watching auto-combat while planning your next merge is engaging
- Card choices create "hmm, which one?" moments
- You want to do "one more run" to try a different strategy
- Tier 3 troops feel powerful and earned
- The deployment system creates a natural flow of merge opportunities
- The 3-second ad creative (merge → giant troop → enemies destroyed) is compelling

### KILL / RETHINK if:
- Merge feels like busywork, not strategy
- Slow-mo feels like an interruption rather than empowerment
- Auto-combat is boring to watch (troops just stand and hit)
- Card choices feel random rather than strategic
- AI-generated art looks inconsistent across troop types
- Runs feel same-y despite roguelike elements
- The game feels like a worse Archero clone

---

## PART 27: TOOLS & WORKFLOW

### Development
- Unity 2022.3 LTS + 2D URP (bloom for merge glow effects)
- Claude Code + Coplay Unity MCP
- DOTween for all animations (especially merge pop + slow-mo transitions)
- ScriptableObjects for all config and troop/card/enemy/zone data

### MCP Setup
```bash
claude mcp add --scope user --transport stdio coplay-mcp \
  --env MCP_TOOL_TIMEOUT=720000 \
  -- uvx --python ">=3.11" coplay-mcp-server@latest
```

### AI Art Generation
- Troop sprites: Midjourney or Ludo.ai (15 unique + tier variants)
  - Prompt: "Chibi warrior character, [type], [tier description], front-facing sprite, flat colors, white outline, transparent background, mobile game style, cute proportions"
- Enemy sprites: Same tools, 5 base types with world-theme recolors
- UI icons: Recraft.ai (card icons, currency icons)
- Sound effects: Freesound.org + ElevenLabs Sound Effects
- Music: Suno.ai ("epic fantasy mobile game music, lo-fi, 60 second loop")
- Consistency tool: Scenario.com (train on first batch for style lock)

### Session Continuity
Use CLAUDE_CONTEXT.md in project root. Update after each session with current phase, completed work, and next steps.

---

*End of GDD v2.0 — Ready for Phase 1 prototyping.*
