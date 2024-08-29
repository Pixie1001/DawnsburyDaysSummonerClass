using Dawnsbury.Core.CharacterBuilder.Feats;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Spellbook;
using Dawnsbury.Core.CharacterBuilder.Spellcasting;
using Dawnsbury.Core.CharacterBuilder;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Core.Mechanics.Treasure;
using Dawnsbury.Modding;

//spellSource.WithSpells(new SpellId[] { SpellId.Bless, SpellId.ScorchingRay, SpellId.ScorchingRay, SpellId.ScorchingRay, SpellId.Fireball, SpellId.Fireball });
//spellSource.FocusSpells.Add(AllSpells.CreateSpellInCombat(spells[SummonerSpellId.EidolonsWrath], eidolon, 3, tSummoner));

//Item? mainHand = summoner.PrimaryItem;
//Item? offHand = summoner.SecondaryItem;

//YouBeginAction = (async (qf, action) => {
//    if (!action.Name.StartsWith("Strike (")) {
//        return;
//    }

//    int bonus = 0;
//    int dice = 1;

//    Item? mainHand = summoner.PrimaryItem;
//    Item? offHand = summoner.SecondaryItem;

//    if (mainHand != null && mainHand.WeaponProperties != null) {
//        bonus = mainHand.WeaponProperties.ItemBonus;
//        dice = mainHand.WeaponProperties.DamageDieCount;
//    }
//    if (offHand != null && offHand.WeaponProperties != null && offHand.WeaponProperties.ItemBonus > bonus) {
//        bonus = offHand.WeaponProperties.ItemBonus;
//        dice = offHand.WeaponProperties.DamageDieCount;
//    }

//    if (qf.Owner.UnarmedStrike.WeaponProperties.ItemBonus < bonus) {
//        qf.Owner.UnarmedStrike.WeaponProperties.ItemBonus = bonus;
//    }


//}),

//yield return new TrueFeat(ModManager.RegisterFeatName("AbundantSpellCastingSummoner1-Divine", "Blessed Vessel"), 1, "Your strong connect to your eidolon grants you additional spells.",
//    $"You gain an extra level 1 spell slot, and learn the {AllSpells.CreateModernSpellTemplate(SpellId.Bless, tSummoner).ToSpellLink()} spell.\n\nUnlike your other spells, this spell is not a signature spell.",
//    new Trait[2] { tSummoner, Trait.Homebrew })
//.WithPrerequisite(sheet => sheet.SpellRepertoires[tSummoner].SpellList == Trait.Divine, "You must be a divine caster.")
//.WithOnSheet((Action<CalculatedCharacterSheetValues>)(values => {
//    if (!values.SpellRepertoires.ContainsKey(tSummoner))
//        return;
//    ++values.SpellRepertoires[tSummoner].SpellSlots[1];
//}))
//.WithOnCreature((sheet, creature) => {
//    Spell spell = AllSpells.CreateModernSpellTemplate(SpellId.Bless, tSummoner, 1);
//    List<Spell> spellsKnown = sheet.SpellRepertoires[tSummoner].SpellsKnown;
//    if (spellsKnown.FirstOrDefault(s => s.SpellId == spell.SpellId && s.SpellLevel == spell.SpellLevel) == null) {
//        spellsKnown.Add(spell);
//    }
//});

//yield return new TrueFeat(ModManager.RegisterFeatName("AbundantSpellCastingSummoner4-Divine", "Blood of Martyrs"), 4, "Your strong connect to your eidolon grants you additional spells.",
//    $"You gain an extra level 2 spell slot, and learn the {AllSpells.CreateModernSpellTemplate(SpellId.BloodVendetta, tSummoner).ToSpellLink()} spell.\n\nUnlike your other spells, this spell is not a signature spell.",
//    new Trait[2] { tSummoner, Trait.Homebrew })
//.WithPrerequisite(sheet => sheet.SpellRepertoires[tSummoner].SpellList == Trait.Divine, "You must be a divine caster.")
//.WithOnSheet((Action<CalculatedCharacterSheetValues>)(values => {
//    if (!values.SpellRepertoires.ContainsKey(tSummoner))
//        return;
//    ++values.SpellRepertoires[tSummoner].SpellSlots[2];
//}))
//.WithOnCreature((sheet, creature) => {
//    Spell spell = AllSpells.CreateModernSpellTemplate(SpellId.BloodVendetta, tSummoner, 2);
//    List<Spell> spellsKnown = sheet.SpellRepertoires[tSummoner].SpellsKnown;
//    if (spellsKnown.FirstOrDefault(s => s.SpellId == spell.SpellId && s.SpellLevel == spell.SpellLevel) == null) {
//        spellsKnown.Add(spell);
//    }
//});

