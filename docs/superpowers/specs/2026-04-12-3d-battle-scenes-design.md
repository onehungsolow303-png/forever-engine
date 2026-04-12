# 3D Battle Scenes Design Spec

## Goal

Replace the 2D procedural grid battle renderer with 3D dungeon room prefabs, BG3-style grid overlay, 3D character models, and mouse+keyboard hybrid input. Keep the existing `BattleManager` turn logic and combat resolution intact.

## Architecture

### Unchanged Systems

- **BattleManager** — turn loop, combat resolution, AI, victory/loss. No changes to game logic.
- **BattleGrid** — walkability array, obstacle tracking. Still the gameplay truth layer.
- **BattleCombatant** — stats, position (int X/Y on grid), factory methods from PlayerData/EnemyDef.
- **EncounterData / EnemyDef** — enemy definitions, dynamic encounter generation, XP budgets.

### New Components

#### BattleSceneTemplate (ScriptableObject)

Defines a battle room:
- `GameObject RoomPrefab` — reference to a dungeon room prefab (from Multistory Dungeons 2, Lordenfel, Eternal Temple, or NatureManufacture packs)
- `int GridWidth, GridHeight` — grid dimensions for this room (default 8x8, up to 16x16)
- `Vector2Int[] PlayerSpawnZone` — cells where the player/party spawns
- `Vector2Int[] EnemySpawnZone` — cells where enemies spawn
- `Vector2Int[] BossSpawnPoints` — specific positions for boss encounters (center throne, flanking positions)
- `string Biome` — "forest", "dungeon", "castle", "temple"
- `bool IsBossArena` — boss rooms are more intricate but still part of the biome pool
- `Light[] SceneLights` — position/color/intensity for room lighting

~30-40 base templates across biomes. Boss arenas are premium-tier templates within the same biome pool — more elaborate geometry, boss-specific spawn choreography (center position, flanking minion spots), but drawn from the pool like any other room.

#### BattleVariation

Runtime randomizer applied to any non-boss template:
- Random prop placement (barrels, crates, pillars) as obstacles within walkable grid
- Lighting color/intensity variation
- Room rotation (0/90/180/270)
- Obstacle density variation
- Updates `BattleGrid.Walkable[]` to reflect placed props

30 shells x 8+ variations = 240+ unique-feeling encounters. Boss arenas skip variation to preserve their designed layout.

#### BattleRenderer3D

Replaces `BattleRenderer`. Responsibilities:
- Instantiate room prefab from template
- Apply variation (props, lighting, rotation)
- Generate grid overlay mesh (Y=0.01 above floor, only when needed)
- Spawn 3D character models at grid positions
- Manage damage number popups (floating text)
- Handle death visuals (tilt + alpha fade)
- Turn indicator (scale pulse on active combatant)

#### BattleInputController

Mouse + keyboard hybrid:
- **Raycast** from camera through mouse onto grid plane to detect hovered cell (x, y)
- **Move action:** click walkable tile to move there. Preview path on hover.
- **Attack action:** click enemy to attack (if in range).
- **Keyboard shortcuts:** WASD (move), F/1 (attack nearest), Q (spell menu), H (potion), Space (end turn) — all still work.
- **Hover info:** hovering an enemy shows inspect tooltip (name + visible conditions only, no HP).

#### PerspectiveCameraController (reuse)

Same camera controller as overworld, with battle-specific defaults:
- Tighter zoom range (8-20 units instead of 5-40)
- Higher elevation angle (55-60 degrees for tactical overview)
- Limited orbit range (prevent looking under the floor)
- Follow target = center of the battle grid
- Right-click drag orbit, scroll zoom (same controls as overworld)

## Grid Overlay

On-demand visibility — grid only appears when player selects a move action or hovers movement keys. Hidden during enemy turns and non-movement actions.

- **Reachable tiles:** semi-transparent blue
- **Threatened tiles** (enemy adjacency): semi-transparent red
- **Selected path:** brighter blue with line trace
- **Current tile:** white highlight
- **Unwalkable cells** (walls, obstacles): no overlay rendered

Generated as a flat mesh from `BattleGrid.Walkable[]`. When `BattleVariation` places obstacles, it updates walkability and regenerates affected overlay cells.

## Character Models

### Source Assets

