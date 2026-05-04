# MERGE & MARCH - Claude Code Context File
# UPDATE THIS FILE at the end of every session.
# Read this file at the START of every session.

---

## PROJECT STATUS

**Current Phase:** Phase 2 - Roguelike Cards + Readability + Enemy Variety
**Session Count:** 14
**Last Session Date:** 2026-05-04
**Last Session Summary:** Session 14 fixed the Session 11 sprite scale problem at the source by moving troop sprite imports to 1100 PPU and enemy sprite imports to 1200 PPU, then reset runtime visual scales to readable multipliers (`troopBaseScale = 1`, `tierTwoScale = 1.15`, `tierThreeScale = 1.3`, `enemyBaseScale = 1`). Troop tier visual scaling now applies `troopBaseScale * tierMultiplier` while still normalizing art crop differences against Tier 1. Tier glow and merge highlight sizing now recalculates from current sprite bounds using small world-space padding, preventing T2/T3 glow from ballooning after merges. Damage numbers were reduced, now float only 0.4 world units, fade after halfway, use softer troop-hit red, and compact 1000+ values to `K` format. Empty-slot indicators are now generated hollow rounded-square outlines around 0.7 world units wide, hidden on occupied slots, visible again when slots clear, and gently pulsing between 20-35% alpha. Lane guides were tuned to soft white 16% alpha and markers to 18%. Runtime and editor assemblies build cleanly with zero warnings.
**Next Task:** Open Unity, let assets reimport, then do a focused Play Mode visual validation pass: troop fit in cells, T2/T3 size progression, adjacent-slot overlap, Grunt/Tank/Boss relative sizes, damage-number readability, empty-slot indicator visibility/pulse, lane guide strength, wave counter contrast, active-buffs panel fit, HP bar offsets, merge highlight sizing, drag colliders, and damage-number spawn positions. If those pass, record the requested fresh 60-second readability clip.

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
- Starting lineup distributes the four default troops across all four columns: Knight, Archer, Knight, Archer.
- Current tuning in `GameConfig`:
  - `cellSize = 1.15`
  - `gridOffset = (-1.725, -3.6)`
  - `troopBaseScale = 1`
  - `tierTwoScale = 1.15`
  - `tierThreeScale = 1.3`
  - `slotVisualScale = 0.61`
  - `enemyBaseScale = 1`
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
- `Enemy` now handles Bomber row-contact triggering, still applies Knight thorns when appropriate, spawns floating damage numbers for each damage instance, and shows attack impact FX when damaging troops.
- Enemy engagement now uses actual contact-range distance, so enemies no longer stop at spawn just because a troop exists far below in the same lane.
- `EnemySpawner` balances spawn columns within each wave instead of relying on pure random lane selection, preventing high-count waves from clustering heavily in the center by chance.
- Troop and enemy sprites now import at gameplay-sized PPUs instead of 100 PPU: troops use 1100 PPU and enemies use 1200 PPU. Troop tier visual sizing applies `troopBaseScale * tierMultiplier` and still normalizes each active tier sprite against that troop's Tier 1 cleaned sprite, preserving the intended 1.0x / 1.15x / 1.3x visual progression even when imported art crops differ.
- `Troop.MaxHP` now respects run HP buffs, and HP-boost cards heal the granted difference immediately.
- `Troop` now supports single-use Bomber behavior, `HasExplodedThisWave`, bomber respawn/reset on wave start, inactive-state drag lockout, tier-scaled healer support power, runtime HP bars, red troop-hit damage numbers, death-direction arrows, and upgraded tier-glow visuals with a T3 pulse.
- HP bars are World Space Canvas with UI Images using `fillAmount`. Hierarchy: Prefab -> `HPBarRoot` (`Canvas`) -> `Background` + `Fill`. `Fill` image is `Type=Filled`, `Method=Horizontal`, `Origin=Left`. References are assigned via Inspector. The stale merged-HP-bar bug is fixed.
- BattleGrid clears dead troops reliably so their slots reopen for next-wave deployment and revive/spawn cards.
- The three new troop visuals are currently communicated with lightweight prototype FX: Mage purple sweep, Healer green pulse, and Bomber orange explosion circle.
- `WaveManager` now builds a richer runtime HUD with merge count and active-buff text alongside the existing wave/coin labels.
- `CardSystem` now emits buff-change notifications and can summarize the currently active run buffs for HUD display.
- Starting lineup is still the default 2 Knights + 2 Archers for production flow, now distributed one per column. The runtime supports all 5 troop types and includes optional keyboard lineup presets for faster testing.
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

