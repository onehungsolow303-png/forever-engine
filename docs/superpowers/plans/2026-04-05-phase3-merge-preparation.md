# Phase 3: Merge Preparation — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Port the Map Generator pipeline and Image Generator asset system into C# Unity editor tools, creating a unified engine with built-in content creation.

**Architecture:** Editor scripts (UnityEditor namespace) that run inside the Unity Editor as custom windows. The generation pipeline becomes a C# port of the Python agents. The asset generator becomes a procedural sprite/texture creation tool.

**Tech Stack:** Unity 6+ Editor API, C# noise libraries, NativeArray for terrain data, ScriptableObject for profiles/configs

---

## File Map

### Task 1 — Generation Data Structures (C# port of Python data/)

| File | Responsibility |
|------|---------------|
| `Assets/Scripts/Generation/Data/GenerationRequest.cs` | Map generation parameters |
| `Assets/Scripts/Generation/Data/MapProfile.cs` | 30+ map type profiles |
| `Assets/Scripts/Generation/Data/RoomGraph.cs` | Abstract room topology |
| `Assets/Scripts/Generation/Data/RoomPurpose.cs` | Room purposes + adjacency rules |
| `Assets/Scripts/Generation/Data/GameTables.cs` | D&D 5e encounter/loot budgets |
| `Assets/Tests/EditMode/GenerationDataTests.cs` | Profile validation, table lookups |

### Task 2 — Terrain Generation (C# port of terrain_agent + cave_carver)

| File | Responsibility |
|------|---------------|
| `Assets/Scripts/Generation/Agents/TerrainGenerator.cs` | Perlin noise elevation/moisture |
| `Assets/Scripts/Generation/Agents/CaveCarver.cs` | Cellular automata cave carving |
| `Assets/Scripts/Generation/Utility/PerlinNoise.cs` | Multi-octave gradient noise |
| `Assets/Tests/EditMode/TerrainGenerationTests.cs` | Noise range, biome coverage, cave connectivity |

### Task 3 — Layout Generation (C# port of topology + structure + connector)

| File | Responsibility |
|------|---------------|
| `Assets/Scripts/Generation/Agents/TopologyBuilder.cs` | 4 topology types (linear, loop, hub, hybrid) |
| `Assets/Scripts/Generation/Agents/RoomPlacer.cs` | BSP room placement with adjacency |
| `Assets/Scripts/Generation/Agents/CorridorCarver.cs` | Connect rooms with corridors |
| `Assets/Tests/EditMode/LayoutGenerationTests.cs` | Room connectivity, overlap detection |

### Task 4 — Population Generation (C# port of encounter + trap + loot + dressing)

| File | Responsibility |
|------|---------------|
| `Assets/Scripts/Generation/Agents/PopulationGenerator.cs` | Encounters, traps, loot, dressing in one pass |
| `Assets/Tests/EditMode/PopulationTests.cs` | Budget scaling, spawn placement |

### Task 5 — Pipeline Coordinator (C# port of coordinator.py)

| File | Responsibility |
|------|---------------|
| `Assets/Scripts/Generation/PipelineCoordinator.cs` | 3-phase orchestration with validation |
| `Assets/Tests/EditMode/PipelineTests.cs` | Full pipeline execution test |

### Task 6 — Asset Generator (procedural sprite/texture creation)

| File | Responsibility |
|------|---------------|
| `Assets/Scripts/AssetGeneration/ProceduralSpriteGenerator.cs` | Generate creature tokens, items, tiles |
| `Assets/Scripts/AssetGeneration/TextureGenerator.cs` | Generate terrain textures, tileset patterns |
| `Assets/Scripts/AssetGeneration/AssetManifestBuilder.cs` | Build asset_manifest.json from generated assets |

### Task 7 — Unified Editor Window

| File | Responsibility |
|------|---------------|
| `Assets/Editor/ForeverEngineEditor.cs` | Main editor window with tabs for all tools |
| `Assets/Editor/GenerationTab.cs` | Map generation UI (replaces Python GUI) |
| `Assets/Editor/AssetGenerationTab.cs` | Asset creation UI |
| `Assets/Editor/MapPreviewTab.cs` | Preview generated maps in editor |

---

## Task 1: Generation Data Structures

**Files:**
- Create: `Assets/Scripts/Generation/Data/GenerationRequest.cs`
- Create: `Assets/Scripts/Generation/Data/MapProfile.cs`
- Create: `Assets/Scripts/Generation/Data/RoomGraph.cs`
- Create: `Assets/Scripts/Generation/Data/RoomPurpose.cs`
- Create: `Assets/Scripts/Generation/Data/GameTables.cs`
- Create: `Assets/Tests/EditMode/GenerationDataTests.cs`

All data structures ported from Map Generator Python. Static readonly data, no Unity runtime dependencies.

- [ ] **Step 1: Write tests**

Tests verify profiles load, tables have expected values, request validation works.

- [ ] **Step 2: Implement all 5 data files**

GenerationRequest: map_type, biome, width, height, seed, party_level, party_size with Validate().
MapProfile: 30+ profiles with room pools, creature tables, topology preferences.
RoomGraph: Nodes (id, x, y, w, h, purpose) + Edges (from, to, type) + topology type enum.
RoomPurpose: 13 purposes with encounter/trap/loot multipliers + adjacency rules.
GameTables: XP budgets by party level, loot tier multipliers.