- **Heroes (18 models):** `C:\Pictures\Assets\NPCs\` — Dragonborn, Dwarf, Elf, Githyanki, Human (fighters, wizards, clerics, rangers)
- **Monsters (36 models):** `C:\Pictures\Assets\NPCs\Monsters\` — Goblins (8 variants incl. mounted), Skeletons, Zombies, Bandits, Lizardfolk, Kobolds, Knolls, Orcs, Mummy, Mephitis, Stirges, Giant Rat

### Import Pipeline

GLB files imported into `Assets/Models/NPCs/` and `Assets/Models/Monsters/`. Unity auto-generates prefabs from GLB import.

### Mapping

- `EnemyDef` gets a new `string ModelId` field mapping to a model prefab name
- `PlayerData` gets a `string ModelId` field (hardcoded initially, character creation later)
- Lookup: `Resources.Load<GameObject>($"Models/{modelId}")` or addressable reference
- **Fallback:** unmapped enemies render as colored capsule (same as current overworld player)

### Rendering

- Static poses for now (no skeletal animation)
- Placed on grid cell center, rotated to face current target
- Scale pulse (1.0 to 1.15x) on active combatant's turn
- Death: rigid body tilt (90 degree rotation over 0.5s) + alpha fade to 0

## Battle UI

### Design Direction

Follows the layout from the 4K concept art (`C:\Pictures\Assets\UI Elements\4k resilution UI\`): dark ornate fantasy frames, parchment backgrounds. First implementation uses functional UI Toolkit placeholders matching the layout structure; polished art pass comes later.

### No Enemy Health Exposure

Enemies do NOT show HP bars, health numbers, or percentage. Players should not know enemy health. This is a core design decision for multiplayer readiness.

### Player HUD (bottom-left)

- Player portrait frame (matches concept art square frame)
- HP bar (green to red lerp)
- Condition icons (poison, stun, paralysis, fear)
- Action/movement remaining indicators

### Party Panel (left side, future multiplayer)

- Stack of character boxes per party member
- Each box: name, HP bar, condition icons
- Clicking a party member centers camera on them
- Single-player: shows only the player's box

### Action Bar (bottom-center)

- Matches concept art bottom bar layout
- Buttons: Move, Attack, Spell, Potion, End Turn
- Each shows keyboard shortcut label
- Clickable with mouse

### Enemy Inspect (on hover/click)

- Small tooltip: name + visible conditions only
- No HP, no damage taken total, no percentage
- Conditions shown = things you'd visually see (poisoned aura, stunned stars, etc.)

### Damage Numbers

- Float up from hit position
- Color tiers: white (small), orange (medium), red (big), yellow (crit)
- Fade out over ~1 second

## Battle Flow

1. `GameManager.EnterBattle(encounterId)` — loads battle scene
2. `BattleManager.Start()`:
   a. `EncounterData.Get(encounterId)` — get enemy definitions
   b. Pick `BattleSceneTemplate` from biome pool (or boss-specific template if `IsBossArena` matches encounter)
   c. `BattleRenderer3D.Initialize(template)`:
      - Instantiate room prefab
      - Apply `BattleVariation` (props, lighting, rotation) — skip for boss arenas
      - Generate `BattleGrid` walkability from template + variation
      - Project grid overlay (hidden initially)
      - Spawn player model at `PlayerSpawnZone`
      - Spawn enemy models at `EnemySpawnZone` (bosses at `BossSpawnPoints`)
   d. Roll initiative, order combatants
   e. `StartTurn()` — begin combat loop
3. Combat loop: unchanged turn logic from `BattleManager`
4. Victory/loss: cleanup room, return to overworld

## Biome Template Distribution

| Biome | Regular Templates | Boss Templates | Asset Pack Source |
|-------|------------------|----------------|-------------------|
| Forest | 8-10 clearing rooms | 2-3 elaborate glades | Forest Environment Dynamic Nature |
| Dungeon | 8-10 stone rooms | 2-3 throne/ritual rooms | Multistory Dungeons 2 |
| Castle | 6-8 hall/corridor rooms | 2-3 grand halls | Lordenfel |
| Temple | 4-6 chamber rooms | 1-2 sanctum rooms | Eternal Temple |

Total: ~30-40 regular + ~8-10 boss = ~40-50 templates.

## File Structure

```
Assets/Scripts/Demo/Battle/
  BattleManager.cs          (existing, minor changes)
  BattleGrid.cs             (existing, unchanged)
  BattleCombatant.cs        (existing, add ModelId field)
  BattleRenderer.cs         (existing, kept for 2D fallback)
  BattleRenderer3D.cs       (NEW)
  BattleSceneTemplate.cs    (NEW, ScriptableObject)
  BattleVariation.cs        (NEW)
  BattleInputController.cs  (NEW)
  BattleGridOverlay.cs      (NEW)
  BattleUI.cs               (NEW, UI Toolkit)

Assets/Models/
  NPCs/                     (imported hero GLBs)
  Monsters/                 (imported monster GLBs)

Assets/ScriptableObjects/BattleTemplates/
  Forest/                   (forest biome templates)
  Dungeon/                  (dungeon biome templates)
  Castle/                   (castle biome templates)
  Temple/                   (temple biome templates)
```

## Out of Scope (Future Work)

- Skeletal animation (idle, walk, attack) — static poses for now
- Polished 4K UI art integration — functional placeholders first
- Multiplayer networking — architecture supports it (no enemy HP exposure, party panel)
- Dungeon interior exploration (Phase 5 — separate spec)
- Spell VFX / particle effects
- Cover system / destructible props
