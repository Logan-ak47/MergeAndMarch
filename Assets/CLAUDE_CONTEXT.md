# MERGE & MARCH - Claude Code Context File
# UPDATE THIS FILE at the end of every session.
# Read this file at the START of every session.

---

## PROJECT STATUS

**Current Phase:** Phase 2 - Roguelike Cards + Readability + Enemy Variety
**Session Count:** 10
**Last Session Date:** 2026-04-23
**Last Session Summary:** Session 10 focused on the merge moment so short gameplay clips read as a cared-about hybrid-casual prototype instead of a raw placeholder. `MergeController` now runs a stronger three-beat merge sequence: both source troops flash white, a troop-colored particle burst fires at the merge point, the result troop pops in with a bouncy overshoot, and the camera gets a subtle shake. DOTween is still not present in `Packages/manifest.json`, so the pop/shake are implemented with unscaled coroutine animation to keep the project compiling. A runtime particle fallback was added so the burst works even before a prefab is wired. The requested baseline clip capture still needs to be recorded manually.
**Next Task:** Session 11: continue the clip-readiness pass, wire a dedicated `MergeBurstFX` prefab if desired, and capture before/after gameplay footage for honest public-facing A/B comparison.

---

## WHAT IS THIS GAME

Hybrid-casual mobile game. Drag to merge same troops into stronger warriors during auto-battles. Pick roguelike cards between waves. Idle barracks trains troops offline.

**The 3-second ad creative:** Two small troops merge -> slow-mo -> glowing giant troop appears -> enemies destroyed.

---

## TECH STACK

- Unity 2022.3 LTS, 2D URP
- DOTween (planned for final merge animation polish)
- TextMeshPro (UI text)
- ScriptableObjects for all data/config
- Coplay Unity MCP for editor control when available
- Target: Mobile (Android/iOS), portrait battle gameplay
- Project currently uses the **new Input System**

## PROJECT STRUCTURE

```
Assets/_MergeAndMarch/
|-- Scripts/
|   |-- Core/         - RunManager, TimeScaleManager, GridCameraFramer
|   |-- Gameplay/     - BattleGrid, Troop, MergeController, Enemy, EnemySpawner, AutoCombat, DeploymentSystem, WaveManager, CardSystem, RunBuffs
|   |-- Data/         - GameConfig, TroopData, TroopType, TroopTargeting, EnemyData, EnemyType, CardData, CardCategory, CardRarity, CardEffectType
|   |-- UI/           - CardSelectionUI
|   |-- Editor/       - Phase1SceneSetupTool
|-- Prefabs/
|-- ScriptableObjects/
|   |-- Cards/        - 20 starter card assets
|-- Scenes/
|   |-- Game.unity
```

---

## PHASE 1 STATUS

### Session 1: Grid + Troops on Screen
- [x] Create GameConfig ScriptableObject foundation
- [x] Create TroopData ScriptableObject foundation
- [x] Build BattleGrid for portrait layout
- [x] Vertical orientation: Row 0 (top) = frontline, Row 1 (bottom) = backline
- [x] Create Troop component with placeholder visuals
- [x] Create Troop prefab via editor setup tool
- [x] Create Knight TroopData
- [x] Create Archer TroopData
- [x] Place 4 troops in the center 2 columns: Knights on row 0, Archers on row 1
- [x] Camera framing for portrait with grid at the bottom and battlefield space above
- [x] Empty slot visuals
- [x] One-click editor setup tool to scaffold the scene without MCP
- **Current feel:** Grid is working in portrait and the scene can be regenerated from the Unity menu.

### Session 2: Drag to Merge + Slow-Mo
- [x] MergeController for drag start / update / release
- [x] TimeScaleManager: 0.2 on drag, reset on release
- [x] Drag troop follows pointer
- [x] Merge on same-type same-tier drop
- [x] Swap on different troop drop
- [x] Reposition on empty-slot drop
- [x] Return to origin on invalid drop
- [x] Tier state and tier visual scaling in Troop
- [x] One merge per drag
- [x] Input updated to the new Unity Input System
- [~] Merge animation is currently a lightweight code-driven placeholder; DOTween polish still pending
- **Current feel:** Core merge interaction is playable and aligned with the game identity.

