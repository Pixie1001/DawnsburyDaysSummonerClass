using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using Dawnsbury;
using Dawnsbury.Audio;
using Dawnsbury.Auxiliary;
using Dawnsbury.Core;
using Dawnsbury.Core.Mechanics.Rules;
using Dawnsbury.Core.Animations;
using Dawnsbury.Core.CharacterBuilder;
using Dawnsbury.Core.CharacterBuilder.AbilityScores;
using Dawnsbury.Core.CharacterBuilder.Feats;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Common;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Spellbook;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.TrueFeatDb;
using Dawnsbury.Core.CharacterBuilder.Selections.Options;
using Dawnsbury.Core.CharacterBuilder.Spellcasting;
using Dawnsbury.Core.CombatActions;
using Dawnsbury.Core.Coroutines;
using Dawnsbury.Core.Coroutines.Options;
using Dawnsbury.Core.Coroutines.Requests;
using Dawnsbury.Core.Creatures;
using Dawnsbury.Core.Creatures.Parts;
using Dawnsbury.Core.Intelligence;
using Dawnsbury.Core.Mechanics;
using Dawnsbury.Core.Mechanics.Core;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Core.Mechanics.Targeting;
using Dawnsbury.Core.Mechanics.Targeting.TargetingRequirements;
using Dawnsbury.Core.Mechanics.Targeting.Targets;
using Dawnsbury.Core.Mechanics.Treasure;
using Dawnsbury.Core.Possibilities;
using Dawnsbury.Core.Roller;
using Dawnsbury.Core.StatBlocks;
using Dawnsbury.Core.StatBlocks.Description;
using Dawnsbury.Core.Tiles;
using Dawnsbury.Display;
using Dawnsbury.Display.Illustrations;
using Dawnsbury.Display.Text;
using Dawnsbury.IO;
using Dawnsbury.Modding;
using Dawnsbury.ThirdParty.SteamApi;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using static Dawnsbury.Mods.Classes.Summoner.SummonerClassLoader;