//yield return new TrueFeat(ModManager.RegisterFeatName("AbundantSpellCastingSummoner1-Occult", "Terror from Beyond"), 1, "Your strong connect to your eidolon grants you additional spells.",
//    $"You gain an extra level 1 spell slot, and learn the {AllSpells.CreateModernSpellTemplate(SpellId.Fear, tSummoner).ToSpellLink()} spell.\n\nUnlike your other spells, this spell is not a signature spell.",
//    new Trait[2] { tSummoner, Trait.Homebrew })
//.WithPrerequisite(sheet => sheet.SpellRepertoires[tSummoner].SpellList == Trait.Occult, "You must be a occult caster.")
//.WithOnSheet((Action<CalculatedCharacterSheetValues>)(values => {
//    if (!values.SpellRepertoires.ContainsKey(tSummoner))
//        return;
//    ++values.SpellRepertoires[tSummoner].SpellSlots[1];
//}))
//.WithOnCreature((sheet, creature) => {
//    Spell spell = AllSpells.CreateModernSpellTemplate(SpellId.Fear, tSummoner, 1);
//    List<Spell> spellsKnown = sheet.SpellRepertoires[tSummoner].SpellsKnown;
//    if (spellsKnown.FirstOrDefault(s => s.SpellId == spell.SpellId && s.SpellLevel == spell.SpellLevel) == null) {
//        spellsKnown.Add(spell);
//    }
//});

//yield return new TrueFeat(ModManager.RegisterFeatName("AbundantSpellCastingSummoner4-Occult/Arcane", "Dimensional Distortion"), 4, "Your strong connect to your eidolon grants you additional spells.",
//    $"You gain an extra level 2 spell slot, and learn the {AllSpells.CreateModernSpellTemplate(SpellId.Blur, tSummoner).ToSpellLink()} spell.\n\nUnlike your other spells, this spell is not a signature spell.", new Trait[2] {
//                tSummoner,
//                Trait.Homebrew
//})
//.WithPrerequisite(sheet => sheet.SpellRepertoires[tSummoner].SpellList == Trait.Occult || sheet.SpellRepertoires[tSummoner].SpellList == Trait.Arcane, "You must be an arcane or occult caster.")
//.WithOnSheet((Action<CalculatedCharacterSheetValues>)(values => {
//    if (!values.SpellRepertoires.ContainsKey(tSummoner))
//        return;
//    ++values.SpellRepertoires[tSummoner].SpellSlots[2];
//}))
//.WithOnCreature((sheet, creature) => {
//    Spell spell = AllSpells.CreateModernSpellTemplate(SpellId.Blur, tSummoner, 2);
//    List<Spell> spellsKnown = sheet.SpellRepertoires[tSummoner].SpellsKnown;
//    if (spellsKnown.FirstOrDefault(s => s.SpellId == spell.SpellId && s.SpellLevel == spell.SpellLevel) == null) {
//        spellsKnown.Add(spell);
//    }
//});

//yield return new TrueFeat(ModManager.RegisterFeatName("AbundantSpellCastingSummoner1-Arcane", "Protective Bond"), 1, "Your strong connect to your eidolon grants you additional spells.",
//    $"You gain an extra level 1 spell slot, and learn the {AllSpells.CreateModernSpellTemplate(SpellId.MageArmor, tSummoner).ToSpellLink()} spell.\n\nUnlike your other spells, this spell is not a signature spell.",
//    new Trait[2] { tSummoner, Trait.Homebrew })
//.WithPrerequisite(sheet => sheet.SpellRepertoires[tSummoner].SpellList == Trait.Arcane, "You must be an arcane caster.")
//.WithOnSheet((Action<CalculatedCharacterSheetValues>)(values => {
//    if (!values.SpellRepertoires.ContainsKey(tSummoner))
//        return;
//    ++values.SpellRepertoires[tSummoner].SpellSlots[1];
//}))
//.WithOnCreature((sheet, creature) => {
//    Spell spell = AllSpells.CreateModernSpellTemplate(SpellId.MageArmor, tSummoner, 1);
//    List<Spell> spellsKnown = sheet.SpellRepertoires[tSummoner].SpellsKnown;
//    if (spellsKnown.FirstOrDefault(s => s.SpellId == spell.SpellId && s.SpellLevel == spell.SpellLevel) == null) {
//        spellsKnown.Add(spell);
//    }
//});