- [ ] **Step 3: Commit** `feat: Generation data structures — profiles, rooms, game tables`

---

## Task 2: Terrain Generation

**Files:**
- Create: `Assets/Scripts/Generation/Utility/PerlinNoise.cs`
- Create: `Assets/Scripts/Generation/Agents/TerrainGenerator.cs`
- Create: `Assets/Scripts/Generation/Agents/CaveCarver.cs`
- Create: `Assets/Tests/EditMode/TerrainGenerationTests.cs`

Port Perlin noise, terrain coloring, biome assignment, cellular automata cave carving.

- [ ] **Step 1: Write tests** (noise in 0-1 range, terrain produces walkable area, caves are connected)
- [ ] **Step 2: Implement PerlinNoise** (multi-octave gradient noise, seeded)
- [ ] **Step 3: Implement TerrainGenerator** (elevation + moisture → terrain color + walkability)
- [ ] **Step 4: Implement CaveCarver** (threshold carving + cellular automata smoothing + flood fill)
- [ ] **Step 5: Commit** `feat: Terrain generation with Perlin noise and cave carving`

---

## Task 3: Layout Generation

**Files:**
- Create: `Assets/Scripts/Generation/Agents/TopologyBuilder.cs`
- Create: `Assets/Scripts/Generation/Agents/RoomPlacer.cs`
- Create: `Assets/Scripts/Generation/Agents/CorridorCarver.cs`
- Create: `Assets/Tests/EditMode/LayoutGenerationTests.cs`

Port room graph generation, BSP placement, corridor connection.

- [ ] **Step 1: Write tests** (topology produces connected graph, rooms don't overlap, all rooms reachable)
- [ ] **Step 2: Implement TopologyBuilder** (4 algorithms: linear_with_branches, loop_based, hub_and_spoke, hybrid)
- [ ] **Step 3: Implement RoomPlacer** (BSP subdivision, room sizing from profile, adjacency enforcement)
- [ ] **Step 4: Implement CorridorCarver** (L-shaped corridors between room centers, door placement)
- [ ] **Step 5: Commit** `feat: Layout generation with 4 topology types and BSP rooms`

---

## Task 4: Population Generation

**Files:**
- Create: `Assets/Scripts/Generation/Agents/PopulationGenerator.cs`
- Create: `Assets/Tests/EditMode/PopulationTests.cs`

Port encounter, trap, loot, and dressing placement into a single pass.

- [ ] **Step 1: Write tests** (encounters scale with party level, traps respect density, loot within budget)
- [ ] **Step 2: Implement PopulationGenerator** (room purpose → encounters + traps + loot + dressing)
- [ ] **Step 3: Commit** `feat: Population generation with D&D 5e encounter budgets`

---

## Task 5: Pipeline Coordinator

**Files:**
- Create: `Assets/Scripts/Generation/PipelineCoordinator.cs`
- Create: `Assets/Tests/EditMode/PipelineTests.cs`

Port the 3-phase orchestration with validation and retry logic.

- [ ] **Step 1: Write tests** (full pipeline produces valid map data, validation catches errors)
- [ ] **Step 2: Implement PipelineCoordinator** (Phase 1→2→3 with validation gates and retry)
- [ ] **Step 3: Commit** `feat: PipelineCoordinator with 3-phase generation and validation`

---

## Task 6: Asset Generator

**Files:**
- Create: `Assets/Scripts/AssetGeneration/ProceduralSpriteGenerator.cs`
- Create: `Assets/Scripts/AssetGeneration/TextureGenerator.cs`
- Create: `Assets/Scripts/AssetGeneration/AssetManifestBuilder.cs`

Procedural asset creation — generates sprites and textures programmatically.

- [ ] **Step 1: Implement ProceduralSpriteGenerator** (colored circles for tokens, simple shapes for items)
- [ ] **Step 2: Implement TextureGenerator** (noise-based terrain textures, pattern tilesets)
- [ ] **Step 3: Implement AssetManifestBuilder** (scans generated assets, builds manifest JSON)
- [ ] **Step 4: Commit** `feat: Procedural asset generator for sprites, textures, and manifest`

---

## Task 7: Unified Editor Window

**Files:**
- Create: `Assets/Editor/ForeverEngineEditor.cs`
- Create: `Assets/Editor/GenerationTab.cs`
- Create: `Assets/Editor/AssetGenerationTab.cs`
- Create: `Assets/Editor/MapPreviewTab.cs`

Unity Editor window that replaces the Python GUI for map generation.

- [ ] **Step 1: Implement ForeverEngineEditor** (main EditorWindow with tab bar)
- [ ] **Step 2: Implement GenerationTab** (map type, biome, size, seed, generate button)
- [ ] **Step 3: Implement AssetGenerationTab** (sprite/texture generation controls)
- [ ] **Step 4: Implement MapPreviewTab** (preview generated terrain in editor)
- [ ] **Step 5: Commit** `feat: Unified editor window with generation, assets, and preview tabs`

---

## Summary

| Task | What | New Files | Tests |
|------|------|-----------|-------|
| 1 | Generation data structures | 6 | 6 |
| 2 | Terrain + cave generation | 4 | 4 |
| 3 | Layout generation | 4 | 4 |
| 4 | Population generation | 2 | 3 |
| 5 | Pipeline coordinator | 2 | 2 |
| 6 | Asset generator | 3 | 0 |
| 7 | Editor window | 4 | 0 |
| **Total** | | **25 files** | **19 tests** |
