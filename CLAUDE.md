# Forever Engine — Claude Code Project Rules

## What This Is
A Unity 6+ game runtime: hybrid DOTS/MonoBehaviour engine that hosts the playable RPG. Part of a four-repo ecosystem after the 2026-04-06 three-module consolidation pivot. Forever engine is the client; the brain and asset library live out-of-process in Python services.

## Architecture
- **ECS (DOTS + Burst + Job System):** per-frame game logic — fog, combat, AI inference, learning, pathfinding, entities
- **MonoBehaviour:** rendering (3D mesh scenes via asset packs + 2D Tilemap/Sprite legacy), camera, UI Toolkit, IMGUI HUDs, audio, input, bootstrap, HTTP bridges
- **Bridges/:** out-of-process client code
  - `AssetClient.cs` — coroutine HTTP client to Asset Manager (port 7801)
  - `DirectorClient.cs` — coroutine HTTP client to Director Hub (port 7802) with retry + backoff
  - `ServiceWatchdog.cs` — boot-time `/health` check on both services
  - `GameStateServer.cs` — HttpListener-backed read-only server on 127.0.0.1:7803 exposing live engine state to Director Hub's game_state_tool
  - `SharedSchemaTypes.cs` — auto-generated POCOs from `C:\Dev\.shared\codegen\csharp_gen.py`
  - `SmokeTestRunner.cs` — batchmode-friendly cross-module integration test
- **Demo/:** the playable RPG demo
  - `GameManager.cs` — singleton; constructs the bridge clients + watchdog + state server
  - `Demo/UI/DialoguePanel.cs` — UI Toolkit overlay routing player text through DirectorClient

  Note: client-side `DirectorEvents.cs` was deleted 2026-04-25; all gameplay → Director routing now goes through the server (`ForeverEngine.Server.DirectorBridge`) per Spec 3B.

## Repo Ecosystem (post-pivot)
- **Forever engine** (`C:\Dev\Forever engine`) — this repo. Game runtime.
- **Director Hub** (`C:\Dev\Director Hub`) — Python/FastAPI on port 7802. Agentic AI brain.
- **Asset Manager** (`C:\Dev\Asset Manager`) — Python/FastAPI on port 7801. Asset library + selectors + generators + AI gateway.
- **`.shared`** (`C:\Dev\.shared`) — Cross-module contract layer: JSON schemas, codegen, project state, docs.

## Rules

1. **Forever Engine is the source of truth for rules.** Stats, HP, dice resolution, item/spell mechanics — these never leave C#. The Director Hub interprets player intent; the engine resolves the math.
2. **Asset Manager is the only writer of the asset library.** Forever engine reads asset IDs over HTTP via AssetClient; never generate or modify asset files at runtime.
3. **Director Hub never touches the engine directly.** It returns structured DecisionPayload JSON the engine applies. No direct ECS writes.
4. **`.shared/` is the contract layer.** Schemas in `.shared/schemas/` are the single source of truth. If you change one, regenerate `Bridges/SharedSchemaTypes.cs` via:
   ```
   python C:/Dev/.shared/codegen/csharp_gen.py --out "C:/Dev/Forever engine/Assets/Scripts/Bridges/SharedSchemaTypes.cs"
   ```
5. **ECS for per-frame logic, MonoBehaviour for visuals + IO.** Per-frame AI (`AI/Inference`, `AI/Learning`, `AI/PlayerModeling`, `AI/SelfHealing`) stays in C#; LLM-driven planning and narrative live out-of-process in Director Hub.
6. **Burst-compile all Jobs.** Every IJob must have `[BurstCompile]`.
7. **NativeArray for map data.** Walkability, fog, elevation stored as NativeArrays for job access.
8. **Validate imports.** Always run `SchemaValidator` before loading external map/asset data.
9. **No game logic in MonoBehaviours that aren't bridges or UI.** The bridge classes in `Bridges/` and the UI panels are exempt — they're IO and presentation, not game logic.
10. **ScriptableObject for config.** All magic numbers go in `GameConfig` or per-feature config SOs, not hardcoded.
11. **Fault boundaries on bridge calls.** Use `SystemMonitor.GetOrCreate("name").TryExecute(...)` around any out-of-process call so repeated failures auto-disable rather than spam logs.