### Session 3: Auto-Combat + Enemies
- [x] Create EnemyData ScriptableObject
- [x] Create Grunt EnemyData
- [x] Enemy component: spawn above the screen and march downward
- [x] AutoCombat system with vertical targeting rules
- [x] Basic fail state when enemies pass below the grid
- [x] Placeholder enemy prefab and scene wiring via Unity MCP
- [x] Enemy hit flash and troop attack/hit flash feedback
- [x] Troop collider sizing now follows visible troop size, improving drag reliability
- [x] Remove enemy x-jitter so lane alignment is clearer
- [x] Add enemy attack pulse feedback
- [x] Add lane-guide visuals so columns read during combat
- [x] Add target-aware Knight/Archer attack motion
- [~] Combat is now playable and more understandable, but still visually rough: no projectiles, no death FX, and wave/state feedback is still minimal
- **Current feel:** The prototype now has a readable battle loop, and the main remaining work is polish/tuning rather than missing combat fundamentals.

### Session 4: Combat readability polish
- [x] Remove enemy spawn jitter for cleaner lanes
- [x] Add lane-guide visuals above the battle grid
- [x] Add enemy attack pulse feedback
- [x] Add target-aware Knight/Archers attack motion
- [~] Unity MCP verification was flaky at the end of the session, so play confirmation needed a recheck
- **Current feel:** Combat columns and attack intent read much better, but ranged hits, deaths, and wave flow still needed support.

### Session 5: First full run loop
- [x] Archer attacks now fire a visible green projectile and deal damage on projectile arrival
- [x] Enemies now shrink/fade on death instead of disappearing instantly
- [x] Knight hits now add a punch-scale impact on enemies alongside the existing flash
- [x] Create `WaveManager` state-driven flow for waves 1-15 plus boss wave 16
- [x] Create `DeploymentSystem` and deploy one fresh Tier 1 troop into a random empty slot on waves 2+
- [x] Update `EnemySpawner` for staggered spawning, wave-clear tracking, boss tuning, and defeat notification
- [x] Add simple runtime HUD text for wave count, wave cleared, run end, and coins
- [x] Auto-restart the run 2 seconds after victory or defeat
- [x] Defeat now triggers on enemy escape or all troops dead
- [x] TMP import and troop sprite-scaling follow-up cleanup completed after the session
- [~] Play Mode smoke test passed and the loop runs, but I did not automate a full 15+boss completion from start to finish in-editor yet
- **Current feel:** The prototype has its first real run structure and is ready to answer whether the between-wave planning loop is fun enough to justify the card layer.

### Session 6: Roguelike card system
- [x] Create `CardData`, `CardCategory`, `CardRarity`, and `CardEffectType`
- [x] Create 15 starter `CardData` assets with exact starter values
- [x] Create `RunBuffs` runtime state container
- [x] Create `CardSystem` with weighted 3-card selection and no duplicates in a single pick
- [x] Create `CardSelectionUI` with 3 tappable runtime cards showing name, description, and rarity
- [x] Replace wave-card placeholder with actual card-pick flow after waves 3, 6, 9, 12, and 15
- [x] Apply card effects into combat, merge behavior, HP, deployment count, spawn cards, and coins
- [x] Reset run buffs on each new run
- [x] Keep deploy pop-in animation for deployed and spawned troops
- [~] Compile and Play Mode smoke tests are clean, but I did not do a full manual wave-3-through-wave-15 card-feel balancing pass yet
- **Current feel:** The run now has real strategic pivots between waves, which should meaningfully improve replayability once card balance is tuned.

### Session 7: Mage, Healer, Bomber troop types
- [x] Create `Mage`, `Healer`, and `Bomber` `TroopData` assets with correct prototype stats
- [x] Add explicit `TroopTargeting` data so troop behavior is data-driven instead of inferred only from type
- [x] Implement Mage AoE band attacks using nearest-enemy-by-Y targeting plus a full-width purple wave visual
- [x] Implement Healer support pulses targeting the lowest-HP adjacent ally with a green "+" effect
- [x] Implement Bomber single-use explosion behavior with row-contact triggering, AoE damage, wave reset, and inactive-state drag/merge lockout after explosion
- [x] Move Bomber triggering into `Enemy.Update` row-zone checks so the explosion happens on contact rather than on a normal combat timer
- [x] Add weighted deployment support so the non-Bomber deployment pool now includes Knight, Archer, Mage, and Healer with extra weight based on starting composition
- [x] Add 5 new troop-specific cards: Arcane Surge, Spawn Mage, Divine Light, Detonation, and Chain Reaction
- [x] Add optional debug lineup hotkeys in `RunManager` for fast composition playtesting (`1` all Knights, `2` default, `3` experimental Mage/Healer/Bomber mix)
- [~] Code compiles cleanly, but I have not yet done the requested 10+ hands-on composition test runs, so troop feel notes below are still provisional rather than playtest-validated
- **Current feel:** Mage has the clearest new battlefield read because the purple sweep communicates value instantly. Healer looks strategically promising because adjacency matters now, but may still read weaker than Mage until more sustained damage pressure is present in runs. Bomber should feel dramatic when it lands, but needs hands-on testing to confirm whether the trap timing feels smart or merely swingy.

