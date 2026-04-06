# Combat Completeness Design: Creature Stats + Equipment Integration

**Date:** 2026-04-05
**Status:** Approved
**Scope:** Add per-creature D&D 5e stat blocks and expand equipment loading

## Problem

Enemies use generic scaled defaults (HP=level*4+4, all abilities 10) making every encounter feel identical. RPGBridge hardcodes 8 equipment items despite 203 generated assets existing in Resources.

## Solution

1. Add a `CreatureDatabase` with authentic D&D 5e stat blocks for all 30 creature variants
2. Wire MapSerializer to use real stats instead of scaled defaults
3. Expand RPGBridge to load any equipment by ID from the full asset pool

## Components

### 1. New: CreatureDatabase.cs

**Location:** `Assets/Scripts/Generation/Data/CreatureDatabase.cs`
**Namespace:** `ForeverEngine.Generation.Data`

Static class following the `GameTables`/`MapProfile` pattern.

**Data structure:**
```csharp
public struct CreatureStatBlock
{
    public int HP, AC;
    public int STR, DEX, CON, INT, WIS, CHA;
    public int Speed;
    public string AtkDice;
    public DamageType AttackDamageType;
    public DamageType Resistances;
    public DamageType Vulnerabilities;
    public DamageType Immunities;
    public int CR;     // Challenge rating (fractional: 25=1/4, 50=1/2, 100=1, etc.)
    public int XP;
    public string AiBehavior; // Default AI behavior for this creature
}
```

**Public API:**
```csharp
public static CreatureStatBlock GetStats(string variant)
```

Returns the stat block for the given variant string. Falls back to scaled defaults (HP = 8 + CR/25, AC = 11, all abilities 10, speed 6, "1d4") for unknown variants.

**All 30 creature variants with D&D 5e stats:**

| Variant | HP | AC | STR | DEX | CON | INT | WIS | CHA | Spd | AtkDice | DmgType | CR | XP | Behavior |
|---------|----|----|-----|-----|-----|-----|-----|-----|-----|---------|---------|----|----|----------|
| goblin | 7 | 15 | 8 | 14 | 10 | 10 | 8 | 8 | 6 | 1d6+2 | Slashing | 25 | 50 | chase |
| skeleton | 13 | 13 | 10 | 14 | 15 | 6 | 8 | 5 | 6 | 1d6+2 | Piercing | 25 | 50 | chase |
| zombie | 22 | 8 | 13 | 6 | 16 | 3 | 6 | 5 | 4 | 1d6+1 | Bludgeoning | 25 | 50 | chase |
| rat | 1 | 10 | 2 | 11 | 9 | 2 | 10 | 4 | 6 | 1d1 | Piercing | 0 | 10 | flee |
| spider | 1 | 12 | 2 | 14 | 8 | 1 | 10 | 2 | 6 | 1d1 | Piercing | 0 | 10 | chase |
| bat | 1 | 12 | 2 | 15 | 8 | 2 | 12 | 4 | 6 | 1d1 | Piercing | 0 | 10 | flee |
| beetle | 4 | 14 | 8 | 10 | 13 | 1 | 7 | 3 | 4 | 1d4+1 | Piercing | 12 | 25 | chase |
| slime | 22 | 8 | 12 | 8 | 16 | 1 | 6 | 2 | 4 | 1d6+1 | Acid | 50 | 100 | chase |
| guard | 11 | 16 | 13 | 12 | 12 | 10 | 11 | 10 | 6 | 1d6+1 | Piercing | 12 | 25 | guard |
| knight | 52 | 18 | 16 | 11 | 14 | 11 | 11 | 15 | 6 | 2d6+3 | Slashing | 300 | 700 | guard |
| mage | 40 | 12 | 9 | 14 | 11 | 17 | 12 | 11 | 6 | 1d4 | Bludgeoning | 600 | 2300 | chase |
| servant | 4 | 10 | 10 | 10 | 10 | 10 | 10 | 10 | 6 | 1d2 | Bludgeoning | 0 | 10 | flee |
| wraith | 67 | 13 | 6 | 16 | 16 | 12 | 14 | 15 | 6 | 3d6+3 | Necrotic | 500 | 1800 | chase |
| vampire_spawn | 82 | 15 | 16 | 16 | 16 | 11 | 10 | 12 | 6 | 2d6+3 | Necrotic | 500 | 1800 | chase |
| crocodile | 19 | 12 | 15 | 10 | 13 | 2 | 10 | 5 | 4 | 1d10+2 | Piercing | 50 | 100 | chase |
| thief | 16 | 14 | 10 | 16 | 12 | 12 | 10 | 14 | 6 | 1d6+3 | Piercing | 50 | 100 | chase |
| cultist | 9 | 12 | 11 | 12 | 10 | 10 | 11 | 10 | 6 | 1d6+1 | Slashing | 12 | 25 | chase |
| golem | 93 | 17 | 19 | 9 | 18 | 3 | 11 | 1 | 5 | 2d8+5 | Bludgeoning | 500 | 1800 | guard |
| elemental | 90 | 15 | 18 | 14 | 18 | 6 | 10 | 6 | 6 | 2d8+4 | Fire | 500 | 1800 | chase |
| priest | 27 | 13 | 10 | 10 | 12 | 13 | 16 | 13 | 6 | 1d6 | Bludgeoning | 200 | 450 | guard |
| kobold | 5 | 12 | 7 | 15 | 9 | 8 | 7 | 8 | 6 | 1d4+2 | Piercing | 12 | 25 | flee |
| rust_monster | 27 | 14 | 13 | 12 | 13 | 2 | 13 | 6 | 8 | 1d6+1 | Piercing | 50 | 100 | chase |
| earth_elemental | 126 | 17 | 20 | 8 | 20 | 5 | 10 | 5 | 5 | 2d8+5 | Bludgeoning | 500 | 1800 | chase |
| wolf | 11 | 13 | 12 | 15 | 12 | 3 | 12 | 6 | 8 | 2d4+2 | Piercing | 25 | 50 | chase |
| bear | 34 | 11 | 19 | 10 | 16 | 2 | 13 | 7 | 8 | 2d6+4 | Slashing | 100 | 200 | chase |
| treant | 138 | 16 | 23 | 8 | 21 | 12 | 16 | 12 | 6 | 3d6+6 | Bludgeoning | 900 | 5000 | guard |
| fairy | 1 | 15 | 3 | 18 | 8 | 14 | 12 | 16 | 6 | 1d1 | Radiant | 12 | 25 | flee |
| bandit | 11 | 12 | 11 | 12 | 12 | 10 | 10 | 10 | 6 | 1d6+1 | Slashing | 12 | 25 | chase |
| villager | 4 | 10 | 10 | 10 | 10 | 10 | 10 | 10 | 6 | 1d2 | Bludgeoning | 0 | 10 | flee |
| merchant | 9 | 11 | 10 | 12 | 10 | 13 | 14 | 14 | 6 | 1d4 | Bludgeoning | 0 | 10 | flee |
| imp | 10 | 13 | 6 | 17 | 13 | 11 | 12 | 14 | 4 | 1d4+3 | Piercing | 100 | 200 | chase |
| animated_armor | 33 | 18 | 14 | 11 | 13 | 1 | 3 | 1 | 5 | 2d6+2 | Bludgeoning | 100 | 200 | guard |

