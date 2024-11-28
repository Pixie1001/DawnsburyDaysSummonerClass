﻿using System;
using System.Collections.Generic;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using Dawnsbury;
using Dawnsbury.Audio;
using Dawnsbury.Core;
using Dawnsbury.Auxiliary;
using Dawnsbury.Core.Mechanics.Rules;
using Dawnsbury.Core.Animations;
using Dawnsbury.Core.CharacterBuilder;
using Dawnsbury.Core.CharacterBuilder.AbilityScores;
using Dawnsbury.Core.CharacterBuilder.Feats;
using Dawnsbury.Core.CharacterBuilder.FeatsDb;
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
using Dawnsbury.Core.Mechanics.Damage;
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
using Dawnsbury.Core.Animations.Movement;
using Dawnsbury.IO;
using Dawnsbury.Modding;
using Dawnsbury.Mods.Classes.Summoner;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using static Dawnsbury.Mods.Classes.Summoner.SummonerSpells;
using static Dawnsbury.Mods.Classes.Summoner.SummonerClassLoader;
using static Dawnsbury.Mods.Classes.Summoner.Enums;
using Microsoft.VisualBasic;
using static System.Net.Mime.MediaTypeNames;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Data;

// TODO: Setup to remove handwrap transfer entirely, in case a future patch breaks them, and instead just add runes based on invested weapon