---

## CURRENT IMPLEMENTATION NOTES

- Battle layout is still **portrait-only for prototype purposes**.
- Grid is centered horizontally and anchored near the bottom of the screen.
- Enemies enter from the **top** and move downward.
- Starting lineup uses the **center two columns**, not the outer columns.
- Current tuning in `GameConfig`:
  - `cellSize = 1.15`
  - `gridOffset = (-1.725, -3.6)`
  - `troopBaseScale = 3.6`
  - `tierTwoScale = 4.3`
  - `tierThreeScale = 5.0`
  - `enemyBaseScale = 3.2`
  - `laneGuideHeight = 5.8`
  - `laneGuideWidthScale = 0.1`
  - `laneGuideMarkerScale = 0.16`
  - `enemySpawnIntervalMin = 0.3`
  - `enemySpawnIntervalMax = 0.5`
- Camera framing is handled by `GridCameraFramer`.
- Scene generation is handled by `Phase1SceneSetupTool`.
- The active battle scene is currently `Assets/Scenes/Game.unity`.
- Archer damage is no longer instant; it now lands on projectile arrival.
- Enemy logic still uses lane-based engagement: enemies stop when they are logically attacking a troop in their assigned column.
- `WaveManager` now owns the run lifecycle: wave start, card pick, deployment, victory, defeat, and restart.
- `CardSystem` now owns run buffs, weighted card rolls, effect application, and card-pick completion signaling.
- `CardSelectionUI` builds a simple runtime 3-card selection panel on the overlay canvas.
- `EnemySpawner` handles per-wave enemy composition (`GetWaveEnemies`), staggered spawns, HP/scale multipliers, the boss wave, and wave-clear tracking. Wave 16 remains a scaled Grunt boss. Rusher/Tank/Flyer data auto-load in editor via `AssetDatabase`; null data falls back to grunt.
- `EnemyData` now has `sizeScale` (base visual scale, multiplied by `enemyBaseScale`), `skipsFrontline` (Flyer: skip Row 0 troops and Row 0 bombers), and `renderYOffset` (Flyer: shift spawn +0.3 Y so it renders above the frontline lane).
- `DeploymentSystem` can now deploy multiple troops for a single wave, handles card-driven troop spawn effects, excludes Bomber from random deployment, and weights normal deployments toward the starting lineup composition.
- `MergeController` now consumes one-shot merge boosts, applies merge-heal buffs to adjacent troops, and reports successful merges into the HUD merge counter.
- Merge highlighting is now working consistently in play: valid targets pulse, non-valid troops dim, and the earlier "sometimes nothing happens" merge-feedback issue does not appear to be reproducing after the Session 9 fix pass.
- `MergeController` now gives merges a clearer mini-cutscene: source troops flash white, a troop-colored merge burst fires at the destination, the upgraded troop pops in with a stronger overshoot, and the camera gets a subtle shake. This is coroutine-driven for now because DOTween is still not installed in the project manifest.
- `AutoCombat` now routes troop actions through explicit targeting types: ranged/melee attacks, Mage AoE bands, and Healer support pulses. Bomber triggering is handled by enemy row contact instead of the normal combat tick.
- `Enemy` now handles Bomber row-contact triggering, still applies Knight thorns when appropriate, and spawns floating damage numbers for each damage instance.
- `Troop.MaxHP` now respects run HP buffs, and HP-boost cards heal the granted difference immediately.
- `Troop` now supports single-use Bomber behavior, `HasExplodedThisWave`, bomber respawn/reset on wave start, inactive-state drag lockout, tier-scaled healer support power, runtime HP bars, and upgraded tier-glow visuals with a T3 pulse.
- HP bars are World Space Canvas with UI Images using `fillAmount`. Hierarchy: Prefab -> `HPBarRoot` (`Canvas`) -> `Background` + `Fill`. `Fill` image is `Type=Filled`, `Method=Horizontal`, `Origin=Left`. References are assigned via Inspector. The stale merged-HP-bar bug is fixed.
- BattleGrid clears dead troops reliably so their slots reopen for next-wave deployment and revive/spawn cards.
- The three new troop visuals are currently communicated with lightweight prototype FX: Mage purple sweep, Healer green pulse, and Bomber orange explosion circle.
- `WaveManager` now builds a richer runtime HUD with merge count and active-buff text alongside the existing wave/coin labels.
- `CardSystem` now emits buff-change notifications and can summarize the currently active run buffs for HUD display.
- Starting lineup is still the default 2 Knights + 2 Archers for production flow, but the runtime now supports all 5 troop types and includes optional keyboard lineup presets for faster testing.
- TextMeshPro is imported and the temporary fallback path is no longer needed.
- Troop placeholder sprite tiling warnings were removed by switching to simple sprite scaling.
- There are now 20 starter cards in the pool instead of 15.
- I have not yet completed the requested 10+ manual playtest runs with different compositions, so strength/weakness judgments are currently based on implementation expectations rather than direct play sessions.