//yield return new TrueFeat(ModManager.RegisterFeatName("AbundantSpellCastingSummoner1-Primal", "Tears of the Forest"), 1, "Your strong connect to your eidolon grants you additional spells.",
//    $"You gain an extra level 1 spell slot, and learn the {AllSpells.CreateModernSpellTemplate(SpellId.Grease, tSummoner).ToSpellLink()} spell.\n\nUnlike your other spells, this spell is not a signature spell.",
//    new Trait[2] { tSummoner, Trait.Homebrew })
//.WithPrerequisite(sheet => sheet.SpellRepertoires[tSummoner].SpellList == Trait.Primal, "You must be a primal caster.")
//.WithOnSheet((Action<CalculatedCharacterSheetValues>)(values => {
//    if (!values.SpellRepertoires.ContainsKey(tSummoner))
//        return;
//    ++values.SpellRepertoires[tSummoner].SpellSlots[1];
//}))
//.WithOnCreature((sheet, creature) => {
//    Spell spell = AllSpells.CreateModernSpellTemplate(SpellId.Grease, tSummoner, 1);
//    List<Spell> spellsKnown = sheet.SpellRepertoires[tSummoner].SpellsKnown;
//    if (spellsKnown.FirstOrDefault(s => s.SpellId == spell.SpellId && s.SpellLevel == spell.SpellLevel) == null) {
//        spellsKnown.Add(spell);
//    }
//});

//yield return new TrueFeat(ModManager.RegisterFeatName("AbundantSpellCastingSummoner4-Primal", "Flesh of the Dryad"), 4, "Your strong connect to your eidolon grants you additional spells.",
//    $"You gain an extra level 2 spell slot, and learn the {AllSpells.CreateModernSpellTemplate(SpellId.Barkskin, tSummoner).ToSpellLink()} spell.\n\nUnlike your other spells, this spell is not a signature spell.",
//    new Trait[2] { tSummoner, Trait.Homebrew })
//.WithPrerequisite(sheet => sheet.SpellRepertoires[tSummoner].SpellList == Trait.Primal, "You must be a primal caster.")
//.WithOnSheet((Action<CalculatedCharacterSheetValues>)(values => {
//    if (!values.SpellRepertoires.ContainsKey(tSummoner))
//        return;
//    ++values.SpellRepertoires[tSummoner].SpellSlots[2];
//}))
//.WithOnCreature((sheet, creature) => {
//    Spell spell = AllSpells.CreateModernSpellTemplate(SpellId.Barkskin, tSummoner, 2);
//    List<Spell> spellsKnown = sheet.SpellRepertoires[tSummoner].SpellsKnown;
//    if (spellsKnown.FirstOrDefault(s => s.SpellId == spell.SpellId && s.SpellLevel == spell.SpellLevel) == null) {
//        spellsKnown.Add(spell);
//    }
//});