### Session 11 - 2026-04-27
**Duration:** Sprite assignment + clip-readiness presentation pass
**Built:** Added tier-aware troop sprite support and assigned all imported troop sprites to `Knight`, `Archer`, `Mage`, `Healer`, and `Bomber` data. Assigned sprites to `Grunt`, `Rusher`, `Tank`, and `Flyer`, added a dedicated `Boss.asset`, and updated wave 16 to use boss data when present. Fixed current sprite metas to Single, 100 PPU, Bilinear, no mipmaps, Tight mesh, centered pivot, and uncompressed texture settings; added an editor postprocessor to keep future reimports aligned. Removed main-sprite tinting when art sprites are present so sprites do not look washed out. Added runtime background gradient, softer segmented lane guides, hidden-on-occupied slot indicators, styled HUD bars, BOSS wave styling, coin icon, active-buff panel, animated WAVE CLEARED banner, and more polished runtime card UI with headers, category labels, rarity gems, and staggered entrance animation.
**Issues:** Font replacement was not completed because no new TTF was added to the project during this shell session. Actual Play Mode sizing/readability tuning and Recorder/OBS clip capture still need to happen in Unity's Game view. No DOTween install was performed.
**Verification:** `dotnet build Assembly-CSharp.csproj` and `dotnet build Assembly-CSharp-Editor.csproj` both pass with zero warnings.
**Next:** Let Unity reimport, manually inspect scale/HP bars/merge FX/card UI in Play Mode, make any by-eye scale tweaks, then record the 10s core-loop clip, 30s full-wave clip, and 60s progression highlight for external validation.
**Kill criteria status:** The project is materially closer to a shareable clip, but the session is not complete until the new visuals are watched in motion and the actual clips are captured.

### Session 12 - 2026-04-27
**Duration:** Correctness bug-fix session
**Built:** Fixed the post-import scale problem by normalizing each tier sprite's runtime scale against the troop's Tier 1 cleaned sprite, then set the configured progression to `0.1 / 0.115 / 0.13` so Tier 2 reads about 15% larger and Tier 3 about 30% larger without overflowing cells. Kept enemy art scale on the 100-PPU `enemyBaseScale = 0.1` path. Replaced pure-random enemy lane selection with per-wave balanced column selection to prevent high-count waves from stacking or clustering in the center. Fixed enemy contact logic so enemies only stop when within `enemyEngageDistance` of a valid troop instead of stopping far above the grid; Flyer frontline-skip behavior remains intact. Strengthened floating damage numbers with larger no-wrap TMP text, black outline, explicit rect size, and high Effects sorting.
**Issues:** Unity Game view still needs hands-on validation for sprite sizing, lane distribution, Flyer movement, and damage-number readability because this shell session could only compile-check behavior.
**Verification:** `dotnet build Assembly-CSharp.csproj` and `dotnet build Assembly-CSharp-Editor.csproj` both pass with zero warnings.
**Next:** Run Play Mode and verify the four Session 12 bugs visually before any polish or feature work. If the fixes hold, proceed to clip capture.
**Kill criteria status:** The code-level causes for the critical broken-looking gameplay clips are fixed, pending Unity Play Mode visual confirmation.