namespace Dawnsbury.Mods.Classes.Summoner {
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public static class SummonerSpells {

        public enum SummonerSpellId {
            EvolutionSurge,
            EidolonBoost,
            ReinforceEidolon,
            ExtendBoost,
            LifelinkSurge
        }

        public static Dictionary<SummonerSpellId, SpellId> LoadSpells() {
            Dictionary<SummonerSpellId, SpellId> spellList = new Dictionary<SummonerSpellId, SpellId>();


            spellList.Add(SummonerSpellId.EvolutionSurge, ModManager.RegisterNewSpell("EvolutionSurge", 1, (spellId, spellcaster, spellLevel, inCombat, spellInformation) => {
                return Spells.CreateModern(illEvolutionSurge, "Evolution Surge", new[] { tSummoner, Trait.Focus, Trait.Morph, Trait.Transmutation, Trait.Uncommon },
                        "You flood your eidolon with power, creating a temporary evolution in your eidolon's capabilities.",
                        "Your eidolon gains one the following adeptations for the rest of the encounter\n• Your eidolon gains a swim speed.\n• Your eidolom gains a +20-foot status bonus to its speed.",
                        Target.RangedFriend(20).WithAdditionalConditionOnTargetCreature((CreatureTargetingRequirement)new EidolonCreatureTargetingRequirement(qfSummonerBond)), spellLevel, null)
                    .WithSoundEffect(SfxName.Abjuration)
                    .WithVariants(new SpellVariant[2] {
                    new SpellVariant("Amphibious", "Amphibious Evolution Surge", (Illustration) IllustrationName.ElementWater),
                    new SpellVariant("Agility", "Agility Evolution Surge", (Illustration) IllustrationName.FleetStep)})
                    .WithCreateVariantDescription((Func<int, SpellVariant, string>)((_, variant) => {
                        string text = "Until the end of the encounter, your eidolon ";
                        if (variant.Id == "Amphibious") {
                            return text + "gains a swim speed.";
                        } else if (variant.Id == "Agility") {
                            return text + "gains a +20ft status bonus to its speed.";
                        }
                        return text;
                    }))
                    .WithEffectOnEachTarget((Delegates.EffectOnEachTarget)(async (spell, caster, target, result) => {
                        SpellVariant variant = spell.ChosenVariant;
                        if (variant.Id == "Amphibious") {
                            target.AddQEffect(QEffect.Swimming().WithExpirationNever());
                            target.AddQEffect(new QEffect(
                            variant.Name, "Your eidolon gains a swim speed.",
                            ExpirationCondition.Never, caster, variant.Illustration) {
                                CountsAsABuff = true
                            });
                        } else if (variant.Id == "Agility") {
                            target.AddQEffect(new QEffect(
                            variant.Name, "Your eidolon gains a +20ft status bonus to its speed.",
                            ExpirationCondition.Never, caster, variant.Illustration) {
                                Id = QEffectId.Swimming,
                                CountsAsABuff = true,
                                BonusToAllSpeeds = ((Func<QEffect, Bonus>)(_ => new Bonus(4, BonusType.Status, "Evolution Surge")))
                            });

                        }
                    }));
            }));

            spellList.Add(SummonerSpellId.EidolonBoost, ModManager.RegisterNewSpell("EidolonBoost", 1, (spellId, spellcaster, spellLevel, inCombat, spellInformation) => {
                return Spells.CreateModern(illEidolonBoost, "Eidolon Boost", new[] { tSummoner, Trait.Cantrip, Trait.Evocation, Trait.Uncommon },
                        "You focus deeply on the link between you and your eidolon and boost the power of its attacks.",
                        "Your eidolon gains a +2 status bonus to damage rolls with its unarmed attacks.\n\n{b}Special.{/b} If your eidolon's Strikes deal more than one weapon damage die, the status bonus increases to 2 per weapon damage die, to a maximum of +8 with four weapon damage dice.",
                        Target.RangedFriend(20).WithAdditionalConditionOnTargetCreature((CreatureTargetingRequirement)new EidolonCreatureTargetingRequirement(qfSummonerBond)), spellLevel, null)
                    .WithSoundEffect(SfxName.Abjuration)
                    .WithEffectOnEachTarget((Delegates.EffectOnEachTarget)(async (spell, caster, target, result) => {
                        target.RemoveAllQEffects(qfActTogether => qfActTogether.Name == "Reinforce Eidolon");
                        QEffect buff = new QEffect("Eidolon Boost", "+2 status bonus to damage per damage die on unarmed attacks.") {
                            Key = "Eidolon Boost",
                            Source = caster,
                            CountsAsABuff = true,
                            Illustration = illEidolonBoost,
                            BonusToDamage = (qf, action, target) => {
                                if (!action.HasTrait(Trait.Unarmed)) {
                                    return null;
                                }
                                int dice = action.TrueDamageFormula.ToString()[0] - '0';

                                int bestDice = 0;

                                Item? mainHand = GetSummoner(qf.Owner).PrimaryItem;
                                Item? offHand = GetSummoner(qf.Owner).SecondaryItem;

                                if (mainHand != null && mainHand.WeaponProperties != null) {
                                    bestDice = mainHand.WeaponProperties.DamageDieCount;
                                }
                                if (offHand != null && offHand.WeaponProperties != null) {
                                    bestDice = offHand.WeaponProperties.DamageDieCount > bestDice ? offHand.WeaponProperties.DamageDieCount : bestDice;
                                }

                                if (bestDice >= dice) {
                                    dice = bestDice;
                                }

                                return new Bonus(dice * 2, BonusType.Status, "Eidolon Boost");
                            },
                            ExpiresAt = ExpirationCondition.ExpiresAtStartOfSourcesTurn,
                        };
                        QEffect extender = caster.QEffects.FirstOrDefault(qf => qf.Id == qfExtendBoostExtender);
                        if (extender != null) {
                            buff.ExpiresAt = ExpirationCondition.CountsDownAtStartOfSourcesTurn;
                            buff.Value = extender.Value;
                            extender.ExpiresAt = ExpirationCondition.Immediately;
                        }
                        target.AddQEffect(buff);
                    })).WithActionCost(1);
            }));

            spellList.Add(SummonerSpellId.ReinforceEidolon, ModManager.RegisterNewSpell("ReinforceEidolon", 1, (spellId, spellcaster, spellLevel, inCombat, spellInformation) => {
                return Spells.CreateModern(illReinforceEidolon, "Reinforce Eidolon", new[] { tSummoner, Trait.Cantrip, Trait.Abjuration, Trait.Uncommon },
                        "You focus deeply on the link between you and your eidolon and reinforce your eidolon's defenses.",
                        "Your eidolon gains a +1 status bonus to AC and saving throws, plus resistance to all damage equal to half the spell's level.\n\n{b}Special.{/b} Your eidolon can benefit from either boost eidolon or reinforce eidolon, but not both; if you cast one of these spells during the other's duration, the newer spell replaces the older one.",
                        Target.RangedFriend(20).WithAdditionalConditionOnTargetCreature((CreatureTargetingRequirement)new EidolonCreatureTargetingRequirement(qfSummonerBond)), spellLevel, null)
                    .WithSoundEffect(SfxName.Abjuration)
                    .WithEffectOnEachTarget((Delegates.EffectOnEachTarget)(async (spell, caster, target, result) => {
                        target.RemoveAllQEffects(qfActTogether => qfActTogether.Name == "Eidolon Boost");
                        QEffect buff = new QEffect("Reinforce Eidolon", "+1 status bonus to AC and all saves." + (spellLevel > 1 ? " Plus resist " + spellLevel / 2 + " to all damage." : "")) {
                            Key = "Reinforce Eidolon",
                            Source = caster,
                            CountsAsABuff = true,
                            Illustration = illReinforceEidolon,
                            BonusToDefenses = (qf, action, target) => {
                                return new Bonus(1, BonusType.Status, "Reinforce Eidolon");
                            },
                            StateCheck = (qfResistance =>
                                qfResistance.Owner.WeaknessAndResistance.Hardness = 1),
                            WhenExpires = qf => {
                                qf.Owner.WeaknessAndResistance.Hardness = 0;
                            },
                            ExpiresAt = ExpirationCondition.ExpiresAtStartOfSourcesTurn,
                        };
                        QEffect extender = caster.QEffects.FirstOrDefault(qf => qf.Id == qfExtendBoostExtender);
                        if (extender != null) {
                            buff.ExpiresAt = ExpirationCondition.CountsDownAtStartOfSourcesTurn;
                            buff.Value = extender.Value;
                            extender.ExpiresAt = ExpirationCondition.Immediately;
                        }
                        target.AddQEffect(buff);
                    })).WithActionCost(1);
            }));

            spellList.Add(SummonerSpellId.LifelinkSurge, ModManager.RegisterNewSpell("LifelinkSurgeSpell", 2, (spellId, spellcaster, spellLevel, inCombat, spellInformation) => {
                return Spells.CreateModern(illLifeLink, "Lifelink Surge", new[] { tSummoner, Trait.Focus, Trait.Healing, Trait.Positive, Trait.Necromancy, Trait.Uncommon },
                        "You make a quick gesture, tracing the link between yourself and your eidolon and drawing on your connection to slowly strengthen your shared life force.",
                        "Your eidolon gains fast healing 4 for 4 rounds, which causes it to heal 4 HP at the start of each of its turns.",
                        Target.RangedFriend(20).WithAdditionalConditionOnTargetCreature((CreatureTargetingRequirement)new EidolonCreatureTargetingRequirement(qfSummonerBond)), spellLevel, null)
                    .WithSoundEffect(SfxName.Healing)
                    .WithEffectOnEachTarget((Delegates.EffectOnEachTarget)(async (spell, caster, target, result) => {
                        target.AddQEffect(new QEffect("Lifelink Boost", "You gain Fast Healing 4.") {
                            Value = 4,
                            Source = caster,
                            Illustration = illLifeLink,
                            StartOfYourTurn = (async (qf, self) => {
                                self.Heal("4", null);
                            }),
                            ExpiresAt = ExpirationCondition.CountsDownAtStartOfSourcesTurn,
                        });
                    })).WithActionCost(1);
            }));

            spellList.Add(SummonerSpellId.ExtendBoost, ModManager.RegisterNewSpell("ExtendBoostSpell", 1, (spellId, spellcaster, spellLevel, inCombat, spellInformation) => {
                return Spells.CreateModern(illExtendBoost, "Extend Boost", new[] { tSummoner, Trait.Focus, Trait.Metamagic, Trait.Divination, Trait.Uncommon },
                        "You focus on the intricacies of the magic binding you to your eidolon to extend the duration of your boost eidolon or reinforce eidolon spell.",
                        "If your next action is to cast boost eidolon or reinforce eidolon, attempt a skill check with the skill associated with the tradition of magic you gain from your eidolon (such as Nature for a primal eidolon) vs. a standard-difficulty DC of your level. The effect depends on the result of your check.\r\n\r\n{b}Critical Success{/b} The spell lasts 4 rounds.\r\n{b}Success{/b} The spell lasts 3 rounds.\r\n{b}Failure{/b} The spell lasts 1 round, but you don't spend the Focus Point for casting this spell.\r\n",
                        Target.Self(), spellLevel, null)
                    .WithSoundEffect(SfxName.MinorAbjuration)
                    .WithEffectOnEachTarget((Delegates.EffectOnEachTarget)(async (spell, caster, target, result) => {
                        target.AddQEffect(new QEffect("Extend Boost Toggled", "The duration of the next Boost Eidolon or Reinforce Eidolon cantrip you cast will be extended.") {
                            Illustration = illExtendBoost,
                            YouBeginAction = (async (qf, action) => {
                                if (action.SpellId != null && (action.SpellId == spellList[SummonerSpellId.EidolonBoost] || action.SpellId == spellList[SummonerSpellId.ReinforceEidolon])) {
                                    CheckResult result = CommonSpellEffects.RollCheck("Extend Boost Check", new ActiveRollSpecification(Checks.SkillCheck(SpellTraditionToSkill(qf.Owner.PersistentCharacterSheet.Calculated.SpellRepertoires[tSummoner].SpellList)), Checks.FlatDC(GetDCByLevel(qf.Owner.Level))), qf.Owner, qf.Owner);
                                    int duration = 0;
                                    if (result == CheckResult.Failure) {
                                        spellcaster.Spellcasting.FocusPoints += 1;
                                        spellcaster.Occupies.Overhead("Focus point refunded", Color.Green);
                                    } else if (result == CheckResult.Success) {
                                        duration = 3;
                                    } else if (result == CheckResult.CriticalSuccess) {
                                        duration = 4;
                                    }

                                    if (duration > 0) {
                                        target.AddQEffect(new QEffect() {
                                            Id = qfExtendBoostExtender,
                                            Value = duration
                                        });
                                    }
                                } else {
                                    spellcaster.Spellcasting.FocusPoints += 1;
                                    spellcaster.Occupies.Overhead("Focus point refunded", Color.Green);
                                }
                                qf.ExpiresAt = ExpirationCondition.Immediately;
                            })
                        });

                    })).WithActionCost(0);
            }));


            // Add new spells HERE


            return spellList;
        }

        private static Skill SpellTraditionToSkill(Trait tradition) {
            switch (tradition) {
                case Trait.Divine:
                    return Skill.Religion;
                    break;
                case Trait.Arcane:
                    return Skill.Arcana;
                    break;
                case Trait.Primal:
                    return Skill.Nature;
                    break;
                case Trait.Occult:
                    return Skill.Occultism;
                    break;
                default:
                    return Skill.Society;
            }
        }

        private static int GetDCByLevel(int level) {
            switch (level) {
                case 0:
                    return 14;
                    break;
                case 1:
                    return 15;
                    break;
                case 2:
                    return 16;
                    break;
                case 3:
                    return 18;
                    break;
                case 4:
                    return 19;
                    break;
                case 5:
                    return 20;
                    break;
                case 6:
                    return 22;
                    break;
                case 7:
                    return 23;
                    break;
                case 8:
                    return 24;
                    break;
                default:
                    throw new Exception("ERROR: Invalid player level.");
                    return 30;
            }
        }
    }
}
