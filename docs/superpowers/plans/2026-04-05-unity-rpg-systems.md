# Unity RPG Systems — Implementation Plan (Plan 4)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development

**Goal:** Enhance the Forever Engine's ECS systems with full Infinity RPG combat resolution, spellcasting, conditions, experience/leveling, and rest mechanics. Also add character creation data, RPG databases, and content loader bridge.

**Architecture:** New ECS components and systems following the existing DOTS pattern. New MonoBehaviour scripts for UI-facing RPG features. ScriptableObject databases for spells, monsters, and items.

**Tech Stack:** Unity 6, C# DOTS (ISystem, IComponentData), MonoBehaviour for UI

**Project:** `C:\Dev\Forever engin\`

---

### Task 1: RPG Data Classes (C# ScriptableObjects)

Create `Assets/Scripts/Data/`:
- `SpellDatabase.cs` — ScriptableObject holding spell definitions as serializable structs
- `MonsterDatabase.cs` — ScriptableObject holding monster stat blocks
- `ItemDatabase.cs` — ScriptableObject holding weapon/armor/item data
- `InfinityRPGData.cs` — Static tables: proficiency by level, XP thresholds, CR/XP table

---

### Task 2: Enhanced ECS Components

Create/enhance in `Assets/Scripts/ECS/Components/`:
- `CharacterSheetComponent.cs` — IComponentData: class enum, level, XP, spell slots, proficiencies
- `SpellbookComponent.cs` — IBufferElementData: known/prepared spells, concentration tracking
- `ConditionComponent.cs` — IBufferElementData: active conditions with duration/source
- `EncounterComponent.cs` — IComponentData: trigger zone, monster group ref, activated flag
- `TrapComponent.cs` — IComponentData: DCs, damage, triggered flag
- `NPCPersonalityComponent.cs` — IComponentData: personality seed hash, disposition, faction
- `LootTableComponent.cs` — IComponentData: reference to generated loot data

---

### Task 3: Enhanced Combat System

Modify `Assets/Scripts/ECS/Systems/CombatSystem.cs`:
- Full advantage/disadvantage resolution
- Critical hit handling (double damage dice)
- Natural 1 auto-miss
- Damage type tracking (for resistance/vulnerability)
- Death saving throws at 0 HP

---

### Task 4: New ECS Systems

Create in `Assets/Scripts/ECS/Systems/`:
- `SpellSystem.cs` — Spell casting resolution, slot tracking, concentration checks
- `ConditionSystem.cs` — Apply/remove conditions, tick durations each turn
- `ExperienceSystem.cs` — XP awards on encounter completion, level-up detection
- `RestSystem.cs` — Short rest (hit dice healing) and long rest (full recovery)
- `EncounterTriggerSystem.cs` — Detect player entering encounter zones
- `TrapDetectionSystem.cs` — Passive perception vs trap DC on movement

---

### Task 5: Character Creation MonoBehaviours

Create `Assets/Scripts/MonoBehaviour/CharacterCreation/`:
- `CharacterCreator.cs` — Species/class/background selection flow
- `AbilityScoreAssigner.cs` — Point buy / standard array UI controller
- `CharacterSheetDisplay.cs` — Character sheet view

---

### Task 6: Content Loader Bridge

Create `Assets/Scripts/MonoBehaviour/ContentLoader/`:
- `AssetGeneratorBridge.cs` — Calls Python Asset Generator via Process
- `ContentRequestQueue.cs` — Priority queue for generation requests
- `HotContentLoader.cs` — Load new MapData.json + assets without scene restart

---

### Task 7: RPG UI MonoBehaviours

Create `Assets/Scripts/MonoBehaviour/RPG/`:
- `LevelUpManager.cs` — Level-up flow, ability score improvements, feat selection
- `SpellcastingUI.cs` — Spell selection and targeting UI
- `LootPickupManager.cs` — Chest interaction, loot display, inventory add
- `RestManager.cs` — Rest initiation, hit dice spending UI