---

## CRITICAL DESIGN RULES

1. **Merging is ALWAYS worth it.** T2 > two T1s stat-wise. The decision is timing, not whether.
2. **One merge per drag.** Touch -> slow-mo -> one action -> release -> full speed. No chaining.
3. **Troops fight on their own.** Player NEVER controls attacks. Only merge, reposition, pick cards.
4. **Slow-mo on drag.** 20% time scale while finger is down. Snap back on release.
5. **Decision-based input only.** No continuous control, no reflex tests.
6. **Troop death = slot opens immediately.** Next wave deployment fills it. Tier investment is lost.
7. **Portrait battlefield.** Grid at bottom, enemies from top.

---

## NUMBERS QUICK REFERENCE

### Troop Base Stats (Tier 1, Level 1)

| Type | HP | Attack | Interval | Targeting |
|------|----|--------|----------|-----------|
| Knight | 100 | 15 | 2.0s | Melee (own row + 1 row above) |
| Archer | 40 | 12 | 0.8s | Nearest enemy (infinite, by Y distance) |
| Mage | 35 | 25 | 2.5s | AoE horizontal band |
| Healer | 60 | 0 (heals 15) | 3.0s | Lowest-HP adjacent troop |
| Bomber | 20 | 80 | Instant | On contact |

### Tier Multipliers

| Tier | HP/Atk Multiplier | Atk Speed |
|------|-------------------|-----------|
| 1 | 1.0x | 1.0x |
| 2 | 3.0x | 0.9x |
| 3 | 9.0x | 0.8x |

### Grid

- 4 columns x 2 rows = 8 slots
- Row 0 (top) = frontline
- Row 1 (bottom) = backline
- Grid centered horizontally, anchored to bottom area of portrait screen
- Enemies spawn above camera top edge and move downward
- Fail state: enemy passes below the troop rows

---

## SESSION LOG

### Session 1 - 2026-03-28
**Duration:** Prototype setup session
**Built:** Core data classes, troop component, battle grid, run manager, prefab/asset/scene generation editor tool, portrait scene framing foundation.
**Issues:** Initial camera framing and old horizontal assumptions had to be corrected after the design switched to portrait.
**Next:** Add merge interaction and slow-mo.
**Kill criteria status:** Partially testable.

### Session 2 - 2026-03-28
**Duration:** Merge interaction session
**Built:** Drag-to-merge, reposition, swap, tactical slow-mo, tier visuals, Input System support, portrait-centered bottom grid layout, camera framing updates, larger placeholder troop sizing.
**Issues:** Troop readability required multiple scale passes; DOTween-quality merge juice still pending.
**Next:** Build Session 3 enemies and auto-combat coming from the top.
**Kill criteria status:** Merge interaction is testable; combat is not yet implemented.

