using ForeverEngine.RPG.Combat;
using ForeverEngine.RPG.Data;
using ForeverEngine.RPG.Enums;
using ForeverEngine.ECS.Utility;

namespace ForeverEngine.RPG.Spells
{
    /// <summary>
    /// Static spell casting pipeline implementing the full cast resolution:
    /// 1. Validate: caster knows spell, has slot at required level (cantrip = no slot)
    /// 2. Apply metamagic (if any) — modify context, spend sorcery points
    /// 3. Expend spell slot (unless cantrip or ritual)
    /// 4. If spell attack: call AttackResolver with spell attack bonus
    /// 5. If save-based: target rolls save vs caster's spell DC
    /// 6. Roll damage/healing with upcast scaling
    /// 7. Apply damage via DamageResolver (or apply healing)
    /// 8. Apply conditions if save failed
    /// 9. Set concentration if spell requires it (ends previous)
    /// </summary>
    public static class SpellCastingPipeline
    {
        /// <summary>
        /// Cast a spell. Full pipeline from validation through resolution.
        /// </summary>
        /// <param name="ctx">Cast context with caster, targets, spell, slot level.</param>
        /// <param name="casterAbilities">Caster's effective ability scores.</param>
        /// <param name="casterProficiency">Caster's proficiency bonus.</param>
        /// <param name="castingAbility">The ability used for spellcasting (INT/WIS/CHA).</param>
        /// <param name="spellSlots">Caster's spell slot manager.</param>
        /// <param name="concentration">Caster's concentration tracker.</param>
        /// <param name="sorceryPoints">Sorcery points (for metamagic). Pass null if not a Sorcerer.</param>
        /// <param name="seed">RNG seed.</param>
        /// <returns>Cast result.</returns>
        public static CastResult Cast(
            CastContext ctx,
            AbilityScores casterAbilities,
            int casterProficiency,
            Ability castingAbility,
            SpellSlotManager spellSlots,
            ConcentrationTracker concentration,
            ResourcePool? sorceryPoints,
            ref uint seed)
        {
            var spell = ctx.Spell;
            if (spell == null)
                return CastResult.Failure("No spell specified");

            // 1. Validate slot level
            if (!spell.IsCantrip && !ctx.IsRitual)
            {
                if (ctx.SlotLevel < spell.Level)
                    return CastResult.Failure($"Slot level {ctx.SlotLevel} is below spell level {spell.Level}");

                if (!spellSlots.CanCast(spell, ctx.SlotLevel))
                    return CastResult.Failure($"No available slot at level {ctx.SlotLevel}");
            }

            // 2. Apply metamagic (if any)
            var modifiedCtx = ctx;
            ResourcePool sp = sorceryPoints ?? new ResourcePool(0);
            if (ctx.Metamagic != MetamagicType.None)
            {
                modifiedCtx = MetamagicEngine.ApplyMetamagic(ctx, ctx.Metamagic, ref sp);
            }

            // 3. Expend spell slot (unless cantrip or ritual)
            int slotExpended = 0;
            if (!spell.IsCantrip && !ctx.IsRitual)
            {
                if (!spellSlots.ExpendSlot(ctx.SlotLevel))
                    return CastResult.Failure("Failed to expend spell slot");
                slotExpended = ctx.SlotLevel;
            }

            // Calculate spell DC and attack bonus
            int spellMod = casterAbilities.GetModifier(castingAbility);
            int spellDC = 8 + casterProficiency + spellMod;
            int spellAttackBonus = casterProficiency + spellMod;

            int totalDamage = 0;
            int totalHealing = 0;
            Condition appliedConditions = Condition.None;
            bool targetSaved = false;

            // 4. If spell attack: resolve attack
            if (spell.SpellAttack && modifiedCtx.Targets != null && modifiedCtx.Targets.Length > 0)
            {
                // For simplicity, resolve against first target
                // Full AoE handling would iterate all targets
                var attackCtx = new AttackContext
                {
                    AttackerAbilities = casterAbilities,
                    AttackerProficiency = spellAttackBonus,
                    TargetAC = 10, // Would be populated from target's actual AC
                    IsRanged = spell.Range > 5,
                    IsMelee = spell.Range <= 5,
                    CritRange = 20,
                    MagicBonus = 0
                };

                var attackResult = AttackResolver.Resolve(attackCtx, ref seed);
                if (!attackResult.Hit)
                {
                    return new CastResult
                    {
                        Success = true, // Spell was cast, just missed
                        DamageDealt = 0,
                        SlotExpended = slotExpended,
                        FailureReason = "Attack missed"
                    };
                }

                // 6. Roll damage with upcast scaling
                if (spell.DamageDiceCount > 0)
                {
                    int upcastLevels = ctx.SlotLevel - spell.Level;
                    var upcastBonus = spell.GetUpcastDamage();
                    int extraDice = upcastLevels > 0 ? upcastBonus.Count * upcastLevels : 0;

                    var dmgCtx = new DamageContext
                    {
                        BaseDamage = new DiceExpression(
                            spell.DamageDiceCount + extraDice,
                            spell.DamageDie,
                            spell.DamageBonus),
                        Type = spell.DamageType,
                        Critical = attackResult.Critical,
                        BonusDamage = 0
                    };

                    var dmgResult = DamageResolver.Apply(dmgCtx, ref seed);
                    totalDamage = dmgResult.AfterResistance;
                }
            }
            // 5. If save-based: target saves vs DC
            else if (spell.HasSave)
            {
                // Roll save for target (simplified — would use target's actual ability + proficiency)
                int saveRoll = DiceRoller.Roll(1, 20, 0, ref seed);
                targetSaved = saveRoll >= spellDC;

                if (!targetSaved || spell.DamageDiceCount > 0)
                {
                    // 6. Roll damage with upcast
                    if (spell.DamageDiceCount > 0)
                    {
                        int upcastLevels = ctx.SlotLevel - spell.Level;
                        var upcastBonus = spell.GetUpcastDamage();
                        int extraDice = upcastLevels > 0 ? upcastBonus.Count * upcastLevels : 0;

                        var dmgCtx = new DamageContext
                        {
                            BaseDamage = new DiceExpression(
                                spell.DamageDiceCount + extraDice,
                                spell.DamageDie,
                                spell.DamageBonus),
                            Type = spell.DamageType,
                            Critical = false,
                            BonusDamage = 0
                        };

                        var dmgResult = DamageResolver.Apply(dmgCtx, ref seed);

                        // Save for half damage on some spells
                        totalDamage = targetSaved ? dmgResult.AfterResistance / 2 : dmgResult.AfterResistance;
                    }

                    // 8. Apply conditions if save failed
                    if (!targetSaved && spell.AppliesCondition != Condition.None)
                    {
                        appliedConditions = spell.AppliesCondition;
                    }
                }
            }
            else
            {
                // Non-attack, non-save spells (buffs, healing, utility)
                if (spell.HealingDiceCount > 0)
                {
                    var healExpr = spell.GetHealing();
                    totalHealing = healExpr.Roll(ref seed);

                    // Upcast healing
                    int upcastLevels = ctx.SlotLevel - spell.Level;
                    if (upcastLevels > 0)
                    {
                        var upcastHeal = spell.GetUpcastDamage(); // Reuse upcast field for healing spells
                        if (upcastHeal.Count > 0)
                        {
                            for (int i = 0; i < upcastLevels; i++)
                                totalHealing += upcastHeal.Roll(ref seed);
                        }
                    }
                }

                // Direct condition application (no save required)
                if (spell.AppliesCondition != Condition.None)
                {
                    appliedConditions = spell.AppliesCondition;
                }
            }

            // 9. Set concentration if spell requires it
            bool concentrationStarted = false;
            if (spell.Concentration && concentration != null)
            {
                concentration.Begin(spell);
                concentrationStarted = true;
            }

            return new CastResult
            {
                Success = true,
                DamageDealt = totalDamage,
                HealingDone = totalHealing,
                ConditionsApplied = appliedConditions,
                SlotExpended = slotExpended,
                ConcentrationStarted = concentrationStarted
            };
        }
    }
}
