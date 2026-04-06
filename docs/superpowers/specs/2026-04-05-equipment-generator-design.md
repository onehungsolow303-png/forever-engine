# Equipment Generator Design Spec

## Overview
Single `EquipmentGenerator.cs` that generates all weapon and armor ScriptableObject assets for the Forever Engine RPG system. Follows the established generator pattern (SpellGenerator, ClassGenerator, SpeciesGenerator). Produces ~189 assets covering the full D&D 5e SRD equipment list plus magic variants and named magic items.

## Output Paths
- Weapons: `Assets/Resources/RPG/Content/Weapons/{weapon_id}.asset`
- Armor: `Assets/Resources/RPG/Content/Armor/{armor_id}.asset`

## Weapon Assets (~130 total)

### Base Weapons (27)

**Simple Melee (6):**
| ID | Name | Damage | Type | Properties |
|---|---|---|---|---|
| club | Club | 1d4 | Bludgeoning | Light |
| dagger | Dagger | 1d4 | Piercing | Finesse, Light, Thrown |
| greatclub | Greatclub | 1d8 | Bludgeoning | TwoHanded |
| handaxe | Handaxe | 1d6 | Slashing | Light, Thrown |
| javelin | Javelin | 1d6 | Piercing | Thrown |
| mace | Mace | 1d6 | Bludgeoning | — |

**Simple Ranged (4):**
| ID | Name | Damage | Type | Range | Properties |
|---|---|---|---|---|---|
| light_crossbow | Light Crossbow | 1d8 | Piercing | 80/320 | Ammunition, Loading, TwoHanded |
| dart | Dart | 1d4 | Piercing | 20/60 | Finesse, Thrown |
| shortbow | Shortbow | 1d6 | Piercing | 80/320 | Ammunition, TwoHanded |
| sling | Sling | 1d4 | Bludgeoning | 30/120 | Ammunition |

**Martial Melee (12):**
| ID | Name | Damage | Type | Properties |
|---|---|---|---|---|
| battleaxe | Battleaxe | 1d8 (V:1d10) | Slashing | Versatile |
| flail | Flail | 1d8 | Bludgeoning | — |
| glaive | Glaive | 1d10 | Slashing | Heavy, Reach, TwoHanded |
| greataxe | Greataxe | 1d12 | Slashing | Heavy, TwoHanded |
| greatsword | Greatsword | 2d6 | Slashing | Heavy, TwoHanded |
| halberd | Halberd | 1d10 | Slashing | Heavy, Reach, TwoHanded |
| lance | Lance | 1d12 | Piercing | Reach |
| longsword | Longsword | 1d8 (V:1d10) | Slashing | Versatile |
| morningstar | Morningstar | 1d8 | Piercing | — |
| rapier | Rapier | 1d8 | Piercing | Finesse |
| scimitar | Scimitar | 1d6 | Slashing | Finesse, Light |
| warhammer | Warhammer | 1d8 (V:1d10) | Bludgeoning | Versatile |

**Martial Ranged (5):**
| ID | Name | Damage | Type | Range | Properties |
|---|---|---|---|---|---|
| hand_crossbow | Hand Crossbow | 1d6 | Piercing | 30/120 | Ammunition, Light, Loading |
| heavy_crossbow | Heavy Crossbow | 1d10 | Piercing | 100/400 | Ammunition, Heavy, Loading, TwoHanded |
| longbow | Longbow | 1d8 | Piercing | 150/600 | Ammunition, Heavy, TwoHanded |
| blowgun | Blowgun | 1 | Piercing | 25/100 | Ammunition, Loading |
| net | Net | 0 | Bludgeoning | 5/15 | Thrown |

### Magic Variants (+1/+2/+3) (~81)
Every base weapon gets three magic variants:
- `{id}_plus_1` — Uncommon, MagicBonus=1
- `{id}_plus_2` — Rare, MagicBonus=2
- `{id}_plus_3` — VeryRare, MagicBonus=3

### Named Magic Weapons (~22)
| ID | Name | Base | Rarity | Special |
|---|---|---|---|---|
| flame_tongue_longsword | Flame Tongue Longsword | Longsword | Rare | +2d6 Fire bonus damage |
| flame_tongue_greatsword | Flame Tongue Greatsword | Greatsword | Rare | +2d6 Fire bonus damage |
| frost_brand_longsword | Frost Brand Longsword | Longsword | VeryRare | +1d6 Cold bonus damage, MagicBonus=3 |
| frost_brand_greatsword | Frost Brand Greatsword | Greatsword | VeryRare | +1d6 Cold bonus damage, MagicBonus=3 |
| vorpal_sword | Vorpal Sword | Greatsword | Legendary | MagicBonus=3 |
| dragon_slayer_longsword | Dragon Slayer Longsword | Longsword | Rare | MagicBonus=1 |
| sun_blade | Sun Blade | Longsword | Rare | Radiant damage, MagicBonus=2 |
| vicious_rapier | Vicious Rapier | Rapier | Rare | +2d6 bonus on nat 20 |
| javelin_of_lightning | Javelin of Lightning | Javelin | Uncommon | Lightning damage, MagicBonus=1 |
| dagger_of_venom | Dagger of Venom | Dagger | Rare | Poison damage, MagicBonus=1 |
| mace_of_disruption | Mace of Disruption | Mace | Rare | Radiant damage, MagicBonus=2 |
| mace_of_smiting | Mace of Smiting | Mace | Rare | MagicBonus=1 |
| mace_of_terror | Mace of Terror | Mace | Rare | MagicBonus=2 |
| holy_avenger | Holy Avenger | Longsword | Legendary | Radiant damage, MagicBonus=3 |
| nine_lives_stealer | Nine Lives Stealer | Longsword | VeryRare | Necrotic damage, MagicBonus=2 |
| dancing_sword | Dancing Sword | Longsword | VeryRare | MagicBonus=1 |
| defender_longsword | Defender Longsword | Longsword | Legendary | MagicBonus=3 |
| oathbow | Oathbow | Longbow | VeryRare | MagicBonus=3 |
| berserker_greataxe | Berserker Greataxe | Greataxe | Rare | MagicBonus=1 |
| giant_slayer_greataxe | Giant Slayer Greataxe | Greataxe | Rare | MagicBonus=1 |
| luck_blade | Luck Blade | Longsword | Legendary | MagicBonus=1 |
| scimitar_of_speed | Scimitar of Speed | Scimitar | VeryRare | MagicBonus=2 |

