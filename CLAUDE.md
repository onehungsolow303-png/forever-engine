# Forever Engine — Claude Code Project Rules

## What This Is
A hybrid DOTS/MonoBehaviour game engine built on Unity 6+, evolved from the Map Generator pygame viewer. Part of a three-project ecosystem that will eventually merge into a single game engine.

## Architecture
- **ECS (DOTS + Burst + Job System):** All game logic — fog, combat, AI, pathfinding, entities
- **MonoBehaviour:** Rendering (Tilemap/Sprite), Camera, UI Toolkit, Audio, Input, Bootstrap
- **Shared contracts:** C:\Dev\.shared\schemas\ — entity, map, asset, tile schemas

## Project Ecosystem
- **Forever Engine** (C:\Dev\Forever engin) — PRIMARY, game runtime
- **Map Generator** (C:\Dev\Map Generator) — SECONDARY, procedural content pipeline
- **Image Generator** (C:\Dev\Image generator) — DEFERRED, needs pivot to original asset generation
- **Shared Memory** (C:\Dev\.shared\) — cross-project state, schemas, priorities

## Rules
1. **Forever Engine format is source of truth.** Other projects adapt to our schemas.
2. **ECS for logic, MonoBehaviour for visuals.** Never put game logic in MonoBehaviours.
3. **Burst-compile all Jobs.** Every IJob must have [BurstCompile] attribute.
4. **NativeArray for map data.** Walkability, fog, elevation stored as NativeArrays for job access.
5. **Validate imports.** Always run SchemaValidator before loading external map/asset data.
6. **Cross-project changes.** If you modify a shared schema, update C:\Dev\.shared\project_state.json.
7. **No pygame code.** This is Unity C#. Reference pygame viewer for design intent, not implementation.
8. **ScriptableObject for config.** All magic numbers go in GameConfig, not hardcoded in scripts.

## Design Spec
Full specification: docs/superpowers/specs/2026-04-05-forever-engine-design.md

## Key Files
- Assets/Scripts/ECS/Components/ — All IComponentData structs
- Assets/Scripts/ECS/Systems/ — All ISystems
- Assets/Scripts/ECS/Jobs/ — All Burst-compiled IJobs
- Assets/Scripts/MonoBehaviour/Bootstrap/GameBootstrap.cs — Entry point
- Assets/Scripts/MonoBehaviour/Bootstrap/MapImporter.cs — JSON → ECS bridge
- Assets/Scripts/Shared/SchemaValidator.cs — Cross-project contract enforcement