## Pivot reference
Full pivot specification: `C:\Dev\.shared\docs\superpowers\specs\2026-04-06-three-module-consolidation-design.md`
Implementation plan: `C:\Dev\.shared\docs\superpowers\plans\2026-04-06-three-module-consolidation-plan.md`
Pre-pivot brain code (archived): `C:\Dev\_archive\forever-engine-pre-pivot\`

## Key Files
- `Assets/Scripts/ECS/Components/` — All `IComponentData` structs
- `Assets/Scripts/ECS/Systems/` — All `ISystem`s
- `Assets/Scripts/ECS/Jobs/` — All Burst-compiled `IJob`s
- `Assets/Scripts/MonoBehaviour/Bootstrap/GameBootstrap.cs` — Entry point
- `Assets/Scripts/MonoBehaviour/Bootstrap/MapImporter.cs` — JSON → ECS bridge
- `Assets/Scripts/Shared/SchemaValidator.cs` — Cross-project contract enforcement
- `Assets/Scripts/Bridges/` — All HTTP clients + state server (see Architecture section above)
- `Assets/Scripts/Demo/GameManager.cs` — Singleton wiring the bridge clients into the demo
- `Assets/Scripts/AI/Inference/InferenceEngine.cs` — Sentis ONNX wrapper (per-frame, in-engine)
- `Assets/Scripts/AI/Inference/InferenceScheduler.cs` — Per-frame ms-budget batched inference with PerformanceRegulator feedback
- `Assets/Scripts/Demo/Battle/AIBehavior.cs` — deterministic combat behavior tree (Advance/Attack/Retreat/Flank/Hold/RangedAttack/ProtectAlly). Replaced Q-learning on 2026-04-16; Q-learning files (QLearner/QTableStore/SelfPlayTrainer/CombatBrain) were deleted.
- `Assets/Scripts/ECS/Components/AIBehaviorComponent.cs` — ECS-side combat AI component
- `Assets/Scripts/AI/Learning/DynamicDifficulty.cs` — runtime difficulty scaling (only file remaining in `AI/Learning/`)
- `Assets/Scripts/AI/SelfHealing/{SystemMonitor,FaultBoundary,AssetFaultHandler,PerformanceRegulator}.cs` — Runtime fault graph

## 3D Engine Transition (2026-04-09)

The engine is transitioning from 2D Tilemap/Sprite rendering to full 3D mesh scenes.
Full spec: `C:\Dev\.shared\docs\superpowers\specs\2026-04-09-3d-engine-transition-design.md`

Key components added for 3D:
- `Assets/Scripts/MonoBehaviour/Camera/PerspectiveCameraController.cs` — standalone 3D camera with orbit/zoom/follow
- `Packages/Tripo3d_Unity_Bridge/` — DCC Bridge for AI 3D model import (Tools → Tripo Bridge)
- 7 purchased Unity Asset Store 3D environment packs (34 GB in Assets/, all gitignored for license protection)

UI Toolkit / IMGUI panels (DialoguePanel, BattleHUD, etc.) and ECS game logic are renderer-independent and do NOT need to change for 3D.

The legacy 2D demo scene (`Game.unity`) and its renderers (`TileRenderer`, `FogRenderer`, `EntityRenderer`) were deleted 2026-04-25 — pre-3D-pivot artifacts that nothing loaded. The 3D world is delivered via `World.unity` / `GaiaWorld_*.unity` + Gaia + asset packs.

## Voxel layer (`Assets/Scripts/World/Voxel/`)

The voxel infrastructure (VoxelWorldManager + VoxelChunkStreamer + VoxelChunkRenderer + VoxelMeshBuilder + VoxelLodBucketer + MeshPool) is the **server→client transport for baked planet geometry** under the static-planet-mmo design (2026-04-20 GDT pivot). It is read-only — the planet is bake-once, NOT player-diggable in the current scope.

`VoxelWorldManager.RenderMeshes` defaults to **`false`** intentionally: visual scenes use 3D mesh assets from purchased Unity Asset Store packs, not voxel-generated meshes. Building voxel meshes would cost ~15 ms/chunk × 2205 worst-case = ~3-5 FPS for zero visible benefit. Voxel data still **streams + caches** in case future features (collision queries, line-of-sight) need it.

**Future-load-bearing:** the long-term game vision (per `outcome_objective.md` in the game-development-tracker) includes diggable layers below the surface — Underdark, the 9 Hells, the Abyss. The voxel data layer is the substrate for that future work; carving / mutation paths are deferred until after the planet exists. Do not extend voxel for diggability now. See `~/.claude/projects/C--Dev/game_dev_tracker/`.

### Systems Added 2026-04-13

- **Combat AI** — `AIBehavior.cs` deterministic behavior tree (Advance / Attack / Retreat / Flank / Hold / RangedAttack / ProtectAlly) selecting actions from distance + HP + per-enemy behavior tag. The earlier `CombatBrain.cs` 1296-state Q-learning system was deleted on 2026-04-16; its 7-action vocabulary survives in `AIBehavior`. Encounter-level adaptation is delegated to Director Hub's LLM via server-side `DirectorBridge`.
- **Seamless Battle Zones** (`BattleZone.cs`) — Per-enemy 8×8 grids replacing scene-based arenas
- **Room Decoration** (`RoomDecorator.cs` + `RoomCatalog.cs`) — Post-build prop placement from asset packs
- **Atmosphere System** (`AtmosphereSetup.cs`) — URP post-processing (bloom, tonemapping, color grading, vignette, fog)
- **UI Theme** (`UITheme.cs`) — Shared dark-fantasy IMGUI styling across all HUD panels
- **61 GLB Models** in `Resources/Models/` — 39 monsters + 22 NPCs, all registered in `ModelRegistry.cs`

### Systems Added 2026-04-14

- **Inventory UI** (`InventoryScreen.cs`) — Tab-toggled IMGUI panel with equipment slots, scrollable item list, equip/use/drop buttons. Uses `ItemRegistry.cs` for item ID → name reverse lookup.
- **Quest System** — End-to-end: QuestGiver NPC → Director Hub `/dialogue` with QUEST_TITLE markers → `GameManager.AcceptQuestFromResponse()` parser → `QuestSystem.StartQuest()`. Battle kills auto-progress active quest objectives.
- **Spell Panel** (`SpellPanel.cs`) — IMGUI overlay during combat showing prepared spells with hotkeys, damage/healing info, school, slot availability.
- **Level-Up Screen** (`LevelUpScreen.cs`) — Triggered on XP threshold. Shows ability score improvement (+2 points to allocate across STR/DEX/CON/INT/WIS/CHA).
- **Victory Screen** (`VictoryScreen.cs`) — Triggered when `castle_boss` (The Rot King) is defeated. Stats summary + return to menu.
- **Procedural Animation** (`ModelAnimator.cs`) — Transform-based animations on all combat models: idle bob, attack lunge, hit recoil, death topple.
- **Audio System** — `SoundManager.cs` wired to `AudioConfig.asset` (auto-populated by `AudioPopulator.cs` editor script).
- **Combat VFX** — Procedural `ParticleSystem` bursts in `BattleEffects.cs` for hit (white) and death (dark red) effects.
- **Save/Load Hardening** — Inventory serialization (parallel arrays), ModelId, MaxHunger/MaxThirst, DungeonState persistence, SaveVersion field for future migration.
- **17 Encounter Templates** — Up from 7. Spiders, treants, necromancers, mimics, gnolls, wraiths, cursed knights, and more across forest/dungeon/plains/ruins biomes.
- **Standalone Build** (`StandaloneBuild.cs`) — "Forever Engine → Build Standalone (Windows)" or batchmode `-executeMethod ForeverEngine.Editor.StandaloneBuild.Build`.

### Editor Menu Items
- **Forever Engine → Setup URP & Convert Materials** — converts all pack materials to URP
- **Forever Engine → Populate Room Catalog** — discovers dungeon prop prefabs
- **Forever Engine → Create Missing Assets** — creates GameConfig, RoomCatalog, DungeonNPCConfig SOs
- **Forever Engine → Create Dungeon Exploration Scene** — generates the dungeon scene
- **Forever Engine → Populate Audio Config** — discovers pack audio clips and assigns to AudioConfig SO
- **Forever Engine → Build Standalone (Windows)** — builds to `Builds/ShatteredKingdom/ShatteredKingdom.exe`

## Boot sequence
1. Demo scene loads → `GameManager.Awake()` constructs `AssetClient`, `DirectorClient`, `ServiceWatchdog`, `GameStateServer`
2. `GameManager.Start()` (coroutine) calls `Watchdog.CheckAll()` against both Python services
3. If services are down: log error, the rest of the engine continues in deterministic-fallback mode
4. If services are up: continue normal scene initialization
5. `GameStateServer` is now serving on `127.0.0.1:7803` for Director Hub's `game_state_tool`

## Running cross-module locally

```bash
# Boot both Python services in the background:
cd "C:/Dev/Director Hub" && uvicorn director_hub.bridge.server:app --port 7802 &
cd "C:/Dev/Asset Manager" && uvicorn asset_manager.bridge.server:app --port 7801 &

# Or use the docker-compose convenience:
cd C:/Dev/.shared && docker compose up
```

Then open Forever engine in the Editor and play the Demo scene. The cross-module SmokeTestRunner can be invoked headless via:

```bash
"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Unity.exe" \
  -batchmode -nographics -projectPath "C:/Dev/Forever engine" \
  -executeMethod ForeverEngine.Tests.SmokeTestRunner.Run -quit -logFile -
```