namespace Dawnsbury.Mods.Classes.Summoner {
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    internal static class InvestedWeaponLogic {

        public static QEffect EidolonInvestedWeaponQEffect() {
            return new QEffect() {
                StateCheck = self => {
                    Creature summoner = GetSummoner(self.Owner);

                    if (summoner.FindQEffect(qfInvestedWeapon) != null) {
                        QEffect investedWeaponQf = summoner.FindQEffect(qfInvestedWeapon);
                        Item investedWeapon = investedWeaponQf.Tag as Item;

                        List<Item> unarmedAttacks = new List<Item>() { self.Owner.UnarmedStrike };
                        self.Owner.QEffects.Where(qf => qf.AdditionalUnarmedStrike != null).ForEach(qf => unarmedAttacks.Add(qf.AdditionalUnarmedStrike));

                        bool held = summoner.HeldItems.Contains(investedWeapon) || investedWeapon.ItemName == ItemName.HandwrapsOfMightyBlows;

                        foreach (Item attack in unarmedAttacks) {
                            if (held && investedWeapon.ItemModifications.Any(mod => mod.Kind == ItemModificationKind.PlusOneStriking)) {
                                attack.WeaponProperties.DamageDieCount = 2;
                                attack.WeaponProperties.ItemBonus = 1;
                            } else {
                                attack.WeaponProperties.DamageDieCount = 1;
                                attack.WeaponProperties.ItemBonus = 0;
                            }
                            if (held && investedWeapon.ItemModifications.Any(mod => mod.Kind == ItemModificationKind.PlusOne)) {
                                attack.WeaponProperties.ItemBonus = 1;
                            } else if (!investedWeapon.ItemModifications.Any(mod => mod.Kind == ItemModificationKind.PlusOneStriking)) {
                                attack.WeaponProperties.ItemBonus = 0;
                            }
                        }
                    }
                },
                BonusToSkillChecks = (Func<Skill, CombatAction, Creature?, Bonus?>)((skill, action, creature) => {
                    if (action.Owner.BaseName == "Pseudocreature") {
                        return null;
                    }

                    Item naturalWeapon1 = action.Owner.UnarmedStrike;
                    Item naturalWeapon2 = action.Owner.QEffects.FirstOrDefault(qf => qf.AdditionalUnarmedStrike != null && qf.AdditionalUnarmedStrike.WeaponProperties.Melee).AdditionalUnarmedStrike;

                    Trait[] traits = naturalWeapon1.Traits.Concat(naturalWeapon2.Traits).ToArray();

                    bool applies = false;

                    if (action.ActionId == ActionId.Disarm && traits.Contains(Trait.Disarm)) {
                        applies = true;
                    } else if (action.ActionId == ActionId.Trip && traits.Contains(Trait.Trip)) {
                        applies = true;
                    } else if (action.ActionId == ActionId.Shove && traits.Contains(Trait.Shove)) {
                        applies = true;
                    } else if (action.ActionId == ActionId.Grapple && traits.Contains(tGrapple)) {
                        applies = true;
                    }

                    if (!applies) {
                        return null;
                    }

                    if (naturalWeapon1.WeaponProperties.ItemBonus > 0) {
                        return new Bonus(naturalWeapon1.WeaponProperties.ItemBonus, BonusType.Item, $"{(GetSummoner(action.Owner).FindQEffect(qfInvestedWeapon).Tag as Item).Name}");
                    }
                    return null;
                }),
            };
        }

        //private static void HandleRune(Creature summoner, Item investedWeapon, List<Item> unarmedAttacks, ItemModificationKind type) {
        //    ItemModification? propertyRune = investedWeapon.ItemModifications.FirstOrDefault(rune => rune.Kind == type);

        //    foreach (Item attack in unarmedAttacks) {
        //        if (propertyRune == null) {
        //            attack.ItemModifications.RemoveAll(mod => mod.Kind == type);
        //            if (type == ItemModificationKind.PlusOne)
        //                attack.WeaponProperties.DamageDieCount = 1;
        //            if (type == ItemModificationKind.PlusOneStriking)
        //                attack.WeaponProperties.ItemBonus = 0;
        //        } else if (!attack.ItemModifications.Any(mod => mod.Kind == type)) {
        //            attack.WithModification(new ItemModification(type));
        //        }
        //    }

            //if (propertyRune == null || (!summoner.HeldItems.Contains(investedWeapon) && investedWeapon.ItemName != ItemName.HandwrapsOfMightyBlows) || (unarmedAttacks[0].ItemModifications.FirstOrDefault(mod => mod.Kind == type) != null)) {
            //    foreach (Item attack in unarmedAttacks) {
            //        ItemModification? grantedRune = attack.ItemModifications.FirstOrDefault(rune => rune.Kind == type);
            //        attack.ItemModifications.Remove(grantedRune);
            //        if (grantedRune != null) {
            //            attack.WeaponProperties = GenerateDefaultWeaponProperties(attack, type);
            //        }
            //    }
            //}
            //if (propertyRune != null && !(!summoner.HeldItems.Contains(investedWeapon) && investedWeapon.ItemName != ItemName.HandwrapsOfMightyBlows)) {
            //    foreach (Item attack in unarmedAttacks) {
            //        attack.ItemModifications.Add(propertyRune);
            //        //propertyRune.ModifyItem(attack);
            //    }
            //}
        //}

        private static WeaponProperties GenerateDefaultWeaponProperties(Item attack, ItemModificationKind mod) {
            Item baseItem = new Item(attack.Illustration, attack.Name, attack.Traits.ToArray()).WithWeaponProperties(new WeaponProperties($"1d{attack.WeaponProperties.DamageDieSize}", attack.WeaponProperties.DamageKind));

            if (attack.WeaponProperties.RangeIncrement > 0) {
                baseItem.WeaponProperties.WithRangeIncrement(attack.WeaponProperties.RangeIncrement);
            }
            foreach (ItemModification rune in attack.ItemModifications.Where(r => r.Kind != mod)) {
                rune.ModifyItem(attack);
            }

            return baseItem.WeaponProperties;
        }

        public static QEffect SummonerInvestedWeaponQEffect() {
            return new QEffect() {
                ProvideSectionIntoSubmenu = (self, submenu) => {
                    if (submenu.SubmenuId == SubmenuId.OtherManeuvers) {
                        return new PossibilitySection("Summoner").WithPossibilitySectionId(psSummonerExtra);
                    }
                    return null;
                },
                ProvideActionIntoPossibilitySection = (self, section) => {
                    if (section.PossibilitySectionId != psSummonerExtra) {
                        return null;
                    }

                    // Determine options
                    List<Item> itemOptions = new List<Item>();
                    Item? handwraps = StrikeRules.GetBestHandwraps(self.Owner);
                    if (handwraps != null && handwraps.ItemModifications.Count > 0) {
                        itemOptions.Add(handwraps);
                    }
                    List<Item> weapons = self.Owner.HeldItems;
                    if (weapons.Count >= 1 && weapons[0].WeaponProperties != null && weapons[0].ItemModifications.Count > 0) {
                        itemOptions.Add(weapons[0]);
                    }
                    if (weapons.Count == 2 && weapons[1].WeaponProperties != null && weapons[1].ItemModifications.Count > 0) {
                        itemOptions.Add(weapons[1]);
                    }
                    if (self.Owner.FindQEffect(qfInvestedWeapon) != null) {
                        itemOptions.Remove((Item)self.Owner.FindQEffect(qfInvestedWeapon).Tag);
                    }

                    SubmenuPossibility menu = new SubmenuPossibility(illInvest, "Invest Weapon");
                    menu.Subsections.Add(new PossibilitySection("Invest Weapon"));

                    foreach (Item item in itemOptions) {
                        menu.Subsections[0].AddPossibility((ActionPossibility)new CombatAction(self.Owner, item.Illustration, $"Invest {item.Name}", new Trait[] { Trait.Manipulate },
                            $"Invest {item.Name}, so your eidolon can benefit from it. This will cause your previously invested weapon to become uninvested.", Target.Self())
                        .WithSoundEffect(SfxName.MagicWeapon)
                        .WithActionCost(1)
                        .WithEffectOnSelf(async user => {
                            Creature eidolon = GetEidolon(user);

                            QEffect? oldInvestedEffect = self.Owner.FindQEffect(qfInvestedWeapon);
                            if (oldInvestedEffect != null) {
                                oldInvestedEffect.ExpiresAt = ExpirationCondition.Immediately;
                            }
                            self.Owner.AddQEffect(new QEffect($"Invested Weapon ({item.Name})",
                                item.ItemName == ItemName.HandwrapsOfMightyBlows ? "Your eidolon also benefits from these handwraps of mighty blows." : "While wielding this weapon, your eidolon benefits from its runestones.") {
                                Tag = item,
                                Id = qfInvestedWeapon,
                                Illustration = illInvest
                            });
                        })
                        );
                    }

                    foreach (ActionPossibility possibility in menu.Subsections[0].Possibilities) {
                        possibility.PossibilitySize = PossibilitySize.Half;
                    }
                    menu.WithPossibilityGroup("Summoner");
                    return menu;
                }
            };
        }

        public static void MagicItemLogic(Creature summoner, Creature eidolon) {

            // Share item bonuses
            List<Item> wornItems = summoner.CarriedItems.Where(item => item.IsWorn == true && item.HasTrait(Trait.Invested) && item.PermanentQEffectActionWhenWorn != null).ToList<Item>();
            foreach (Item item in wornItems) {
                QEffect qf1 = new QEffect() {
                    Source = summoner,
                    Owner = eidolon
                };
                QEffect qf2 = new QEffect();
                item.PermanentQEffectActionWhenWorn(qf2, item);
                qf1.BonusToSkills = qf2.BonusToSkills;
                eidolon.AddQEffect(qf1);
            }

            // Share benfits of handwraps
            Item handwraps = StrikeRules.GetBestHandwraps(summoner);
            List<Item> weapons = summoner.HeldItems;
            if (handwraps != null) {
                Item eidolonHandwraps = handwraps.Duplicate();
                summoner.AddQEffect(new QEffect($"Invested Weapon ({eidolonHandwraps.Name})", "Your eidolon also benefits from these handwraps of mighty blows.") {
                    Tag = handwraps,
                    Id = qfInvestedWeapon,
                    Illustration = illInvest
                });
            } else if (weapons.Count() > 0 && weapons[0].ItemModifications.Count > 0) {
                summoner.AddQEffect(new QEffect($"Invested Weapon ({weapons[0].Name})", "While wielding this weapon, your eidolon benefits from its runestones.") {
                    Tag = weapons[0],
                    Id = qfInvestedWeapon,
                    Illustration = illInvest
                });
            } else if (weapons.Count() == 2 && weapons[1].ItemModifications.Count > 0) {
                summoner.AddQEffect(new QEffect($"Invested Weapon ({weapons[1].Name})", "While wielding this weapon, your eidolon benefits from its runestones.") {
                    Tag = weapons[1],
                    Id = qfInvestedWeapon,
                    Illustration = illInvest
                });
            }
        }

    }
}



            
            
            //} else if (summoner.HasFeat(scBeastEidolon)) {
            //    
            //    }
            //} else if (summoner.HasFeat(scDevoPhantomEidolon)) {
            //    
            //    }
            //} else if (summoner.HasFeat(scAzataEidolon)) {
            //    
            //} else if (summoner.HasFeat(scDevilEidolon)) {
            //    
            //} else if (summoner.HasFeat(scAngerPhantom)) {
            //    
            //}