//spellList.Add(SummonerSpellId.ExtendBoost, ModManager.RegisterNewSpell("ExtendBoostSpell", 1, (spellId, spellcaster, spellLevel, inCombat, spellInformation) => {
//    return Spells.CreateModern(illExtendBoost, "Extend Boost", new[] { tSummoner, Trait.Focus, Trait.Metamagic, Trait.Divination, Trait.Uncommon },
//            "FLAVOUR.",
//            "CRUNCH.",
//            Target.RangedFriend(20).WithAdditionalConditionOnTargetCreature((CreatureTargetingRequirement)new EidolonCreatureTargetingRequirement(qfSummonerBond)), spellLevel, null)
//        .WithActiveRollSpecification(new ActiveRollSpecification(Checks.SkillCheck((spellcaster != null ? SpellTraditionToSkill(spellcaster.PersistentCharacterSheet.Calculated.SpellRepertoires[tSummoner].SpellList) : Skill.Arcana)), Checks.FlatDC(GetDCByLevel((spellcaster != null ? spellcaster.Level : 1)))))
//        .WithSoundEffect(SfxName.Abjuration)
//        .WithVariants(new SpellVariant[] {
//            new SpellVariant("EidolonBoost", "Eidolon Boost", (Illustration) illEidolonBoost)
//        })
//        .WithCreateVariantDescription((Func<int, SpellVariant, string>)((_, variant) => {
//            if (variant.Id == "EidolonBoost") {
//                return "Your eidolon gains a +2 status bonus to damage rolls with its unarmed attacks.\n\n{b}Special.{/b} If your eidolon's Strikes deal more than one weapon damage die, the status bonus increases to 2 per weapon damage die, to a maximum of +8 with four weapon damage dice.";
//            } else if (variant.Id == "ReinforceEidolon") {
//                return "AWAITING IMPLEMENTATION";
//            }
//            return "";
//        }))
//        .WithEffectOnEachTarget((Delegates.EffectOnEachTarget)(async (spell, caster, target, result) => {
//            int duration = 0;
//            ExpirationCondition expire = ExpirationCondition.ExpiresAtStartOfSourcesTurn;
//            if (result == CheckResult.Failure) {
//                spellcaster.Spellcasting.FocusPoints += 1;
//                spellcaster.Occupies.Overhead("Focus point refunded", Color.Green);
//            } else if (result == CheckResult.Success) {
//                duration = 3;
//                expire = ExpirationCondition.CountsDownAtStartOfSourcesTurn;
//            } else if (result == CheckResult.CriticalSuccess) {
//                duration = 4;
//                expire = ExpirationCondition.CountsDownAtStartOfSourcesTurn;
//            }

//            SpellVariant variant = spell.ChosenVariant;
//            if (variant.Id == "EidolonBoost") {
//                target.RemoveAllQEffects(qfActTogether => qfActTogether.Name == "Reinforce Eidolon");
//                target.AddQEffect(new QEffect("Eidolon Boost", "+2 status bonus to damage per damage die on unarmed attacks.") {
//                    Key = "Eidolon Boost",
//                    CountsAsABuff = true,
//                    Illustration = illEidolonBoost,
//                    BonusToDamage = (qf, action, target) => {
//                        if (!action.HasTrait(Trait.Unarmed)) {
//                            return null;
//                        }
//                        int dice = action.TrueDamageFormula.ToString()[0] - '0';

//                        int bestDice = 0;

//                        Item? mainHand = GetSummoner(qf.Owner).PrimaryItem;
//                        Item? offHand = GetSummoner(qf.Owner).SecondaryItem;

//                        if (mainHand != null && mainHand.WeaponProperties != null) {
//                            bestDice = mainHand.WeaponProperties.DamageDieCount;
//                        }
//                        if (offHand != null && offHand.WeaponProperties != null) {
//                            bestDice = offHand.WeaponProperties.DamageDieCount > bestDice ? offHand.WeaponProperties.DamageDieCount : bestDice;
//                        }

//                        if (bestDice >= dice) {
//                            dice = bestDice;
//                        }

//                        return new Bonus(dice * 2, BonusType.Status, "Eidolon Boost");
//                    },
//                    Value = duration,
//                    ExpiresAt = expire
//                });
//            } else if (variant.Id == "ReinforceEidolon") {
//                target.RemoveAllQEffects(qfActTogether => qfActTogether.Name == "Eidolon Boost");
//                target.AddQEffect(new QEffect("Reinforce Eidolon", "+1 status bonus to AC and all saves." + (spellLevel > 1 ? " Plus resist " + spellLevel / 2 + " to all damage." : "")) {
//                    Key = "Reinforce Eidolon",
//                    CountsAsABuff = true,
//                    Illustration = illReinforceEidolon,
//                    BonusToDefenses = (qf, action, target) => {
//                        return new Bonus(1, BonusType.Status, "Reinforce Eidolon");
//                    },
//                    StateCheck = (qfResistance =>
//                        qfResistance.Owner.WeaknessAndResistance.Hardness = 1),
//                    WhenExpires = qf => {
//                        qf.Owner.WeaknessAndResistance.Hardness = 0;
//                    },
//                    Value = duration,
//                    ExpiresAt = expire
//                });
//            }
//        })).WithActionCost(1);
//}));