### Session 13 - 2026-04-28
**Duration:** Lane coverage + combat feedback fixes
**Built:** Confirmed and documented the distributed default starting lineup so all four columns begin covered. Confirmed Knight adjacent-column targeting is active and preserves same melee reach while preferring own-column targets on equal distance. Added red troop damage numbers for enemy hits, optional attacker tracking on `Troop.ApplyDamage`, cached killer-position death arrows, cyan Flyer slash FX on backline hits, and compact melee impact FX for other enemy attacks. Added a tiny runtime `CombatFeedbackFx` helper so these effects fade independently after the damaged troop is destroyed.
**Biggest perceived impact:** Expected to be Flyer slash FX plus red troop damage numbers, because back-row Mage/Healer damage should finally read as an enemy action instead of a sudden unexplained death.
**Issues:** Full Play Mode readability validation and the requested fresh 60-second clip still need to be recorded manually in Unity/OBS. No new gameplay bugs surfaced from shell verification.
**Verification:** `dotnet build Assembly-CSharp.csproj` and `dotnet build Assembly-CSharp-Editor.csproj` both pass with zero warnings.
**Next:** Run Play Mode through early, mid, and late waves to verify every troop damage instance has a readable hit, death arrows point toward the killer, and adjacent Knights meaningfully soften empty-column losses. Then record the 60-second readability clip.
**Kill criteria status:** The code-level clarity fixes are complete; final confirmation depends on Game view playtesting and footage capture.

### Session 14 - 2026-05-04
**Duration:** Focused visual sizing fix session
**Built:** Changed all current troop sprite metas to 1100 PPU and all current enemy sprite metas to 1200 PPU so art imports near the intended world size. Updated `MergeAndMarchSpritePostprocessor` so future reimports keep those PPU values by folder. Reset `GameConfig` and scene setup defaults to `troopBaseScale = 1`, `tierTwoScale = 1.15`, `tierThreeScale = 1.3`, and `enemyBaseScale = 1`. Fixed `Troop.ResolveTierVisualScale` so tier sizes are true multipliers on top of base scale, while still compensating for tier art crop differences. Recalibrated `TierGlow` and `MergeHighlight` child scales from the current sprite bounds using fixed world-space padding, so glow/highlight sizes stay proportional after T2/T3 merge upgrades and during merge pop animations. Reduced floating damage-number size and float distance, shifted fading to the second half of the lifetime, softened troop-hit red to `#FF8888`, added compact `K` formatting for 1000+ damage, and set `Boss.asset` `sizeScale` to 2 so Boss data itself is dramatic before wave scaling. Reworked empty slots into generated hollow rounded-square outlines around 0.7 world units wide, soft white, hidden when occupied, restored when cleared, and pulsing gently between 20-35% alpha. Tuned lane guides to 16% alpha and lane markers to 18%.
**Issues:** Unity MCP resources were not available in this shell session, so verification was limited to file inspection and C# builds. Unity still needs to reimport the changed sprite metas before Game view validation. The requested start-to-finish Play Mode run could not be performed from this environment.
**Verification:** `dotnet build Assembly-CSharp.csproj` and `dotnet build Assembly-CSharp-Editor.csproj` both pass with zero warnings. The first parallel editor build failed only because both builds tried to write the same temp DLL at once; rerunning editor build alone passed. Follow-up builds after the slot visual pass also passed with zero warnings.
**Next:** Open Unity and let the sprite reimport finish, then Play Mode check cell fit, tier progression, adjacent overlap, enemy relative sizes, damage-number readability, empty-slot outline readability/pulse, lane guide strength, wave counter contrast, active-buffs panel fit, HP bar offsets, merge highlight size, drag colliders, and damage-number spawn positions.
**Kill criteria status:** Code/import settings now match the requested visual sizing model; final confirmation depends on Game view validation.

---

## REFERENCE FILES

- `MERGE_AND_MARCH_GDD.md` - Full game design document
- `PHASE1_SETUP.md` - Unity setup notes
- `CLAUDE_CONTEXT.md` - Session continuity file