## Armor Assets (~59 total)

### Base Armor (13)

**Light Armor (3):**
| ID | Name | Base AC | Stealth Disadv. | Str Req |
|---|---|---|---|---|
| padded | Padded | 11 | Yes | 0 |
| leather | Leather | 11 | No | 0 |
| studded_leather | Studded Leather | 12 | No | 0 |

**Medium Armor (4):**
| ID | Name | Base AC | Stealth Disadv. | Str Req |
|---|---|---|---|---|
| hide | Hide | 12 | No | 0 |
| chain_shirt | Chain Shirt | 13 | No | 0 |
| scale_mail | Scale Mail | 14 | Yes | 0 |
| breastplate | Breastplate | 14 | No | 0 |

**Heavy Armor (4):**
| ID | Name | Base AC | Stealth Disadv. | Str Req |
|---|---|---|---|---|
| ring_mail | Ring Mail | 14 | Yes | 0 |
| chain_mail | Chain Mail | 16 | Yes | 13 |
| splint | Splint | 17 | Yes | 15 |
| plate | Plate | 18 | Yes | 15 |

**Shield (1):**
| ID | Name | Base AC | Stealth Disadv. | Str Req |
|---|---|---|---|---|
| shield | Shield | 2 | No | 0 |

Half Plate is excluded as it's not in the base SRD.

### Magic Variants (+1/+2/+3) (~36 + 3 shields = 39)
Every base armor and shield gets three magic variants:
- `{id}_plus_1` — Uncommon, MagicBonus=1
- `{id}_plus_2` — Rare, MagicBonus=2
- `{id}_plus_3` — VeryRare, MagicBonus=3

### Named Magic Armor (~11)
| ID | Name | Base | Rarity | Special |
|---|---|---|---|---|
| mithral_chain_shirt | Mithral Chain Shirt | Chain Shirt | Uncommon | No stealth disadvantage (already none) |
| mithral_chain_mail | Mithral Chain Mail | Chain Mail | Uncommon | No stealth disadvantage, no STR req |
| mithral_breastplate | Mithral Breastplate | Breastplate | Uncommon | No stealth disadvantage (already none) |
| mithral_splint | Mithral Splint | Splint | Uncommon | No stealth disadvantage, no STR req |
| mithral_plate | Mithral Plate | Plate | Uncommon | No stealth disadvantage, no STR req |
| adamantine_chain_mail | Adamantine Chain Mail | Chain Mail | Uncommon | MagicBonus=1 |
| adamantine_splint | Adamantine Splint | Splint | Uncommon | MagicBonus=1 |
| adamantine_plate | Adamantine Plate | Plate | Uncommon | MagicBonus=1 |
| dragon_scale_mail | Dragon Scale Mail | Scale Mail | VeryRare | MagicBonus=2 |
| demon_armor | Demon Armor | Plate | VeryRare | MagicBonus=1 |
| animated_shield | Animated Shield | Shield | VeryRare | MagicBonus=2 |

## Factory Helper Methods

```
// Weapons
Melee(id, name, diceCount, die, dmgType, props, group) -> WeaponData
Ranged(id, name, diceCount, die, dmgType, normalRange, longRange, props, group) -> WeaponData
Versatile(id, name, diceCount, die, versDiceCount, versDie, dmgType, props, group) -> WeaponData
MagicWeapon(baseId, baseName, baseDiceCount, baseDie, baseDmgType, baseProps, baseGroup, bonus) -> WeaponData
NamedWeapon(id, name, diceCount, die, dmgType, props, group, magicBonus, rarity, normalRange, longRange, versDiceCount, versDie) -> WeaponData

// Armor
MakeArmor(id, name, baseAC, type, stealthDisadv, strReq) -> ArmorData
MagicArmor(baseId, baseName, baseAC, baseType, baseStealth, baseStr, bonus) -> ArmorData
NamedArmor(id, name, baseAC, type, stealthDisadv, strReq, magicBonus, rarity) -> ArmorData
```

## Integration Points

### ContentGenerator.cs
- Add `EquipmentGenerator.GenerateAll()` call after SpeciesGenerator
- Add `EnsureFolder` calls for Weapons and Armor directories
- Add validation: expect ~130 weapons and ~59 armor assets

### RPGBridge.cs
- Update `EnsureLoaded()` to load starting equipment from `RPG/Content/Weapons/` and `RPG/Content/Armor/`
- Remove TODO comments for weapon/armor loading
- Load class-appropriate defaults (e.g., Warrior gets longsword + chain_mail)

## What's NOT Included
- No loot table system (separate feature)
- No inventory management UI
- No equipment weight/encumbrance
- No attunement mechanics
- No ammunition tracking
- No special ability text (data model stores stats only)