// Slow test
//summoner.AddQEffect(QEffect.Slowed(2));

//if (summoner.HasFeat(scAngelicEidolon)) {
//    eidolon.AddQEffect(QEffect.Slowed(1));
//}

// Trait.Arcane, new List<FeatName>() { FeatName.Arcana, FeatName.Intimidation }, new Func<Feat, bool>(ft => ft.HasTrait(tAlignment))

//yield return new EidolonBond(ModManager.RegisterFeatName("AdamantineDragon", "Adamantite Dragon"), "Your eidolon is a gleaming adamantite dragon.", "Your eidolon's breath weapon deals mental damage vs. Will.",
//    Trait.Primal, new List<FeatName>() { FeatName.Nature, FeatName.Intimidation }, new Func<Feat, bool>(ft => ft.HasTrait(tAlignment)),
//    new List<Trait>() { Trait.Mental, tRemaster, tDragonType }, new List<Feat>() { dragonConeBreath, dragonLineBreath });
//yield return new EidolonBond(ModManager.RegisterFeatName("ConspiratorDragon", "Conspirator Dragon"), "Your eidolon is a cunning conspirator dragon.", "Your eidolon's breath weapon deals poison damage vs. Fortitude.",
//    Trait.Occult, new List<FeatName>() { FeatName.Occultism, FeatName.Intimidation }, new Func<Feat, bool>(ft => ft.HasTrait(tAlignment)),
//    new List<Trait>() { Trait.Poison, tRemaster, tDragonType }, new List<Feat>() { dragonConeBreath, dragonLineBreath });
//yield return new EidolonBond(ModManager.RegisterFeatName("DiabolicDragon", "Diabolic Dragon"), "Your eidolon is a hellish diabolic dragon.", "Your eidolon's breath weapon deals fire damage vs. Reflex.",
//    Trait.Arcane, new List<FeatName>() { FeatName.Arcana, FeatName.Intimidation }, new Func<Feat, bool>(ft => ft.FeatName == ftALawfulEvil),
//    new List<Trait>() { Trait.Fire, tRemaster, tDragonType }, new List<Feat>() { dragonConeBreath, dragonLineBreath });
//yield return new EidolonBond(ModManager.RegisterFeatName("EmpyrealDragon", "Empyreal Dragon"), "Your eidolon is a heavenly empyreal dragon.", "Your eidolon's breath weapon deals good damage vs. Reflex.",
//    Trait.Divine, new List<FeatName>() { FeatName.Religion, FeatName.Intimidation }, new Func<Feat, bool>(ft => ft.HasTrait(tAlignment) && ft.HasTrait(Trait.Good)),
//    new List<Trait>() { Trait.Good, tRemaster, tDragonType }, new List<Feat>() { dragonConeBreath, dragonLineBreath });
//yield return new EidolonBond(ModManager.RegisterFeatName("FortuneDragon", "Fortune Dragon"), "Your eidolon is a bejewled fortune dragon.", "Your eidolon's breath weapon deals force damage vs. Reflex.",
//    Trait.Arcane, new List<FeatName>() { FeatName.Arcana, FeatName.Intimidation }, new Func<Feat, bool>(ft => ft.HasTrait(tAlignment)),
//    new List<Trait>() { Trait.Force, tRemaster, tDragonType }, new List<Feat>() { dragonConeBreath, dragonLineBreath });
//yield return new EidolonBond(ModManager.RegisterFeatName("HornedDragon", "Horned Dragon"), "Your eidolon is a bestial horned dragon.", "Your eidolon's breath weapon deals poison damage vs. Fortitude.",
//    Trait.Primal, new List<FeatName>() { FeatName.Nature, FeatName.Intimidation }, new Func<Feat, bool>(ft => ft.HasTrait(tAlignment)),
//    new List<Trait>() { Trait.Poison, tRemaster, tDragonType }, new List<Feat>() { dragonConeBreath, dragonLineBreath });
//yield return new EidolonBond(ModManager.RegisterFeatName("MirageDragon", "Mirage Dragon"), "Your eidolon is an elusive mirage dragon.", "Your eidolon's breath weapon deals mental damage vs. Will.",
//    Trait.Arcane, new List<FeatName>() { FeatName.Arcana, FeatName.Intimidation }, new Func<Feat, bool>(ft => ft.HasTrait(tAlignment)),
//    new List<Trait>() { Trait.Mental, tRemaster, tDragonType }, new List<Feat>() { dragonConeBreath, dragonLineBreath });
//yield return new EidolonBond(ModManager.RegisterFeatName("OmenDragon", "Omen Dragon"), "Your eidolon is an auspicious omen dragon.", "Your eidolon's breath weapon deals mental damage vs. Will.",
//    Trait.Occult, new List<FeatName>() { FeatName.Occultism, FeatName.Intimidation }, new Func<Feat, bool>(ft => ft.HasTrait(tAlignment)),
//    new List<Trait>() { Trait.Mental, tRemaster, tDragonType }, new List<Feat>() { dragonConeBreath, dragonLineBreath });