**Resistances/Vulnerabilities/Immunities (notable entries):**
- skeleton: vulnerable to Bludgeoning, immune to Poison
- zombie: immune to Poison
- slime: resistant to Acid, immune to Poison/Lightning
- wraith: resistant to Acid/Cold/Fire/Lightning, immune to Necrotic/Poison
- vampire_spawn: resistant to Necrotic, immune to Poison
- golem: immune to Poison/Psychic
- elemental: immune to Fire/Poison
- earth_elemental: resistant to Piercing/Slashing, immune to Poison
- treant: vulnerable to Fire, resistant to Bludgeoning/Piercing
- imp: resistant to Cold, immune to Fire/Poison
- animated_armor: immune to Poison/Psychic

### 2. Modify: MapSerializer.cs BuildSpawns()

Replace the scaled defaults block with:
```csharp
var creatureStats = CreatureDatabase.GetStats(enc.Variant);
spawns.Add(new SSpawn
{
    name = enc.Variant ?? "creature",
    x = enc.X, y = enc.Y, z = 0,
    token_type = "enemy",
    ai_behavior = creatureStats.AiBehavior,
    stats = new SStats
    {
        hp = creatureStats.HP,
        ac = creatureStats.AC,
        strength = creatureStats.STR,
        dexterity = creatureStats.DEX,
        constitution = creatureStats.CON,
        intelligence = creatureStats.INT,
        wisdom = creatureStats.WIS,
        charisma = creatureStats.CHA,
        speed = creatureStats.Speed,
        atk_dice = creatureStats.AtkDice
    }
});
```

### 3. Modify: RPGBridge.cs Equipment Loading

Replace the 4 hardcoded weapon fields and 4 armor fields with cache-based loaders:

```csharp
private static Dictionary<string, WeaponData> _weaponCache = new();
private static Dictionary<string, ArmorData> _armorCache = new();

public static WeaponData GetWeapon(string id)
{
    if (!_weaponCache.TryGetValue(id, out var w))
        _weaponCache[id] = w = Resources.Load<WeaponData>($"RPG/Content/Weapons/{id}");
    return w;
}

public static ArmorData GetArmor(string id)
{
    if (!_armorCache.TryGetValue(id, out var a))
        _armorCache[id] = a = Resources.Load<ArmorData>($"RPG/Content/Armor/{id}");
    return a;
}
```

Premade character factory methods change from field references to cache calls:
- `_weapons["longsword"]` becomes `GetWeapon("longsword")`
- `_armor["chain_mail"]` becomes `GetArmor("chain_mail")`

Remove the `EnsureLoaded()` method, the `_weapons`/`_armor` dictionaries, and the hardcoded `_classes`/`_species` arrays (replace with same cache pattern if used).

## Files Changed

| File | Action |
|---|---|
| `Assets/Scripts/Generation/Data/CreatureDatabase.cs` | **New** — 30 D&D 5e creature stat blocks |
| `Assets/Scripts/Generation/MapSerializer.cs` | **Modify** — use CreatureDatabase in BuildSpawns |
| `Assets/Scripts/Demo/RPGBridge.cs` | **Modify** — cache-based equipment loading |

## Files Unchanged

| File | Reason |
|---|---|
| `Assets/Scripts/MonoBehaviour/Bootstrap/MapImporter.cs` | Already reads all stat fields from JSON |
| `Assets/Scripts/Demo/Battle/BattleCombatant.cs` | Already maps stats to combat |
| `Assets/Scripts/Generation/Agents/PopulationGenerator.cs` | Already selects variants from pool |
| `Assets/Resources/RPG/Content/Weapons/*.asset` | Already generated |
| `Assets/Resources/RPG/Content/Armor/*.asset` | Already generated |
