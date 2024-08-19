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
            EidolonBoost
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
                        string text = "Until the end of the encounter, our eidolon ";
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
                        target.AddQEffect(new QEffect("Eidolon Boost", "+2 status bonus to damage per damage die on unarmed attacks.") {
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
                            ExpiresAt = ExpirationCondition.ExpiresAtEndOfYourTurn,
                        });
                    })).WithActionCost(1);
            }));

            // Add new spells HERE


            return spellList;
        }
    }
}