//if (self == summoner) {
//    // Swap to partners turn
//    // Only give 1 action
//    // Don't give return control - only end turn
//    // Give status effect explaining rules of their turn

//} else {
//}

//QEffect extraAction = new QEffect("Act Together", "Gain an extra 1 cost action this turn.") {
//    ExpiresAt = ExpirationCondition.ExpiresAtEndOfYourTurn,
//    Illustration = IllustrationName.Haste,
//    QuickenedFor = (action => {
//        if (action.ActionCost == 1) {
//            return true;
//        }
//        return false;
//    }),
//};
//partner.AddQEffect(extraAction);
//if (partner.Actions.QuickenedForActions == null)
//    partner.Actions.QuickenedForActions = new DisjunctionDelegate<CombatAction>(extraAction.QuickenedFor);
//else
//    partner.Actions.QuickenedForActions.Add(extraAction.QuickenedFor);
//partner.Actions.UsedQuickenedAction = partner.Actions.QuickenedForActions == null;
//if (!partner.Actions.UsedQuickenedAction)
//    partner.Actions.GetType().GetMethod("AnimateActionUsedTo", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public).Invoke(partner.Actions, new object[] { 3, ActionDisplayStyle.Quickened });
//else
//    partner.Actions.GetType().GetMethod("AnimateActionUsedTo", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public).Invoke(partner.Actions, new object[] { 3, ActionDisplayStyle.Invisible });

// Runic Body (formerly Magic Fang)
//RemasterSpells.RegisterNewSpell("RunicBody", 1, (spellId, spellcaster, spellLevel, inCombat, spellInformation) =>
//{
//    static bool IsValidTargetForRunicBody(Item? item) {
//        if (item != null && item.HasTrait(Trait.Unarmed) && item.WeaponProperties != null) {
//            if (item.WeaponProperties.DamageDieCount > 1) {
//                return item.WeaponProperties.ItemBonus <= 1;
//            }
//            return true;
//        }
//        return false;
//    }

//    return Spells.CreateModern(IllustrationName.KineticRam, "Runic Body", [Trait.Concentrate, Trait.Manipulate, Trait.Arcane, Trait.Divine, Trait.Occult, Trait.Primal, RemasterSpells.Trait.Remaster],
//        "Glowing runes appear on the target’s body.",
//        "All its unarmed attacks become +1 striking unarmed attacks, gaining a +1 item bonus to attack rolls and increasing the number of damage dice to two.",
//        Target.AdjacentFriendOrSelf()
//    .WithAdditionalConditionOnTargetCreature((Creature a, Creature d) => IsValidTargetForRunicBody(d.UnarmedStrike) ? Usability.Usable : Usability.CommonReasons.TargetIsNotPossibleForComplexReason), spellLevel, null)
//    .WithSoundEffect(SfxName.MagicWeapon)
//    .WithEffectOnEachTarget(async (CombatAction spell, Creature caster, Creature target, CheckResult checkResult) => {
//        Item? item = target.UnarmedStrike;
//        if (item != null && item.WeaponProperties != null) {
//            item.WeaponProperties.DamageDieCount = 2;
//            item.WeaponProperties.ItemBonus = 1;
//        }
//        // Expiration is long enough that we don't need to worry about restoring the item.
//        // I create a buff icon, since otherwise it's not clear that your fist is buffed.
//        target.AddQEffect(new QEffect("Runic Body", "Glowing runes appear on the target’s body.") { Illustration = IllustrationName.KineticRam, CountsAsABuff = true });
//    });
//});


