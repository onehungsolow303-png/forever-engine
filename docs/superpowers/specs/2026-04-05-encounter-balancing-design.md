# Encounter Balancing Design

**Date:** 2026-04-05
**Status:** Approved
**Scope:** CR-gated creature selection in PopulationGenerator

## Problem

PopulationGenerator picks creatures randomly from MapProfile.CreaturePool regardless of party level. A level 1 party can face wraiths (CR 500, HP 67) or treants (CR 900, HP 138).

## Solution

Filter CreaturePool by CR threshold based on party level before selecting. Uses CreatureDatabase.GetStats() to look up each creature's CR.

## CR Thresholds by Party Level

| Party Level | Max CR | Example Creatures |
|---|---|---|
| 1-2 | 25 | rat, spider, bat, goblin, skeleton, kobold, beetle, guard, cultist, bandit, fairy, villager, merchant, servant |
| 3-4 | 100 | above + zombie, slime, wolf, crocodile, thief, rust_monster, imp, animated_armor, bear |
| 5-8 | 500 | above + priest, knight, wraith, vampire_spawn, golem, elemental, earth_elemental |
| 9+ | unlimited | above + treant, mage |

Formula: `maxCR = partyLevel <= 2 ? 25 : partyLevel <= 4 ? 100 : partyLevel <= 8 ? 500 : int.MaxValue`

## Changes

**Modify:** `Assets/Scripts/Generation/Agents/PopulationGenerator.cs`

In the encounter selection block (line 64-65), filter `profile.CreaturePool` to only creatures with `CreatureDatabase.GetStats(variant).CR <= maxCR`. If no creatures pass the filter, fall back to the full pool.

```csharp
// Before (current):
string creature = profile.CreaturePool[rng.Next(profile.CreaturePool.Length)];

// After:
int maxCR = request.PartyLevel <= 2 ? 25 : request.PartyLevel <= 4 ? 100 : request.PartyLevel <= 8 ? 500 : int.MaxValue;
var eligible = System.Array.FindAll(profile.CreaturePool, c => CreatureDatabase.GetStats(c).CR <= maxCR);
if (eligible.Length == 0) eligible = profile.CreaturePool;
string creature = eligible[rng.Next(eligible.Length)];
```

## Files Changed

| File | Action |
|---|---|
| `Assets/Scripts/Generation/Agents/PopulationGenerator.cs` | **Modify** — add CR filter |
