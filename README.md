# Merge & March

Merge & March is a hybrid-casual mobile prototype built in Unity where the player drags units to merge them into stronger troops during auto-battles. The long-term design combines three layers:

- In-run tactical merging during combat
- Roguelike card choices between waves
- Idle/meta progression between sessions

The current repository is focused on the Phase 1 prototype slice: portrait battle grid, drag-to-merge, tactical slow-mo, basic enemy spawning, and readable auto-combat foundations.

## Purpose

This project exists to validate the core fantasy quickly:

- Merging during live combat should feel satisfying and strategically clear
- Auto-battle should be readable enough that players can plan merges while watching the fight
- The prototype should support fast iteration before expanding into cards, deployment, idle systems, and meta progression

The target experience is a mobile-friendly, decision-based battle game rather than a reflex-heavy action game.

## Current Status

Current implementation is centered on a playable combat prototype with:

- 4x2 portrait battle grid
- Starting troop placement for Knights and Archers
- Drag-to-merge, swap, reposition, and invalid-drop recovery
- Tactical slow-mo while dragging
- Tier-based troop scaling
- Enemy spawning from the top of the screen
- Lane-based enemy engagement and fail state on escape
- Auto-combat for troops and enemies
- Runtime lane guides and simple attack feedback for readability
- ScriptableObject-driven config/data setup
- Editor tooling to scaffold the scene and core assets

The game design already defines later systems such as deployment, cards, bosses, meta progression, barracks, patrols, and monetization hooks, but those are mostly planned rather than implemented in this repo today.

## Core Game Loop

The intended loop is:

1. Start a run with a small troop lineup
2. Watch troops auto-fight while enemies march downward
3. Drag matching troops together to merge them into stronger tiers
4. Reposition units when needed
5. Survive waves and eventually layer in card choices, deployment, and progression systems

For the current prototype, the most important loop to evaluate is simply:

`read battlefield -> drag -> slow-mo decision -> merge or reposition -> resume combat`

## Project Architecture

The codebase is organized around a small prototype-friendly architecture:

### Data-driven configuration

Gameplay values live in ScriptableObjects so tuning can happen without rewriting systems:

- `GameConfig` holds shared battle/grid tuning
- `TroopData` defines troop stats and role data
- `EnemyData` defines enemy stats and movement/combat data

### Runtime systems

The current runtime is split into a few focused areas:

- `Core`
  - Run/session orchestration
  - Camera framing
  - Time scaling for tactical slow-mo
- `Gameplay`
  - Grid ownership and slot logic
  - Troop behavior and tier state
  - Drag/merge/reposition flow
  - Enemy spawning and movement
  - Auto-combat and target selection
- `Data`
  - Enums and ScriptableObject definitions for troops, enemies, and config
- `Editor`
  - One-click setup tooling for scaffolding the Phase 1 scene and assets

### Design direction

The repository intentionally separates:

- Prototype-ready systems that need fast iteration
- Data assets for tuning
- Editor utilities for rebuilding scene state quickly

This keeps the project easy to regenerate and reduces manual Unity scene setup work during rapid prototyping.

## Repository Layout

```text
Assets/
|-- CLAUDE_CONTEXT.md              Session continuity + current project state
|-- MERGE_AND_MARCH_GDD.md         Full game design document
|-- PHASE1_SETUP.md                Phase 1 setup and wiring notes
|-- Scenes/
|   |-- Game.unity                 Main playable battle scene
|-- _MergeAndMarch/
|   |-- Editor/                    Unity editor setup tooling
|   |-- Prefabs/                   Troop and enemy prefabs
|   |-- ScriptableObjects/         GameConfig and prototype troop/enemy data assets
|   |-- Scripts/
|   |   |-- Core/                  Run lifecycle, camera framing, time scale
|   |   |-- Data/                  Config/data definitions
|   |   |-- Gameplay/              Grid, troops, enemies, combat, merging
```

## Key Scripts

Important current gameplay scripts include:

- `BattleGrid` - owns slot layout, troop placement, lane guide visuals, and spatial helpers
- `Troop` - troop state, tier handling, visuals, combat hooks, and grid interaction
- `MergeController` - drag input, merge validation, swapping, repositioning, and slow-mo triggering
- `AutoCombat` - troop attack timing, targeting, and damage application
- `Enemy` - enemy movement, lane engagement, combat state, and fail-state interaction
- `EnemySpawner` - prototype wave spawning
- `RunManager` - high-level scene orchestration and startup flow
- `GridCameraFramer` - portrait framing for the battlefield
- `TimeScaleManager` - tactical slow-mo control
- `Phase1SceneSetupTool` - editor utility to scaffold the prototype scene

## Primary Documents

These files are the main source of truth when continuing development:

- [Assets/CLAUDE_CONTEXT.md](Assets/CLAUDE_CONTEXT.md)  
  Current phase, recent session history, implementation notes, and next-step guidance.

- [Assets/MERGE_AND_MARCH_GDD.md](Assets/MERGE_AND_MARCH_GDD.md)  
  Full design document covering the intended game loop, systems, progression, economy, and later-phase architecture.

- [Assets/PHASE1_SETUP.md](Assets/PHASE1_SETUP.md)  
  Unity setup notes for the early prototype slice.

## How To Work In This Repo

Recommended flow for a new development session:

1. Read `Assets/CLAUDE_CONTEXT.md`
2. Read `Assets/MERGE_AND_MARCH_GDD.md` for system intent
3. Check the current scene and script structure under `Assets/_MergeAndMarch`
4. Treat Phase 1 prototype clarity and combat readability as the immediate priority unless intentionally expanding scope

## Tech Stack

- Unity 2022.3 LTS
- 2D URP
- C#
- New Unity Input System
- ScriptableObjects for config/data
- TextMeshPro for UI
- DOTween planned for later animation polish

## Prototype Priorities

Current priorities, based on the context files, are:

- Improve combat readability
- Add stronger attack/death feedback
- Validate drag/merge feel on touch devices
- Build forward from a solid battle foundation instead of expanding too early

## Notes

- This repository is a prototype-first project, so the GDD is intentionally broader than the implemented code.
- The active playable scene is `Assets/Scenes/Game.unity`.
- The current phase is documented in `Assets/CLAUDE_CONTEXT.md` and should be updated at the end of each work session.