//var test1 = eidolon.Battle.GameLoop.GetType();
//var test2 = eidolon.Battle.GameLoop.GetType().GetMethod("StartOfTurn", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static, null, new Type[] { typeof(Creature) }, null);
//await (Task)eidolon.Battle.GameLoop.GetType().GetMethod("StartOfTurn", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static).Invoke(eidolon.Battle.GameLoop, new object[] { eidolon });


//this.battle.Log("{b}" + creature?.ToString() + "'s turn.{/b}");

//summoner.PersistentCharacterSheet.SelectedFeats[]

//monster.AddQEffect(new QEffect()
//{
//    Id = QEffectId.SummonedBy,
//    Source = caster
//});

//QEffect temp = creature.FindQEffect(QEffectId.SummonedBy)
//if (temp) {
//  Creature summoner = temp.Source
//}

//

//ranger.Battle.SpawnCreature(animalCompanion, ranger.OwningFaction, ranger.Occupies);

//case SpellId.SummonAnimal:
//    int maximumAnimalLevel = level == 1 ? -1 : 1;
//    return Spells.CreateModern((Illustration)IllustrationName.SummonAnimal, "Summon Animal", new Trait[3]
//    {
//            Trait.Conjuration,
//            Trait.Arcane,
//            Trait.Primal
//    }, "You conjure an animal to fight for you.", (DescriptionDescriptor)("You summon an animal creature whose level is " +
//    S.HeightenedVariable(maximumAnimalLevel, -1) + " or less." + Level1Spells.SummonRulesText +
//    S.HeightenText(level, 1, inCombat, "{b}Heightened (2nd){/b} The maximum level of the summoned creature is 1.")), +
//    (Target)Target.RangedEmptyTileForSummoning(6), level, (SpellSavingThrow)null).WithActionCost(3).WithSoundEffect(SfxName.Summoning) +
//    .WithVariants(MonsterStatBlocks.MonsterExemplars.Where<Creature>((Func<Creature, bool>)(animal => animal.HasTrait(Trait.Animal) +
//    && animal.Level <= maximumAnimalLevel)).Select<Creature, SpellVariant>((Func<Creature, SpellVariant>)(animal => new SpellVariant(animal.Name, "Summon " + animal.Name, animal.Illustration)
//    {
//        GoodnessModifier = (Func<AI, float, float>)((ai, original) => original + (float)(animal.Level * 20))
//    })).ToArray<SpellVariant>()).WithCreateVariantDescription((Func<int, SpellVariant, string>)((_, variant) => +
//    RulesBlock.CreateCreatureDescription(MonsterStatBlocks.MonsterExemplarsByName[variant.Id]))) +
//    .WithEffectOnChosenTargets((Delegates.EffectOnChosenTargets)(async (spell, caster, targets) => await CommonSpellEffects.SummonMonster(spell, caster, targets.ChosenTile)));


//var strike = qf.Owner.CreateStrike(item);
//strike.Name = "Snagging " + strike.Name;
//strike.StrikeModifiers.OnEachTarget += async (caster, target, checkResult) =>
//{
//    if (checkResult >= CheckResult.Success) {
//        target.AddQEffect(new QEffect("Snagged", "You're flat-footed until the start of your next turn or no longer within reach of " + caster + "'s hand.", ExpirationCondition.ExpiresAtStartOfYourTurn, caster, IllustrationName.Flatfooted) {
//            IsFlatFootedTo = (qff, crf, caf) => "Snagging Strike",
//            StateCheck = (qfSelf) =>
//            {
//                if (qfSelf.Owner.DistanceTo(caster) > 1) qfSelf.ExpiresAt = ExpirationCondition.Immediately;
//            }
//        });
//    }
//};
//(strike.Target as CreatureTarget).WithAdditionalConditionOnTargetCreature(new AdjacencyCreatureTargetingRequirement());
//(strike.Target as CreatureTarget).WithAdditionalConditionOnTargetCreature((self, target) => self.HasFreeHand ? Usability.Usable : Usability.CommonReasons.NoFreeHandForManeuver);
//return strike;


