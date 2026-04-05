# Forever Engine — Design Specification

**Date:** 2026-04-05
**Status:** Approved
**Origin:** Evolved from Map Generator pygame viewer (3,530 lines across 11 files)
**Vision:** Full GAME_ENGINE_PROMPT.md specification (C:\Users\bp303\Documents\Map Generator\GAME_ENGINE_PROMPT.md)

---

## 1. Architecture

**Hybrid DOTS/MonoBehaviour on Unity 6+**

- **ECS Layer (DOTS + Burst + Job System):** Game logic, AI, fog of war, combat, pathfinding, entity state
- **MonoBehaviour Layer:** Rendering (Tilemap + SpriteRenderer), Camera, UI Toolkit, Audio, Input, Scene bootstrap
- **Shared Layer:** Schema validation, cross-project data contracts, map import, asset import

## 2. Three-Project Ecosystem

| Project | Path | Role | Priority |
|---------|------|------|----------|
| Forever Engine | C:\Dev\Forever engin | Game runtime (Unity C#) | PRIMARY |
| Map Generator | C:\Dev\Map Generator | Procedural content pipeline (Python) | SECONDARY |
| Image Generator | C:\Dev\Image generator | Original AI asset generation (Python/JS) | DEFERRED |

**Merge target:** All three projects will merge into a single game engine. Each project builds against shared interface contracts at `C:\Dev\.shared\schemas\`.

**Data flow:**
- Map Generator → map_data.json + PNGs → Forever Engine imports at runtime
- Image Generator → sprites, textures, tilesets → Forever Engine Assets/
- Shared memory → C:\Dev\.shared\project_state.json → cross-project sync

## 3. System Rewrite Map

### ECS Systems (DOTS + Burst-compiled Jobs)

| System | Rewritten From | Job Type | Description |
|--------|---------------|----------|-------------|
| FogOfWarSystem | fog_of_war.py (97L) | IJobParallelFor | 360 parallel rays, NativeArray fog grid |
| GameStateSystem | game_engine.py (152L) | ISystem | State machine: Menu/Explore/Combat/Dialogue/Inventory/Pause/GameOver |
| CombatSystem | combat.py (126L) | IJob | D20 initiative, attack resolution, damage |
| AISystem | ai.py (117L) | IJobParallelFor | Batch NPC decisions: chase/guard/patrol/flee/wander |
| PathfindSystem | ai.py greedy chase | IJobParallelFor | Full A* per NPC, parallel |

### ECS Components

| Component | Rewritten From | Data |
|-----------|---------------|------|
| StatsComponent | Creature dataclass | D&D stat block, HP, AC, attack dice |
| PositionComponent | Creature.x/y/z | Tile-based grid position |
| CombatStateComponent | combat turn tracking | Token type, faction, turn resources |
| AIBehaviorComponent | ai_behavior field | AI type, detect/leash range, patrol state |
| FogStateComponent | fog state enum | Visibility per tile (Unexplored/Explored/Visible) |
| FogVisionComponent | sight radius | Tag for entities with fog vision |
| VisualComponent | renderer sprite refs | Sprite ID, variant, tint, scale |

### MonoBehaviour Systems

| System | Rewritten From | Description |
|--------|---------------|-------------|
| GameBootstrap | main.py main() | Scene init, ECS world setup, map loading |
| MapImporter | map_loader.py load_map() | JSON → ECS entities + NativeArrays |
| CameraController | camera.py Camera | Smooth follow, zoom, pan, parallax |
| TileRenderer | renderer.py | Unity Tilemap + procedural tile generation |
| EntityRenderer | renderer.py creatures | SpriteRenderer sync from ECS VisualComponent |
| UIManager | ui_overlay.py | UI Toolkit: HUD, combat log, minimap, menus |
| AudioManager | (new) | Unity AudioSource for SFX, music, ambient |
| InputManager | main.py event loop | Unity Input System with rebindable actions |

### New Systems (not in pygame)

| System | Layer | Description |
|--------|-------|-------------|
| InventorySystem | ECS | Item management, equipment, crafting |
| DialogueSystem | Mono | Branching conversations, condition checks |
| QuestSystem | ECS | State machine quests, objectives, rewards |
| SaveLoadSystem | ECS→Mono | Serialize ECS state to JSON, async on worker thread |
| MenuSystem | Mono | Main menu, pause, options, save slots |
| AnimationSystem | Mono | Sprite sheet animation controller |
| ParticleSystem | Mono | Unity VFX Graph for magic, fire, blood |

## 4. Shared Contract Schemas

All at `C:\Dev\.shared\schemas\`:

- **entity_schema.json** — Universal entity format with ECS components
- **map_schema.json** — Map data format (terrain, walkability, entities, transitions, spawns)
- **asset_manifest.json** — Asset catalog (sprites, tilesets, textures with IDs and metadata)
- **tile_spec.json** — Tile types, terrain definitions, themes, biomes

**Rule:** Forever Engine's format is the source of truth. Map Generator and Image Generator adapt their output to match.

## 5. Performance Targets (from GAME_ENGINE_PROMPT.md Phase 12)

| Metric | Target |
|--------|--------|
| Empty scene frame time | < 0.5ms |
| 10,000 sprites (2D) | 60 FPS |
| Fog raycast (360 rays) | < 0.1ms (Burst parallel) |
| A* pathfind (100 NPCs) | < 1ms (Burst parallel) |
| AI decisions (100 NPCs) | < 0.5ms (Burst parallel) |
| Map import (512x512) | < 1 second |

## 6. Directory Structure

```
C:\Dev\Forever engin\
├── Assets\
│   ├── Scripts\
│   │   ├── ECS\
│   │   │   ├── Components\     (StatsComponent, PositionComponent, etc.)
│   │   │   ├── Systems\        (FogOfWarSystem, GameStateSystem, etc.)
│   │   │   ├── Jobs\           (FogRaycastJob, PathfindJob, AIDecisionJob)
│   │   │   └── Data\           (MapDataBuffer, CombatLogEntry)
│   │   ├── MonoBehaviour\
│   │   │   ├── Bootstrap\      (GameBootstrap, MapImporter)
│   │   │   ├── Camera\         (CameraController)
│   │   │   ├── Rendering\      (TileRenderer, EntityRenderer, FogRenderer)
│   │   │   ├── UI\             (UIManager, HUD, CombatLog, Minimap, Menus)
│   │   │   ├── Audio\          (AudioManager)
│   │   │   └── Input\          (InputManager)
│   │   ├── Shared\             (SchemaValidator)
│   │   └── ScriptableObjects\  (GameConfig)
│   ├── Resources\
│   │   ├── Maps\               (Imported map_data.json + PNGs)
│   │   ├── Sprites\            (From Image Generator)
│   │   ├── Tilesets\           (From Image Generator)
│   │   ├── UI\                 (From Image Generator)
│   │   └── Audio\              (Sound effects, music)
│   ├── Scenes\                 (Bootstrap, MainMenu, Game)
│   ├── Prefabs\                (Creature tokens, UI panels)
│   └── ScriptableObjects\      (GameConfig instances)
├── docs\
│   └── superpowers\specs\      (This design doc)
└── tests\                      (Unity Test Framework)
```

## 7. Implementation Priority

**Phase 1 — Core Loop (rewrite pygame):**
1. ECS Components (Stats, Position, CombatState, AI, Fog, Visual)
2. MapImporter (JSON → ECS)
3. GameBootstrap (scene initialization)
4. FogOfWarSystem + FogRaycastJob
5. GameStateSystem (state machine)
6. CameraController
7. Basic TileRenderer (terrain PNG display)
8. Basic EntityRenderer (creature tokens)
9. Basic UIManager (HUD, combat log)
10. CombatSystem + initiative/attack
11. AISystem + PathfindJob + AIDecisionJob
12. InputManager (WASD, click-to-attack, interact)

**Phase 2 — Game Features (new systems):**
13. InventorySystem
14. DialogueSystem
15. QuestSystem
16. SaveLoadSystem
17. MenuSystem (main menu, pause, options)
18. AnimationSystem
19. AudioManager
20. ParticleSystem

**Phase 3 — Merge Preparation:**
21. Port Map Generator pipeline to C# (editor tool)
22. Port Image Generator to C# (asset pipeline tool)
23. Unified editor window for all three modules
