﻿using System;
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
using static System.Collections.Specialized.BitVector32;

namespace Dawnsbury.Mods.Classes.Summoner {
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public static class SummonerClassEnums {
        public enum EffectKind {
            HARM,
            HEAL,
            NONE
        }

        public enum InterceptKind {
            TARGET,
            DAMAGE
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public class HPShareEffect : QEffect {
        public SummonerClassEnums.InterceptKind Type { get; set; }
        public CombatAction? LoggedAction { get; set; }
        public Creature? LoggedCreature { get; set; }
        public List<CombatAction>? ActionHistory { get; set; }
        public bool LoggedThisTurn { get; set; }
        public int HP { get; set; }
        private CombatAction ca;
        public CombatAction CA { get { return ca; } }

        public HPShareEffect(Creature owner) : base() {
            Init(owner);
        }

        public HPShareEffect(Creature owner, string name, string description) : base(name, description) {
            Init(owner);
        }

        private void Init(Creature owner) {
            this.ca = new CombatAction(owner, (Illustration)IllustrationName.ElementWater, "SummonerClass: Share HP", new Trait[0], "", new UncastableTarget());
        }

        public void Reset() {
            LoggedAction = null;
            LoggedCreature = null;
            ActionHistory = null;
            LoggedThisTurn = false;
        }

        public void LogAction(int currHP, CombatAction? action, Creature? creature, SummonerClassEnums.InterceptKind type) {
            Type = type;
            HP = currHP;
            LoggedAction = action;
            LoggedCreature = creature;
            if (creature != null) {
                ActionHistory = creature.Actions.ActionHistoryThisTurn;
            } else {
                ActionHistory = null;
            }
            LoggedThisTurn = true;
        }

        public SummonerClassEnums.EffectKind HealOrHarm(int currHP) {
            if (this.HP > currHP) {
                return SummonerClassEnums.EffectKind.HARM;
            }
            if (this.HP < currHP) {
                return SummonerClassEnums.EffectKind.HEAL;
            }
            return SummonerClassEnums.EffectKind.NONE;
        }

        public bool CompareEffects(HPShareEffect effectLog) {
            if (this.LoggedAction == effectLog.LoggedAction && this.LoggedCreature == effectLog.LoggedCreature && this.ActionHistory == effectLog.ActionHistory && this.LoggedThisTurn == effectLog.LoggedThisTurn) {
                if (this.HealOrHarm(this.Owner.HP) == effectLog.HealOrHarm(effectLog.Owner.HP)) {
                    return true;
                }
            }
            return false;
        }

        public bool CompareEffects(CombatAction action, Creature attacker) {
            if (this.LoggedAction == action && this.LoggedCreature == attacker && this.ActionHistory == attacker.Actions.ActionHistoryThisTurn && this.LoggedThisTurn == true) {
                return true;
            }
            return false;
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public class EidolonCreatureTargetingRequirement : CreatureTargetingRequirement {
        public QEffectId qfEidolon { get; }

        public EidolonCreatureTargetingRequirement(QEffectId qf) {
            this.qfEidolon = qf;
        }

        public override Usability Satisfied(Creature source, Creature target) {
            return target.QEffects.FirstOrDefault<QEffect>((Func<QEffect, bool>)(qf => qf.Id == this.qfEidolon && qf.Source == source)) != null ? Usability.Usable : Usability.CommonReasons.TargetIsNotPossibleForComplexReason;
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public static class SummonerClassLoader {
        // Traits
        private static Trait tSummoner = ModManager.RegisterTrait("SummonerClass", GenerateClassProperty(new TraitProperties("Summoner", true)));
        private static Trait tEidolon = ModManager.RegisterTrait("EidolonCompanion", new TraitProperties("Eidolon", true));
        private static Trait tAngelicEidolonArray = ModManager.RegisterTrait("AngelicEidolonArray", new TraitProperties("Eidolon Array", false));
        private static Trait tPrimaryAttackType = ModManager.RegisterTrait("EidolonPrimaryWeaponType", new TraitProperties("Eidolon Primary Weapon Type", false));
        private static Trait tPrimaryAttackStats = ModManager.RegisterTrait("EidolonPrimaryWeaponStats", new TraitProperties("Eidolon Primary Weapon Stats", false));
        private static Trait tSecondaryAttackType = ModManager.RegisterTrait("EidolonSecondaryWeaponType", new TraitProperties("Eidolon Secondary Weapon Type", false));

        // Feat names
        private static FeatName scAngelicEidolon = ModManager.RegisterFeatName("Angelic Eidolon");
        private static FeatName scAngelicEidolonAvenger = ModManager.RegisterFeatName("Angelic Avenger");
        private static FeatName scAngelicEidolonEmmissary = ModManager.RegisterFeatName("Angelic Emmisary");

        private static FeatName scDraconicEidolon = ModManager.RegisterFeatName("Draconic Eidolon");
        private static FeatName scBeastEidolon = ModManager.RegisterFeatName("Beast Eidolon");

        // Primary Weapon Feat Names
        private static FeatName ftPSword = ModManager.RegisterFeatName("P_Sword", "Sword");
        private static FeatName ftPPolearm = ModManager.RegisterFeatName("P_Polearm", "Polearm");
        private static FeatName ftPMace = ModManager.RegisterFeatName("P_Mace", "Mace");
        private static FeatName ftPWing = ModManager.RegisterFeatName("P_Wing", "Wing");
        private static FeatName ftPKick = ModManager.RegisterFeatName("P_Kick", "Kick");
        private static FeatName ftPClaw = ModManager.RegisterFeatName("P_Claw", "Claw");
        private static FeatName ftPJaws = ModManager.RegisterFeatName("P_Jaws", "Jaws");
        private static FeatName ftPFist = ModManager.RegisterFeatName("P_Fist", "Fist");
        private static FeatName ftPTendril = ModManager.RegisterFeatName("P_Tendril", "Tendril");
        private static FeatName ftPHorn = ModManager.RegisterFeatName("P_Horn", "Horn");
        private static FeatName ftPTail = ModManager.RegisterFeatName("P_Tail", "Tail");

        // Primary Weapon Statblock Feat Names
        private static FeatName ftPSPowerful = ModManager.RegisterFeatName("PS_Powerful", "Powerful");
        private static FeatName ftPSFatal = ModManager.RegisterFeatName("PS_Fatal", "Fatal");
        private static FeatName ftPSUnstoppable = ModManager.RegisterFeatName("PS_Unstoppable", "Unstoppable");
        private static FeatName ftPSGraceful = ModManager.RegisterFeatName("PS_Graceful", "Graceful");

        // Secondary Weapon Feat Names
        private static FeatName ftSWing = ModManager.RegisterFeatName("S_Wing", "Wing");
        private static FeatName ftSKick = ModManager.RegisterFeatName("S_Kick", "Kick");
        private static FeatName ftSClaw = ModManager.RegisterFeatName("S_Claw", "Claw");
        private static FeatName ftSJaws = ModManager.RegisterFeatName("S_Jaws", "Jaws");
        private static FeatName ftSFist = ModManager.RegisterFeatName("S_Fist", "Fist");
        private static FeatName ftSTendril = ModManager.RegisterFeatName("S_Tendril", "Tendril");
        private static FeatName ftSHorn = ModManager.RegisterFeatName("S_Horn", "Horn");
        private static FeatName ftSTail = ModManager.RegisterFeatName("S_Tail", "Tail");

        // QEffectIDs
        private static QEffectId qfMasterOfEidolon = ModManager.RegisterEnumMember<QEffectId>("Controller of an Eidolon");
        private static QEffectId qfSlavedEidolon = ModManager.RegisterEnumMember<QEffectId>("Summoner's Eidolon");
        private static QEffectId qfSharedActions = ModManager.RegisterEnumMember<QEffectId>("Shared Actions");


        // Spells
        private static SpellId spEvolutionSurge = ModManager.RegisterNewSpell("EvolutionSurge", 1, ((spellId, spellcaster, spellLevel, inCombat, spellInformation) => {
            return Spells.CreateModern(IllustrationName.GravityWeapon, "Evolution Surge", new[] { tSummoner, Trait.Focus, Trait.Morph, Trait.Transmutation, Trait.Uncommon },
                    "-",
                    "RULESTEXT",
                    Target.RangedFriend(20).WithAdditionalConditionOnTargetCreature((CreatureTargetingRequirement)new EidolonCreatureTargetingRequirement(qfSlavedEidolon)), spellLevel, null)
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
                            CountsAsABuff = true,
                            BonusToAllSpeeds = ((Func<QEffect, Bonus>)(_ => new Bonus(4, BonusType.Status, "Evolution Surge")))
                        });

                    }
                }));
        }));

        // Class and subclass text
        private static readonly string SummonerFlavour = "You can magically beckon a powerful being called an eidolon to your side, serving as the mortal conduit that anchors it to the world. " +
            "Whether your eidolon is a friend, a servant, or even a personal god, your connection to it marks you as extraordinary, shaping the course of your life dramatically.";
        private static readonly string SummonerCrunch =
            @"{b}1. Eidolon:{/b} etc.\n\n{b}2. Evolution Feat:{/b} etc.\n\n{b}3. Link Spells:{/b} etc.\n\n{b}4. Spontaneous Spellcasting:{/b} etc.\n\n{b}At higher levels:{/b}" +
            "\n\n{b}Level 2:{/b} Summoner feat" +
            "\n\n{b}Level 3:{/b} General feat, skill increase, level 2 spells {i}(one spell slot){/i}" +
            "\n\n{b}Level 4:{/b} Summoner feat, additional level 2 spell slot";
        private static readonly string AngelicEidolonFlavour = "Your eidolon is a celestial messenger, a member of the angelic host with a unique link to you, allowing them to carry a special message to the mortal world at your side. " +
            "Most angel eidolons are roughly humanoid in form, with feathered wings, glowing eyes, halos, or similar angelic features. However, some take the form of smaller angelic servitors like the winged helme t" +
            "cassisian angel instead. The two of you are destined for an important role in the plans of the celestial realms. Though a true angel, your angel eidolon's link to you as a mortal prevents them " +
            "from casting the angelic messenger ritual, even if they somehow learn it.";

        private static readonly string AngelicEidolonCrunch = "\n\n\u2022 {b}Tradition{/b} Divine\n\u2022 {b}Skills{/b} Diplomacy, Religion\n\u2022 Hallowed Strikes: Your Eidolon's strikes deal +1 good damage.";

        [DawnsburyDaysModMainMethod]
        public static void LoadMod() {
            AddFeats(CreateFeats());
        }

        private static void AddFeats(IEnumerable<Feat> feats) {
            foreach (Feat feat in feats) {
                ModManager.AddFeat(feat);
            }
        }

        private static IEnumerable<Feat> CreateFeats() {
            // Init subclasses
            // [Trait.Angel, Trait.Celestial, Trait.Eidolon]
            Feat angelicEidolon = new Eidolon(scAngelicEidolon, AngelicEidolonFlavour, AngelicEidolonCrunch, Trait.Divine, new List<FeatName>() { FeatName.Religion, FeatName.Diplomacy }, (List<Feat>)null)
                .WithOnSheet((Action<CalculatedCharacterSheetValues>)(values => {
                    values.AddSelectionOption((SelectionOption)new SingleFeatSelectionOption("AngelicEidolonArray", "Eidolon Array", 1, (Func<Feat, bool>)(feat => feat.HasTrait(tAngelicEidolonArray))));
                }));

            yield return CreateEidolonFeat(scAngelicEidolonAvenger, tAngelicEidolonArray, "Your eidolon is a fierce warrior of the heavens.", IllustrationName.Bat256, new int[6] { 4, 2, 3, -1, 1, 0 }, 2, 3);
            yield return CreateEidolonFeat(scAngelicEidolonEmmissary, tAngelicEidolonArray, "Your eidolon is a regal emmisary of the heavens.", IllustrationName.Bird256, new int[6] { 1, 4, 1, 0, 1, 2 }, 1, 4);

            // Init class
            yield return new ClassSelectionFeat(ModManager.RegisterFeatName("Summoner"), SummonerFlavour, tSummoner,
                new EnforcedAbilityBoost(Ability.Charisma), 10, new Trait[5] { Trait.Unarmed, Trait.Simple, Trait.UnarmoredDefense, Trait.Reflex, Trait.Perception }, new Trait[2] { Trait.Fortitude, Trait.Will }, 3, SummonerCrunch, new List<Feat>() { angelicEidolon })
                    .WithOnSheet((Action<CalculatedCharacterSheetValues>)(sheet => {
                        sheet.AddFocusSpellAndFocusPoint(tSummoner, Ability.Charisma, spEvolutionSurge);
                        sheet.AddSelectionOption(new SingleFeatSelectionOption("EidolonPrimaryWeaponStats", "Eidolon Primary Weapon Stats", 1, (Func<Feat, bool>)(ft => ft.HasTrait(tPrimaryAttackStats))));
                        sheet.AddSelectionOption(new SingleFeatSelectionOption("EidolonPrimaryWeapon", "Primary Natural Weapon", 1, (Func<Feat, bool>)(ft => ft.HasTrait(tPrimaryAttackType))));
                        sheet.AddSelectionOption(new SingleFeatSelectionOption("EidolonSecondaryWeapon", "Secondary Natural Weapon", 1, (Func<Feat, bool>)(ft => ft.HasTrait(tSecondaryAttackType))));
                    })).WithRulesBlockForSpell(spEvolutionSurge, tSummoner).WithIllustration((Illustration)IllustrationName.GravityWeapon);

            // Init Natural Attack Options
            yield return new Feat(ftPSword, "Your eidolon wields a sword, or possess a natural blade-like appendage.", "Your eidolon's primary attack deals Slashing damage.", new List<Trait> { tPrimaryAttackType, Trait.Strike }, null).WithIllustration(IllustrationName.Longsword);
            yield return new Feat(ftPPolearm, "Your eidolon wields a spear or lance, or possess a natural spear-like appendage.", "Your eidolon's primary attack deals Piercing damage.", new List<Trait> { tPrimaryAttackType, Trait.Strike }, null).WithIllustration(IllustrationName.Spear);
            yield return new Feat(ftPMace, "Your eidolon wields a mace, or possess a natural mace-like appendage.", "Your eidolon's primary attack deals Bludgeoning damage.", new List<Trait> { tPrimaryAttackType, Trait.Strike }, null).WithIllustration(IllustrationName.Warhammer);
            yield return new Feat(ftPWing, "Your eidolon knocks its enemies aside with a pair of powerful wings.", "Your eidolon's primary attack deals Bludgeoning damage.", new List<Trait> { tPrimaryAttackType, Trait.Strike }, null).WithIllustration(IllustrationName.Wing);
            yield return new Feat(ftPKick, "Your eidolon possesses a powerful kick.", "Your eidolon's primary attack deals Bludgeoning damage.", new List<Trait> { tPrimaryAttackType, Trait.Strike }, null).WithIllustration(IllustrationName.KineticRam);
            yield return new Feat(ftPClaw, "Your eidolon possesses razor sharp claws.", "Your eidolon's primary attack deals Slashing damage.", new List<Trait> { tPrimaryAttackType, Trait.Strike }, null).WithIllustration(IllustrationName.DragonClaws);
            yield return new Feat(ftPJaws, "Your eidolon possesses powerful bite attack.", "Your eidolon's primary attack deals Piercing damage.", new List<Trait> { tPrimaryAttackType, Trait.Strike }, null).WithIllustration(IllustrationName.Jaws);
            yield return new Feat(ftPFist, "Your eidolon tears or pummels its enemies apart with its bare hands.", "Your eidolon's primary attack deals Bludgeoning damage.", new List<Trait> { tPrimaryAttackType, Trait.Strike }, null).WithIllustration(IllustrationName.Fist);
            yield return new Feat(ftPTendril, "Your eidolon possesses crushing tendrils.", "Your eidolon's primary attack deals Bludgeoning damage.", new List<Trait> { tPrimaryAttackType, Trait.Strike }, null).WithIllustration(IllustrationName.Tentacle);
            yield return new Feat(ftPHorn, "Your eidolon possesses vicious horns to gore its enemies.", "Your eidolon's primary attack deals Piercing damage.", new List<Trait> { tPrimaryAttackType, Trait.Strike }, null).WithIllustration(IllustrationName.Horn);
            yield return new Feat(ftPTail, "Your eidolon possesses deadly stinging tail.", "Your eidolon's primary attack deals Piercing damage.", new List<Trait> { tPrimaryAttackType, Trait.Strike }, null).WithIllustration(IllustrationName.Tail);

            yield return new Feat(ftSWing, "Your eidolon knocks its enemies aside with a pair of powerful wings.", "Your eidolon's secondary attack deals 1d6 bludgeoning damage, with the agile and finesse traits." +
                "\n\n{b}" + Trait.Agile.GetTraitProperties().HumanizedName + "{/b} " + Trait.Agile.GetTraitProperties().RulesText +
                "\n{b}" + Trait.Finesse.GetTraitProperties().HumanizedName + "{/b} " + Trait.Finesse.GetTraitProperties().RulesText,
                new List<Trait> { tSecondaryAttackType, Trait.Strike }, null).WithIllustration(IllustrationName.Wing);
            yield return new Feat(ftSKick, "Your eidolon possesses a powerful kick.", "Your eidolon's secondary attack deals 1d6 bludgeoning damage, with the agile and finesse traits." +
                "\n\n{b}" + Trait.Agile.GetTraitProperties().HumanizedName + "{/b} " + Trait.Agile.GetTraitProperties().RulesText +
                "\n{b}" + Trait.Finesse.GetTraitProperties().HumanizedName + "{/b} " + Trait.Finesse.GetTraitProperties().RulesText,
                new List<Trait> { tSecondaryAttackType, Trait.Strike }, null).WithIllustration(IllustrationName.KineticRam);
            yield return new Feat(ftSClaw, "Your eidolon possesses razor sharp claws.", "Your eidolon's secondary attack deals 1d6 slashing damage, with the agile and finesse traits." +
                "\n\n{b}" + Trait.Agile.GetTraitProperties().HumanizedName + "{/b} " + Trait.Agile.GetTraitProperties().RulesText +
                "\n{b}" + Trait.Finesse.GetTraitProperties().HumanizedName + "{/b} " + Trait.Finesse.GetTraitProperties().RulesText,
                new List<Trait> { tSecondaryAttackType, Trait.Strike }, null).WithIllustration(IllustrationName.DragonClaws);
            yield return new Feat(ftSJaws, "Your eidolon possesses powerful bite attack.", "Your eidolon's secondary attack deals 1d6 piercing damage, with the agile and finesse traits." +
                "\n\n{b}" + Trait.Agile.GetTraitProperties().HumanizedName + "{/b} " + Trait.Agile.GetTraitProperties().RulesText +
                "\n{b}" + Trait.Finesse.GetTraitProperties().HumanizedName + "{/b} " + Trait.Finesse.GetTraitProperties().RulesText,
                new List<Trait> { tSecondaryAttackType, Trait.Strike }, null).WithIllustration(IllustrationName.Jaws);
            yield return new Feat(ftSFist, "Your eidolon tears or pummels its enemies apart with its bare hands.", "Your eidolon's secondary attack deals 1d6 bludgeoning damage, with the agile and finesse traits." +
                "\n\n{b}" + Trait.Agile.GetTraitProperties().HumanizedName + "{/b} " + Trait.Agile.GetTraitProperties().RulesText +
                "\n{b}" + Trait.Finesse.GetTraitProperties().HumanizedName + "{/b} " + Trait.Finesse.GetTraitProperties().RulesText,
                new List<Trait> { tSecondaryAttackType, Trait.Strike }, null).WithIllustration(IllustrationName.Fist);
            yield return new Feat(ftSTendril, "Your eidolon possesses crushing tendrils.", "Your eidolon's secondary attack deals 1d6 bludgeoning damage, with the agile and finesse traits." +
                "\n\n{b}" + Trait.Agile.GetTraitProperties().HumanizedName + "{/b} " + Trait.Agile.GetTraitProperties().RulesText +
                "\n{b}" + Trait.Finesse.GetTraitProperties().HumanizedName + "{/b} " + Trait.Finesse.GetTraitProperties().RulesText,
                new List<Trait> { tSecondaryAttackType, Trait.Strike }, null).WithIllustration(IllustrationName.Tentacle);
            yield return new Feat(ftSHorn, "Your eidolon possesses vicious horns to gore its enemies.", "Your eidolon's secondary attack deals 1d6 piercing damage, with the agile and finesse traits." +
                "\n\n{b}" + Trait.Agile.GetTraitProperties().HumanizedName + "{/b} " + Trait.Agile.GetTraitProperties().RulesText +
                "\n{b}" + Trait.Finesse.GetTraitProperties().HumanizedName + "{/b} " + Trait.Finesse.GetTraitProperties().RulesText,
                new List<Trait> { tSecondaryAttackType, Trait.Strike }, null).WithIllustration(IllustrationName.Horn);
            yield return new Feat(ftSTail, "Your eidolon possesses deadly stinging tail.", "Your eidolon's secondary attack deals 1d6 piercing damage, with the agile and finesse traits." +
                "\n\n{b}" + Trait.Agile.GetTraitProperties().HumanizedName + "{/b} " + Trait.Agile.GetTraitProperties().RulesText +
                "\n{b}" + Trait.Finesse.GetTraitProperties().HumanizedName + "{/b} " + Trait.Finesse.GetTraitProperties().RulesText,
                new List<Trait> { tSecondaryAttackType, Trait.Strike }, null).WithIllustration(IllustrationName.Tail);

            // Init Primary Weapon Properties
            yield return new Feat(ftPSPowerful, "Your eidolon possesses great strength, allowing it to easily bully and subdue its enemies.", "Your eidolon's primary deals 1d8 damage, and has the disarm, nonlethal, shove and trip traits. Athletics checks made using a weapon with a maneovre trait benefit from the item bonus on your handwraps of mighty blows." +
                "\n\n{b}" + Trait.Disarm.GetTraitProperties().HumanizedName + "{/b} " + Trait.Disarm.GetTraitProperties().RulesText +
                "\n{b}" + Trait.Nonlethal.GetTraitProperties().HumanizedName + "{/b} " + Trait.Nonlethal.GetTraitProperties().RulesText +
                "\n{b}" + Trait.Disarm.GetTraitProperties().HumanizedName + "{/b} " + Trait.Disarm.GetTraitProperties().RulesText +
                "\n{b}" + Trait.Trip.GetTraitProperties().HumanizedName + "{/b} " + Trait.Trip.GetTraitProperties().RulesText,
                new List<Trait> { tPrimaryAttackStats, Trait.Strike, Trait.Disarm, Trait.Nonlethal, Trait.Shove, Trait.Trip }, null);
            yield return new Feat(ftPSFatal, "Your eidolon waits patiently for the perfect opportunity before closing in on its foes.", "Your eidolon's primary attack deals 1d6 damage, and has the fatal d10 traits." +
                "\n\n{b}" + Trait.FatalD10.GetTraitProperties().HumanizedName + "{/b} " + Trait.FatalD10.GetTraitProperties().RulesText,
                new List<Trait> { tPrimaryAttackStats, Trait.Strike, Trait.FatalD10 }, null);
            yield return new Feat(ftPSUnstoppable, "Your eidolon's attacks pick up speed and momentum as it fights.", "Your eidolon's primary attack deals 1d6 damage, and has the forceful and sweep traits." +
                "\n\n{b}" + Trait.Forceful.GetTraitProperties().HumanizedName + "{/b} " + Trait.Forceful.GetTraitProperties().RulesText +
                "\n{b}" + Trait.Sweep.GetTraitProperties().HumanizedName + "{/b} " + Trait.Sweep.GetTraitProperties().RulesText,
                new List<Trait> { tPrimaryAttackStats, Trait.Strike, Trait.Forceful, Trait.Sweep }, null);
            yield return new Feat(ftPSGraceful, "Your eidolon possesses dexterous and opportunisitic natural weapons.", "Your eidolon's primary attack deals 1d6 damage, and has the deadly d8 and finesse traits." +
                "\n\n{b}" + Trait.DeadlyD8.GetTraitProperties().HumanizedName + "{/b} "+ Trait.DeadlyD8.GetTraitProperties().RulesText +
                "\n{b}" + Trait.Finesse.GetTraitProperties().HumanizedName + "{/b} " + Trait.Finesse.GetTraitProperties().RulesText,
                new List<Trait> { tPrimaryAttackStats, Trait.Strike, Trait.DeadlyD8, Trait.Finesse }, null);
        }

        private static Creature? GetSummoner(Creature eidolon) {
            return eidolon.QEffects.FirstOrDefault<QEffect>((Func<QEffect, bool>)(qf => qf.Id == qfSlavedEidolon))?.Source;
        }

        private static Creature? GetEidolon(Creature summoner) {
            return summoner.QEffects.FirstOrDefault<QEffect>((Func<QEffect, bool>)(qf => qf.Id == qfMasterOfEidolon))?.Source;
        }

        private static TraitProperties GenerateClassProperty(TraitProperties property) {
            property.IsClassTrait = true;
            return property;
        }


        public class Eidolon : Feat {
            public Eidolon(FeatName featName, string flavorText, string rulesText, Trait spellList, List<FeatName> skills, List<Feat> subfeats) : base(featName, flavorText, rulesText, new List<Trait>(), subfeats) {
                this.OnSheet = (Action<CalculatedCharacterSheetValues>)(sheet => {
                    sheet.SpellTraditionsKnown.Add(spellList);
                    sheet.SpellRepertoires.Add(tSummoner, new SpellRepertoire(Ability.Charisma, spellList));
                    foreach (FeatName skill in skills) {
                        sheet.GrantFeat(skill);
                        //sheet.SetProficiency(skill, Proficiency.Trained);
                    }
                    SpellRepertoire repertoire = sheet.SpellRepertoires[tSummoner];
                    sheet.AddSelectionOption((SelectionOption)new AddToSpellRepertoireOption("SummonerCantrips", "Cantrips", 1, tSummoner, spellList, 0, 5));
                    sheet.AddSelectionOption((SelectionOption)new AddToSpellRepertoireOption("SummonerSpells", "Level 1 spells", 1, tSummoner, spellList, 1, 2));
                    sheet.AddSelectionOption((SelectionOption)new AddToSpellRepertoireOption("SummonerSpells2", "Level 1 spell", 2, tSummoner, spellList, 1, 1));
                    sheet.AddSelectionOption((SelectionOption)new AddToSpellRepertoireOption("SummonerSpells3", "Level 2 spells", 3, tSummoner, spellList, 2, 1));
                    sheet.AddSelectionOption((SelectionOption)new AddToSpellRepertoireOption("SummonerSpells4", "Level 2 spell", 4, tSummoner, spellList, 2, 1));
                    repertoire.SpellSlots[1] = 1;
                    sheet.AddAtLevel(2, (Action<CalculatedCharacterSheetValues>)(_ => ++repertoire.SpellSlots[1]));
                    sheet.AddAtLevel(3, (Action<CalculatedCharacterSheetValues>)(_ => ++repertoire.SpellSlots[2]));
                    sheet.AddAtLevel(4, (Action<CalculatedCharacterSheetValues>)(_ => ++repertoire.SpellSlots[2]));
                });
            }
        }

        private static Feat CreateEidolonFeat(FeatName featName, Trait trait, string flavorText, IllustrationName token, int[] abilityScores, int ac, int dexCap) {
            return new Feat(featName, flavorText, "rules text", new List<Trait>() { trait }, (List<Feat>)null).WithIllustration(token).WithOnCreature((Action<CalculatedCharacterSheetValues, Creature>)((sheet, summoner) => summoner.AddQEffect(new QEffect("Eidolon", "This character can summon and command an Eidolon.") {
                StartOfCombat = (Func<QEffect, Task>)(async qfSummonerTechnical => {
                    Creature eidolon = CreateEidolon(featName, token, abilityScores, ac, dexCap, summoner);
                    eidolon.MainName = qfSummonerTechnical.Owner.Name + "'s " + eidolon.MainName;
                    eidolon.AddQEffect(new HPShareEffect(eidolon) {
                        Id = qfSlavedEidolon,
                        Source = summoner
                    });
                    summoner.AddQEffect(new HPShareEffect(summoner) {
                        Id = qfMasterOfEidolon,
                        Source = eidolon
                    });
                    // Share beenfits of handwraps
                    Item handwraps = StrikeRules.GetBestHandwraps(summoner);
                    if (handwraps != null) {
                        Item eidolonHandwraps = handwraps.Duplicate();
                        eidolon.CarriedItems.Add(eidolonHandwraps);
                        eidolonHandwraps.IsWorn = true;
                        //eidolon.CarriedItems.Find(item => item == eidolonHandwraps);
                    }
                    summoner.Battle.SpawnCreature(eidolon, summoner.OwningFaction, summoner.Occupies);
                }),
                StartOfYourTurn = (Func<QEffect, Creature, Task>)(async (qfStartOfTurn, summoner) => {
                    Creature eidolon = GetEidolon(summoner);
                    await (Task)eidolon.Battle.GameLoop.GetType().GetMethod("StartOfTurn", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static).Invoke(eidolon.Battle.GameLoop, new object[] { eidolon });

                    await eidolon.Battle.GameLoop.StateCheck();
                }),
                EndOfYourTurn = (Func<QEffect, Creature, Task>)(async (qfEndOfTurn, summoner) => {
                    Creature eidolon = GetEidolon(summoner);
                    await eidolon.Battle.GameLoop.EndOfTurn(eidolon);
                }),
                ProvideMainAction = (Func<QEffect, Possibility>)(qfSummoner => {
                    Creature? eidolon = GetEidolon(qfSummoner.Owner);
                    if (eidolon == null || !eidolon.Actions.CanTakeActions())
                        return (Possibility)null;
                    return (Possibility)(ActionPossibility)new CombatAction(qfSummoner.Owner, eidolon.Illustration, "Command your Eidolon", new Trait[1] { Trait.Basic }, "Swap to Eidolon.", (Target)Target.Self()) {
                        ShortDescription = "Take control of your Eidolon, using your shared action pool."
                    }.WithEffectOnSelf((Func<Creature, Task>)(async self => {
                        await EidolonActs(summoner, eidolon);
                    }))
                    .WithActionCost(0);
                }),
                YouAreTargeted = (Func<QEffect, CombatAction, Task>)(async (qfHealOrHarm, action) => {
                    if (action.Name == "Command your Eidolon") {
                        return;
                    }

                    if (GetEidolon(qfHealOrHarm.Owner) != null && GetEidolon(qfHealOrHarm.Owner).Destroyed) {
                        return;
                    }

                    HPShareEffect shareHP = (HPShareEffect)qfHealOrHarm.Owner.QEffects.FirstOrDefault<QEffect>((Func<QEffect, bool>)(qf => qf.Id == qfMasterOfEidolon));
                    shareHP.LogAction(qfHealOrHarm.Owner.HP, action, action.Owner, SummonerClassEnums.InterceptKind.TARGET);
                }),
                AfterYouAreTargeted = (Func<QEffect, CombatAction, Task>)(async (qfShareHP, action) => {
                    if (action.Name == "Command your Eidolon") {
                        return;
                    }

                    if (GetEidolon(qfShareHP.Owner) != null && GetEidolon(qfShareHP.Owner).Destroyed) {
                        return;
                    }

                    Creature summoner = qfShareHP.Owner;
                    Creature eidolon = GetEidolon(summoner);

                    HPShareEffect summonerShareHP = (HPShareEffect)summoner.QEffects.FirstOrDefault<QEffect>((Func<QEffect, bool>)(qf => qf.Id == qfMasterOfEidolon));
                    HPShareEffect eidolonShareHP = (HPShareEffect)eidolon.QEffects.FirstOrDefault<QEffect>((Func<QEffect, bool>)(qf => qf.Id == qfSlavedEidolon && qf.Source == qfShareHP.Owner));

                    if (summonerShareHP.CompareEffects(eidolonShareHP)) {
                        // Same effect
                        if (summonerShareHP.HealOrHarm(summoner.HP) == SummonerClassEnums.EffectKind.HARM) {
                            if (eidolon.HP < summoner.HP) {
                                // Deal difference to summoner
                                int damage = summoner.HP - eidolon.HP;
                                await eidolon.DealDirectDamage(eidolonShareHP.CA, DiceFormula.FromText($"{damage}"), summoner, CheckResult.Success, DamageKind.Untyped);
                            } else if (eidolon.HP > summoner.HP) {
                                // Deal difference to eidolon
                                int damage = eidolon.HP - summoner.HP;
                                await summoner.DealDirectDamage(eidolonShareHP.CA, DiceFormula.FromText($"{damage}"), eidolon, CheckResult.Success, DamageKind.Untyped);
                            }
                        } else if (summonerShareHP.HealOrHarm(summoner.HP) == SummonerClassEnums.EffectKind.HEAL) {
                            if (eidolon.HP < summoner.HP) {
                                // Heal for  difference to summoner
                                int healing = summoner.HP - eidolon.HP;
                                summoner.Heal($"{healing}", eidolonShareHP.CA);
                            } else if (eidolon.HP > summoner.HP) {
                                // Heal for  difference to eidolon
                                int healing = eidolon.HP - summoner.HP;
                                eidolon.Heal($"{healing}", summonerShareHP.CA);
                            }
                        }
                    } else {
                        // Invividual effect
                        if (summonerShareHP.HealOrHarm(summoner.HP) == SummonerClassEnums.EffectKind.HARM) {
                            int damage = summonerShareHP.HP - summoner.HP;
                            await summoner.DealDirectDamage(eidolonShareHP.CA, DiceFormula.FromText($"{damage}"), eidolon, CheckResult.Success, DamageKind.Untyped);
                        } else if (summonerShareHP.HealOrHarm(summoner.HP) == SummonerClassEnums.EffectKind.HEAL) {
                            int healing = summoner.HP - summonerShareHP.HP;
                            eidolon.Heal($"{healing}", eidolonShareHP.CA);
                        }
                    }
                }),
                EndOfAnyTurn = (Action<QEffect>)(qfHealOrHarm => {
                    HPShareEffect shareHP = (HPShareEffect)qfHealOrHarm.Owner.QEffects.FirstOrDefault<QEffect>((Func<QEffect, bool>)(qf => qf.Id == qfMasterOfEidolon));
                    if (shareHP != null) {
                        shareHP.Reset();
                    }
                }),
                YouAreDealtDamage = (Func<QEffect, Creature, DamageStuff, Creature, Task<DamageModification?>>)(async (qfPreHazardDamage, attacker, damageStuff, defender) => {
                    if (GetEidolon(qfPreHazardDamage.Owner) != null && GetEidolon(qfPreHazardDamage.Owner).Destroyed) {
                        return null;
                    }

                    // Check if effect is coming from self
                    if (damageStuff.Power.Name == "SummonerClass: Share HP") {
                        return null;
                    }

                    HPShareEffect shareHP = (HPShareEffect)qfPreHazardDamage.Owner.QEffects.FirstOrDefault<QEffect>((Func<QEffect, bool>)(qf => qf.Id == qfMasterOfEidolon));

                    // Check if caught by target check
                    if (shareHP.CompareEffects(damageStuff.Power, attacker)) {
                        return null;
                    }

                    shareHP.LogAction(qfPreHazardDamage.Owner.HP, damageStuff.Power, attacker, SummonerClassEnums.InterceptKind.DAMAGE);
                    return null;
                }),
                AfterYouTakeDamageOfKind = (Func<QEffect, CombatAction?, DamageKind, Task>)(async (qfPostHazardDamage, action, kind) => {
                    if (GetEidolon(qfPostHazardDamage.Owner) != null && GetEidolon(qfPostHazardDamage.Owner).Destroyed) {
                        return;
                    }

                    // Check if effect is coming from self
                    if (action.Name == "SummonerClass: Share HP") {
                        return;
                    }

                    Creature summoner = qfPostHazardDamage.Owner;
                    Creature eidolon = GetEidolon(summoner);

                    HPShareEffect summonerShareHP = (HPShareEffect)summoner.QEffects.FirstOrDefault<QEffect>((Func<QEffect, bool>)(qf => qf.Id == qfMasterOfEidolon));
                    HPShareEffect eidolonShareHP = (HPShareEffect)eidolon.QEffects.FirstOrDefault<QEffect>((Func<QEffect, bool>)(qf => qf.Id == qfSlavedEidolon && qf.Source == qfPostHazardDamage.Owner));

                    if (summonerShareHP.Type != SummonerClassEnums.InterceptKind.DAMAGE) {
                        return;
                    }

                    if (summonerShareHP.CompareEffects(eidolonShareHP)) {
                        // Same effect
                        if (summonerShareHP.HealOrHarm(summoner.HP) == SummonerClassEnums.EffectKind.HARM) {
                            if (eidolon.HP < summoner.HP) {
                                // Deal difference to summoner
                                int damage = summoner.HP - eidolon.HP;
                                await eidolon.DealDirectDamage(summonerShareHP.CA, DiceFormula.FromText($"{damage}"), summoner, CheckResult.Success, DamageKind.Untyped);
                            } else if (eidolon.HP > summoner.HP) {
                                // Deal difference to eidolon
                                int damage = eidolon.HP - summoner.HP;
                                await summoner.DealDirectDamage(summonerShareHP.CA, DiceFormula.FromText($"{damage}"), eidolon, CheckResult.Success, DamageKind.Untyped);
                            }
                        } else if (summonerShareHP.HealOrHarm(summoner.HP) == SummonerClassEnums.EffectKind.HEAL) {
                            if (eidolon.HP < summoner.HP) {
                                // Heal for  difference to summoner
                                int healing = summoner.HP - eidolon.HP;
                                summoner.Heal($"{healing}", eidolonShareHP.CA);
                            } else if (eidolon.HP > summoner.HP) {
                                // Heal for  difference to eidolon
                                int healing = eidolon.HP - summoner.HP;
                                eidolon.Heal($"{healing}", summonerShareHP.CA);
                            }
                        }
                    } else {
                        // Invividual effect
                        if (summonerShareHP.HealOrHarm(summoner.HP) == SummonerClassEnums.EffectKind.HARM) {
                            int damage = summonerShareHP.HP - summoner.HP;
                            await summoner.DealDirectDamage(summonerShareHP.CA, DiceFormula.FromText($"{damage}"), eidolon, CheckResult.Success, DamageKind.Untyped);
                        } else if (summonerShareHP.HealOrHarm(summoner.HP) == SummonerClassEnums.EffectKind.HEAL) {
                            int healing = summoner.HP - summonerShareHP.HP;
                            eidolon.Heal($"{healing}", summonerShareHP.CA);
                        }
                    }
                }),
                AfterYouTakeAction = (Func<QEffect, CombatAction, Task>)(async (qfSharedActions, action) => {
                    Creature summoner = qfSharedActions.Owner;
                    Creature eidolon = GetEidolon(summoner);
                    //eidolon.Actions.UseUpActions(action.ActionCost, )
                    if (eidolon != null && action.Traits.Contains(Trait.Attack)) {
                        eidolon.Actions.AttackedThisManyTimesThisTurn += 1;
                    }
                })
            })));
        }

        //++combatActionExecution.user.Actions.AttackedThisManyTimesThisTurn

        private static Creature CreateEidolon(FeatName featName, IllustrationName token, int[] abilityScores, int ac, int dexCap, Creature summoner) {
            Creature eidolon = CreateEidolonBase(token, "Eidolon", summoner, abilityScores, ac, dexCap);

            //.WithUnarmedStrike(new Item((Illustration)IllustrationName.Jaws, "jaws", new Trait[2] {
            //    Trait.Unarmed,
            //    Trait.Finesse}).WithWeaponProperties(new WeaponProperties("1d6", DamageKind.Piercing))).WithAdditionalUnarmedStrike(CommonItems.CreateNaturalWeapon(IllustrationName.Wing, "wing", "1d4", DamageKind.Slashing, Trait.Agile, Trait.Finesse));

            // Generate natural weapon attacks
            Feat pAttack = summoner.PersistentCharacterSheet.Calculated.AllFeats.FirstOrDefault((Func<Feat, bool>)(ft => ft.HasTrait(tPrimaryAttackType)));
            Feat sAttack = summoner.PersistentCharacterSheet.Calculated.AllFeats.FirstOrDefault((Func<Feat, bool>)(ft => ft.HasTrait(tSecondaryAttackType)));
            Feat pStatsFeat = summoner.PersistentCharacterSheet.Calculated.AllFeats.FirstOrDefault((Func<Feat, bool>)(ft => ft.HasTrait(tPrimaryAttackStats)));
            List<Trait> pStats = new List<Trait>() { Trait.Unarmed };
            for (int i = 2; i < pStatsFeat.Traits.Count ; i++) {
                pStats.Add(pStatsFeat.Traits[i]);
            }

            DamageKind primaryDamageType;
            if (new FeatName[] { ftPMace, ftPWing, ftPKick, ftPFist, ftPTendril }.Contains(pAttack.FeatName)) {
                primaryDamageType = DamageKind.Bludgeoning;
            } else if (new FeatName[] { ftPPolearm, ftPHorn, ftPTail }.Contains(pAttack.FeatName)) {
                primaryDamageType = DamageKind.Piercing;
            } else {
                primaryDamageType = DamageKind.Slashing;
            }

            DamageKind secondaryDamageType;
            if (new FeatName[] { ftSWing, ftSKick, ftSFist, ftSTendril }.Contains(pAttack.FeatName)) {
                secondaryDamageType = DamageKind.Bludgeoning;
            } else if (new FeatName[] { ftSHorn, ftSTail }.Contains(pAttack.FeatName)) {
                secondaryDamageType = DamageKind.Piercing;
            } else {
                secondaryDamageType = DamageKind.Slashing;
            }

            string damage = "1d6";
            if (pStatsFeat.FeatName == ftPSPowerful) {
                damage = "1d8";
            }

            // #TODO: Map these to feat names
            Illustration pIcon = pAttack.Illustration;
            Illustration sIcon = sAttack.Illustration;

            Illustration test = IllustrationName.Tail;

            eidolon.WithUnarmedStrike(new Item(pIcon, pAttack.CustomName, pStats.ToArray()).WithWeaponProperties(new WeaponProperties(damage, primaryDamageType)));
            eidolon.WithAdditionalUnarmedStrike(new Item(sIcon, sAttack.CustomName, new Trait[] { Trait.Unarmed, Trait.Agile, Trait.Finesse }).WithWeaponProperties(new WeaponProperties("1d6", secondaryDamageType)));

            //if (featName == scAngelicEidolon) {

            //} else {
            //    throw new Exception("Unknown animal companion.");
            //}

            eidolon.PostConstructorInitialization(TBattle.Pseudobattle);
            return eidolon;
        }

        private static Creature CreateEidolonBase(IllustrationName illustration, string name, Creature summoner, int[] abilityScores, int ac, int dexCap) {
            int strength = abilityScores[0];
            int dexterity = abilityScores[1];
            int constitution = abilityScores[2];
            int intelligence = abilityScores[3];
            int wisdom = abilityScores[4];
            int charisma = abilityScores[5];
            int level = summoner.Level;
            int num = 2 + level;
            Abilities abilities1 = new Abilities(strength, dexterity, constitution, intelligence, wisdom, charisma);
            Illustration illustration1 = (Illustration)illustration;
            string name1 = name;
            List<Trait> traits = new List<Trait>();
            traits.Add(tEidolon);
            int perception = wisdom + num + level;
            int speed1 = 5;
            Defenses defenses = new Defenses(10 + ac + (dexterity < dexCap ? dexterity : dexCap) + num, constitution + num + num, dexterity + num, wisdom + num + num);
            int hp = summoner.MaxHP;
            Abilities abilities2 = abilities1;
            return new Creature(illustration1, name1, (IList<Trait>)traits, level, perception, speed1, defenses, hp, abilities2, new Skills()).WithProficiency(Trait.Unarmed, Proficiency.Trained).WithEntersInitiativeOrder(false).WithProficiency(Trait.UnarmoredDefense, Proficiency.Trained)
                .AddQEffect(new QEffect() {
                    ProvideMainAction = (Func<QEffect, Possibility>)(qfEidolon => {
                        Creature? summoner = GetSummoner(qfEidolon.Owner);
                        if (summoner == null || !summoner.Actions.CanTakeActions())
                            return (Possibility)null;
                        return (Possibility)(ActionPossibility)new CombatAction(qfEidolon.Owner, summoner.Illustration, "Return Control",
                            new Trait[1] { Trait.Basic }, $"Switch back to controlling {summoner.Name}. All unspent actions will be retained.", (Target)Target.Self((Func<Creature, AI, float>)((cr, ai) =>
                            (float)int.MinValue))).WithActionCost(0).WithActionId(ActionId.EndTurn).WithEffectOnSelf((Action<Creature>)(a => a.Actions.WishesToEndTurn = true));
                    }),
                    YouAcquireQEffect = (Func<QEffect, QEffect, QEffect?>)((qfVanishOnDeath, qfNew) => {
                        if (qfNew.Id == QEffectId.Dying) {
                            qfVanishOnDeath.Owner.Battle.RemoveCreatureFromGame(qfVanishOnDeath.Owner);
                            return null;
                        }
                        return qfNew;
                    }),
                    EndOfCombat = (Func<QEffect, bool, Task>)(async (qfRemoveHandwraps, won) => {
                        Item? handwraps = qfRemoveHandwraps.Owner.CarriedItems.FirstOrDefault<Item>((Func<Item, bool>)(backpackItem => backpackItem.ItemName == ItemName.HandwrapsOfMightyBlows && backpackItem.IsWorn));
                        if (handwraps != null) {
                            qfRemoveHandwraps.Owner.CarriedItems.Remove(handwraps);
                        }
                    }),
                    YouAreTargeted = (Func<QEffect, CombatAction, Task>)(async (qfHealOrHarm, action) => {
                        HPShareEffect shareHP = (HPShareEffect)qfHealOrHarm.Owner.QEffects.FirstOrDefault<QEffect>((Func<QEffect, bool>)(qf => qf.Id == qfSlavedEidolon));
                        shareHP.LogAction(qfHealOrHarm.Owner.HP, action, action.Owner, SummonerClassEnums.InterceptKind.TARGET);
                    }),
                    AfterYouAreTargeted = (Func<QEffect, CombatAction, Task>)(async (qfShareHP, action) => {
                        Creature summoner = GetSummoner(qfShareHP.Owner);
                        Creature eidolon = qfShareHP.Owner;

                        HPShareEffect summonerShareHP = (HPShareEffect)summoner.QEffects.FirstOrDefault<QEffect>((Func<QEffect, bool>)(qf => qf.Id == qfMasterOfEidolon && qf.Source == qfShareHP.Owner));
                        HPShareEffect eidolonShareHP = (HPShareEffect)eidolon.QEffects.FirstOrDefault<QEffect>((Func<QEffect, bool>)(qf => qf.Id == qfSlavedEidolon));

                        if (eidolonShareHP.CompareEffects(summonerShareHP)) {
                            // Same effect
                            if (summonerShareHP.HealOrHarm(summoner.HP) == SummonerClassEnums.EffectKind.HARM) {
                                if (eidolon.HP < summoner.HP) {
                                    // Deal difference to summoner
                                    int damage = summoner.HP - eidolon.HP;
                                    await eidolon.DealDirectDamage(eidolonShareHP.CA, DiceFormula.FromText($"{damage}"), summoner, CheckResult.Success, DamageKind.Untyped);
                                } else if (eidolon.HP > summoner.HP) {
                                    // Deal difference to eidolon
                                    int damage = eidolon.HP - summoner.HP;
                                    await summoner.DealDirectDamage(summonerShareHP.CA, DiceFormula.FromText($"{damage}"), eidolon, CheckResult.Success, DamageKind.Untyped);
                                }
                            } else if (eidolonShareHP.HealOrHarm(summoner.HP) == SummonerClassEnums.EffectKind.HEAL) {
                                if (eidolon.HP < summoner.HP) {
                                    // Heal for  difference to summoner
                                    int healing = summoner.HP - eidolon.HP;
                                    summoner.Heal($"{healing}", eidolonShareHP.CA);
                                } else if (eidolon.HP > summoner.HP) {
                                    // Heal for  difference to eidolon
                                    int healing = eidolon.HP - summoner.HP;
                                    eidolon.Heal($"{healing}", summonerShareHP.CA);
                                }
                            }
                        } else {
                            // Invividual effect
                            if (eidolonShareHP.HealOrHarm(eidolon.HP) == SummonerClassEnums.EffectKind.HARM) {
                                int damage = eidolonShareHP.HP - eidolon.HP;
                                await eidolon.DealDirectDamage(eidolonShareHP.CA, DiceFormula.FromText($"{damage}"), summoner, CheckResult.Success, DamageKind.Untyped);
                            } else if (eidolonShareHP.HealOrHarm(eidolon.HP) == SummonerClassEnums.EffectKind.HEAL) {
                                int healing = eidolon.HP - eidolonShareHP.HP;
                                summoner.Heal($"{healing}", eidolonShareHP.CA);
                            }
                        }
                    }),
                    EndOfAnyTurn = (Action<QEffect>)(qfEndOfTurn => {
                        HPShareEffect shareHP = (HPShareEffect)qfEndOfTurn.Owner.QEffects.FirstOrDefault<QEffect>((Func<QEffect, bool>)(qf => qf.Id == qfSlavedEidolon));
                        if (shareHP != null) {
                            shareHP.Reset();
                        }

                        Creature summoner = GetSummoner(qfEndOfTurn.Owner);
                        Creature eidolon = qfEndOfTurn.Owner;

                        // Catch unhandled hazard healing effects
                        if (summoner.HP > eidolon.HP) {
                            int healing = summoner.HP - eidolon.HP;
                            eidolon.Heal($"{healing}", shareHP.CA);
                        } else if (summoner.HP < eidolon.HP) {
                            int healing = eidolon.HP - summoner.HP;
                            summoner.Heal($"{healing}", shareHP.CA);
                        }
                    }),
                    YouAreDealtDamage = (Func<QEffect, Creature, DamageStuff, Creature, Task<DamageModification?>>)(async (qfPreHazardDamage, attacker, damageStuff, defender) => {
                        // Check if effect is coming from self
                        if (damageStuff.Power.Name == "SummonerClass: Share HP") {
                            return null;
                        }

                        HPShareEffect shareHP = (HPShareEffect)qfPreHazardDamage.Owner.QEffects.FirstOrDefault<QEffect>((Func<QEffect, bool>)(qf => qf.Id == qfSlavedEidolon));

                        // Check if caught by target check
                        if (shareHP.CompareEffects(damageStuff.Power, attacker)) {
                            return null;
                        }

                        shareHP.LogAction(qfPreHazardDamage.Owner.HP, damageStuff.Power, attacker, SummonerClassEnums.InterceptKind.DAMAGE);
                        return null;

                        //if (attacker == qfPreHazardDamage.Owner.Battle.Pseudocreature) {
                        //    qfPreHazardDamage.Owner.Battle.Log("{b}HAZARD DAMAGE LOGGED{/b}");
                        //}
                        //return null;
                    }),
                    AfterYouTakeDamageOfKind = (Func<QEffect, CombatAction?, DamageKind, Task>)(async (qfPostHazardDamage, action, kind) => {
                        // Check if effect is coming from self
                        if (action.Name == "SummonerClass: Share HP") {
                            return;
                        }

                        Creature summoner = GetSummoner(qfPostHazardDamage.Owner);
                        Creature eidolon = qfPostHazardDamage.Owner;

                        HPShareEffect summonerShareHP = (HPShareEffect)summoner.QEffects.FirstOrDefault<QEffect>((Func<QEffect, bool>)(qf => qf.Id == qfMasterOfEidolon && qf.Source == qfPostHazardDamage.Owner));
                        HPShareEffect eidolonShareHP = (HPShareEffect)eidolon.QEffects.FirstOrDefault<QEffect>((Func<QEffect, bool>)(qf => qf.Id == qfSlavedEidolon));

                        if (eidolonShareHP.Type != SummonerClassEnums.InterceptKind.DAMAGE) {
                            return;
                        }


                        if (eidolonShareHP.CompareEffects(summonerShareHP)) {
                            // Same effect
                            if (summonerShareHP.HealOrHarm(summoner.HP) == SummonerClassEnums.EffectKind.HARM) {
                                if (eidolon.HP < summoner.HP) {
                                    // Deal difference to summoner
                                    int damage = summoner.HP - eidolon.HP;
                                    await eidolon.DealDirectDamage(eidolonShareHP.CA, DiceFormula.FromText($"{damage}"), summoner, CheckResult.Success, DamageKind.Untyped);
                                } else if (eidolon.HP > summoner.HP) {
                                    // Deal difference to eidolon
                                    int damage = eidolon.HP - summoner.HP;
                                    await summoner.DealDirectDamage(summonerShareHP.CA, DiceFormula.FromText($"{damage}"), eidolon, CheckResult.Success, DamageKind.Untyped);
                                }
                            } else if (eidolonShareHP.HealOrHarm(summoner.HP) == SummonerClassEnums.EffectKind.HEAL) {
                                if (eidolon.HP < summoner.HP) {
                                    // Heal for  difference to summoner
                                    int healing = summoner.HP - eidolon.HP;
                                    summoner.Heal($"{healing}", eidolonShareHP.CA);
                                } else if (eidolon.HP > summoner.HP) {
                                    // Heal for  difference to eidolon
                                    int healing = eidolon.HP - summoner.HP;
                                    eidolon.Heal($"{healing}", summonerShareHP.CA);
                                }
                            }
                        } else {
                            // Invividual effect
                            if (eidolonShareHP.HealOrHarm(eidolon.HP) == SummonerClassEnums.EffectKind.HARM) {
                                int damage = eidolonShareHP.HP - eidolon.HP;
                                await eidolon.DealDirectDamage(eidolonShareHP.CA, DiceFormula.FromText($"{damage}"), summoner, CheckResult.Success, DamageKind.Untyped);
                            } else if (eidolonShareHP.HealOrHarm(eidolon.HP) == SummonerClassEnums.EffectKind.HEAL) {
                                int healing = eidolon.HP - eidolonShareHP.HP;
                                summoner.Heal($"{healing}", eidolonShareHP.CA);
                            }
                        }

                        //if (action.Owner == qfHazardDamage.Owner.Battle.Pseudocreature && kind != DamageKind.Untyped) {
                        //    qfHazardDamage.Owner.Battle.Log("{b}HAZARD DAMAGE LOGGED{/b}");
                        //}
                    }),
                });

            // Add a quick effect with an end turn trigger, which gives summoner actions == to their pet
        }

        private async static Task EidolonActs(Creature summoner, Creature eidolon) {
            if (eidolon.OwningFaction.IsEnemy)
                await Task.Delay(500);
            Creature oldActiveCreature = eidolon.Battle.ActiveCreature;
            await eidolon.Battle.GameLoop.StateCheck();
            eidolon.Battle.ActiveCreature = eidolon;
            Action<Tile> centerIfNotVisible = eidolon.Battle.SmartCenterIfNotVisible;
            if (centerIfNotVisible != null)
                centerIfNotVisible(eidolon.Occupies);
            await eidolon.Battle.GameLoop.StateCheck();
            //Set remaining actions
            eidolon.Actions.UseUpActions(eidolon.Actions.ActionsLeft - summoner.Actions.ActionsLeft, ActionDisplayStyle.UsedUp);
            if (eidolon.OwningFaction.IsHumanControlled)
                Sfxs.Play(SfxName.StartOfTurn, 0.1f);
            await eidolon.Battle.GameLoop.StateCheck();
            // Process eidolon's turn
            await (Task)eidolon.Battle.GameLoop.GetType().GetMethod("SubActionPhase", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static).Invoke(eidolon.Battle.GameLoop, new object[] { eidolon });
            // Update summoner's actions
            summoner.Actions.UseUpActions(summoner.Actions.ActionsLeft - eidolon.Actions.ActionsLeft, ActionDisplayStyle.UsedUp);
            await eidolon.Battle.GameLoop.StateCheck();
            eidolon.Actions.ForgetAllTurnCounters();
            await eidolon.Battle.GameLoop.StateCheck();
            eidolon.Battle.ActiveCreature = oldActiveCreature;
            oldActiveCreature = (Creature)null;
        }

    }
}

//Yes, indeed.  To prevent an action from showing up on the stat block, add the trait Trait.Basic to its list of trait. Not too intuitive, sorry.