### Session 3 - 2026-04-02
**Duration:** Combat foundation session
**Built:** Enemy data/assets, grunt spawning from above, downward movement, lane-based enemy engagement, troop auto-combat, enemy/troop HP damage flow, fail state when enemies escape, placeholder enemy prefab/setup through Unity MCP, enemy sizing cleanup, troop collider fixes, and simple attack/hit flash feedback.
**Issues:** Combat reads better now, but still lacks projectiles/death FX; some enemies appear stationary because lane engagement is not visually communicated strongly enough; Phase1 setup docs and context needed syncing with the actual scene state.
**Next:** Improve combat readability and wave feel, then decide whether to finish Phase 1 with deployment polish or move into the card/wave structure work.
**Kill criteria status:** Core merge + combat loop is now testable.

### Session 4 - 2026-04-03
**Duration:** Combat readability polish session
**Built:** Removed enemy spawn jitter, added lane-guide visuals above the battle grid, added enemy attack pulse feedback, and added target-aware attack motion for Knight and Archer so combat columns and attack intent are easier to read.
**Issues:** Unity MCP connectivity became flaky during verification, so final in-editor compile/play confirmation should be rechecked next session; combat is clearer now but still lacks projectiles, death FX, and wave UI.
**Next:** Tune lane guide strength/height and attack motion distances in play, then add clearer ranged/projectile or death feedback.
**Kill criteria status:** Core battle readability is improving and worth continuing.

### Session 5 - 2026-04-07
**Duration:** Combat feedback + run loop session
**Built:** Archer projectile feedback with delayed damage, enemy death shrink/fade, knight impact punch, WaveManager-driven 15+boss run flow, staggered wave spawning, coins tracking, between-wave deployment, boss wave tuning, defeat/victory flow, runtime HUD text, and automatic run restart.
**Issues:** TMP essentials are now imported and the temporary font fallback has been removed; troop placeholder sprite tiling warnings are fixed by using simple sprite scaling; full hands-off verification of a complete 15+boss win run is still recommended next session.
**Next:** Build Session 6 card rewards between waves now that the run structure is in place.
**Kill criteria status:** The game can now support a real start-to-finish run and is ready for the “one more run?” evaluation after tuning.

### Session 6 - 2026-04-08
**Duration:** Roguelike cards session
**Built:** Card data architecture, 15 starter card assets, weighted runtime card selection, RunBuffs runtime state, simple 3-card selection UI, and gameplay wiring so cards now affect attack, HP, speed, thorns, merge upgrades, merge healing, deployment count, troop spawns, and coins.
**Issues:** The system compiles and smoke-tests cleanly, but I have not yet done a full manual balancing pass across repeated wave-3/6/9/12/15 choices or a complete end-to-end run where every major card category is exercised deliberately.
**Next:** Add Mage, Healer, and Bomber troop types for Session 7.
**Card feel testing:** Not fully hands-on balanced yet. Provisional expectation is that Fusion Surge, spawn cards, and Bounty Hunter will feel strongest immediately; Merge Aura and Iron Skin may read weaker until combat pressure and board damage are tuned more aggressively.
**Kill criteria status:** The run now has real strategic variation between waves and is much closer to a true “one more run” loop.

### Session 7 - 2026-04-16
**Duration:** New troop types + support systems session
**Built:** Mage AoE band combat with full-width purple sweep, Healer adjacency-based support pulses, Bomber row-contact explosion behavior with wave reset, explicit troop targeting data, Bomber inactive-state drag/merge lockout, weighted deployment using starting composition, 5 new troop-specific cards, and optional debug lineup hotkeys for composition testing.
**Issues:** I have not yet done the requested 10+ manual composition test runs, so troop feel is still provisional; Mage is expected to read best immediately because its effect is the most visible, while Healer may feel weakest until sustained incoming damage makes support value more obvious. Bomber trap timing and radius value still need hands-on validation.
**Next:** Build Session 8 enemy variety with Rusher, Tank, and Flyer.
**Provisional troop feel:** Mage currently looks like the strongest readability win, Healer is most likely to need tuning or stronger combat pressure to feel impactful, and Bomber may need trap timing/radius tuning depending on how often waves naturally enter its trigger zone.
**Kill criteria status:** The troop roster now has real mechanical variety, but composition quality and relative troop value still need live playtesting to confirm.