//.WithOnSheet((Action<CalculatedCharacterSheetValues>)(sheet =>
// {
//     sheet.SetProficiency(Trait.Spell, Proficiency.Trained);
//     sheet.AddFocusSpellAndFocusPoint(Trait.Ranger, Ability.Wisdom, SpellId.MagicHide);
// })).WithRulesBlockForSpell(SpellId.MagicHide, Trait.Ranger).WithIllustration((Illustration)IllustrationName.MagicHide);


//private static Feat CreateAnimalCompanionFeat(FeatName featName, string flavorText) {
//    Creature creature = Ranger.CreateAnimalCompanion(featName, 1);
//    creature.RegeneratePossibilities();
//    foreach (QEffect qeffect in creature.QEffects.ToList<QEffect>()) {
//        Action<QEffect> stateCheck = qeffect.StateCheck;
//        if (stateCheck != null)
//            stateCheck(qeffect);
//    }
//    creature.RecalculateLandSpeed();
//    return new Feat(featName, flavorText, "Flavour text", new List<Trait>(), (List<Feat>)null).WithIllustration(creature.Illustration).WithOnCreature((Action<CalculatedCharacterSheetValues, Creature>)((sheet, ranger) => ranger.AddQEffect(new QEffect()
//    {
//        StartOfCombat = (Func<QEffect, Task>)(qfRangerTechnical =>
//        {
//            if (ranger.PersistentUsedUpResources.AnimalCompanionIsDead) {
//                ranger.Occupies.Overhead("no companion", Color.Green, ranger?.ToString() + "'s animal companion is dead. A new animal companion will find you after your next long rest or downtime.");
//            } else {
//                Creature animalCompanion = Ranger.CreateAnimalCompanion(featName, ranger.Level);
//                animalCompanion.MainName = qfRangerTechnical.Owner.Name + "'s " + animalCompanion.MainName;
//                animalCompanion.AddQEffect(new QEffect()
//                {
//                    Id = QEffectId.RangersCompanion,
//                    Source = ranger,
//                    WhenMonsterDies = (Action<QEffect>)(qfCompanion => ranger.PersistentUsedUpResources.AnimalCompanionIsDead = true)
//                });
//                Action<Creature, Creature> benefitsToCompanion = sheet.RangerBenefitsToCompanion;
//                if (benefitsToCompanion != null)
//                    benefitsToCompanion(animalCompanion, ranger);
//                ranger.Battle.SpawnCreature(animalCompanion, ranger.OwningFaction, ranger.Occupies);
//            }
//        }),
//        EndOfYourTurn = (Func<QEffect, Creature, Task>)((qfRanger, self) =>
//        {
//            if (qfRanger.UsedThisTurn)
//                return;
//            Creature animalCompanion = Ranger.GetAnimalCompanion(qfRanger.Owner);
//            if (animalCompanion == null)
//                return;
//            await animalCompanion.Battle.GameLoop.EndOfTurn(animalCompanion);
//        }),
//        ProvideMainAction = (Func<QEffect, Possibility>)(qfRanger =>
//        {
//            Creature animalCompanion = Ranger.GetAnimalCompanion(qfRanger.Owner);
//            if (animalCompanion == null || !animalCompanion.Actions.CanTakeActions())
//                return (Possibility)null;
//            return (Possibility)(ActionPossibility)new CombatAction(qfRanger.Owner, creature.Illustration, "Command your Animal Companion", new Trait[1]
//            {
//            Trait.Auditory
//            }, "Take 2 actions as your animal companion.", (Target)Target.Self().WithAdditionalRestriction((Func<Creature, string>)(self => qfRanger.UsedThisTurn ? "You already commanded your animal companion this turn." : (string)null)))
//            {
//                ShortDescription = "Take 2 actions as your animal companion."
//            }.WithEffectOnSelf((Func<Creature, Task>)(self =>
//            {
//                Steam.CollectAchievement("RANGER");
//                qfRanger.UsedThisTurn = true;
//                await CommonSpellEffects.YourMinionActs(animalCompanion);
//            }));
//        })
//    })));
//}