### Session 8 - 2026-04-22
**Duration:** Two-part session — readability pass + enemy variety
**Part 1 Built:** Runtime troop/enemy HP bars that only show when damaged, clearer T2/T3 tier visuals with glow treatment and a subtle T3 pulse using unscaled time, floating world-space damage numbers for enemy hits, an active-buffs HUD panel, and a merge counter integrated into the top HUD.
**Part 3 Built:** Two counter-enemy cards — "Piercing Arrow" (Rare, Archer 2× vs Tank) and "Ground Slam" (Rare, Knights can hit Flyers in backline). Added `ArcherDoubleDamageVsTank` and `KnightDamageFlyers` to `CardEffectType`. Added `archerVsTankMultiplier` and `knightCanHitFlyers` to `RunBuffs`. Updated `AutoCombat.ResolveAttack` to check the Tank multiplier when an Archer fires at a Tank, and updated `FindTargetFor` so Knights skip Flyers by default and target them only when `knightCanHitFlyers` is active. Both buffs appear in the active-buffs HUD panel. Both card assets auto-load via `FindAssets` scan of the Cards folder.
**Part 2 Built:** Three new enemy types — Rusher (fast/fragile, yellow-orange, scale 0.85), Tank (slow/massive HP, dark red, scale 1.3), and Flyer (skips Row 0 frontline, attacks backline directly, cyan, renderYOffset 0.3). Added `sizeScale`, `skipsFrontline`, and `renderYOffset` fields to `EnemyData`. Updated `Enemy.cs` to apply `renderYOffset` to spawn position, skip Row 0 troops/bombers for Flyers in `FindBlockingTroop` and `TryTriggerBomberInRowZone`. Rewrote `EnemySpawner` with `GetWaveEnemies()` composition logic covering waves 1–15 (grunts-only early, then Rushers wave 4-5, Tank wave 6, Flyer wave 8, full mix waves 9+). Created `Rusher.asset`, `Tank.asset`, and `Flyer.asset` ScriptableObjects.
**Issues:** Enemy type visuals are still prototype (color tint only — no distinct sprites). Flyer visual distinction relies on cyan tint + 0.3 Y offset; a "wing" sprite would strengthen it but requires art. Row 0 bombers intentionally do NOT trigger from Flyers (mechanical reward for Row 1 bomber placement).
**Next:** Session 9 — playtesting and tuning pass across the full 15-wave run, or next Phase 2 feature.
**Kill criteria status:** The enemy roster now has real mechanical variety. Rusher punishes slow builds, Tank punishes Tier-1 swarms, Flyer punishes pure-frontline strategies. Readability improvements from Part 1 should make the new threats legible immediately.

### Session 9 - 2026-04-22
**Duration:** Readability + stability polish session
**Built:** Reliable merge-target highlighting, troop/enemy HP bar stabilization, cleanup on earlier enemy-variety work, and follow-up automated run reports under `Assets/_MergeAndMarch/TestReports/` to establish an initial win-rate baseline.
**Issues:** Session bookkeeping had drifted toward balance/tuning even though presentation polish was becoming the more urgent bottleneck for public-facing clips.
**Next:** Put polish directly onto the merge moment so viewers judge the core idea rather than the placeholder snap-change.
**Kill criteria status:** Readability improved, but the merge still looked too prototype-heavy for a 10-second public clip.

### Session 10 - 2026-04-23
**Duration:** Merge juice / clip-readiness session
**Built:** Reworked `MergeController` merge feedback into a clearer three-beat sequence: white flash on both source troops, troop-colored merge burst at the target slot, subtle merge camera shake, and a stronger bouncy pop-in for the upgraded troop. Added serialized tuning fields plus a runtime-configured particle-system fallback so the effect works immediately even without a prefab assignment.
**Issues:** DOTween is still not installed in the project manifest, so the requested pop/shake were recreated with coroutine easing instead of DOTween calls. The requested baseline recording step still needs to be done manually in-editor or with external capture.
**Next:** Continue presentation polish beyond the merge moment, decide whether to install DOTween and/or author a dedicated `MergeBurstFX` prefab asset, then capture before/after footage for honest external feedback.
**Kill criteria status:** The core merge beat should read much less like a raw prototype, but public-clip readiness still needs a hands-on visual pass in Game view and actual footage capture.

---

## REFERENCE FILES

- `MERGE_AND_MARCH_GDD.md` - Full game design document
- `PHASE1_SETUP.md` - Unity setup notes
- `CLAUDE_CONTEXT.md` - Session continuity file
