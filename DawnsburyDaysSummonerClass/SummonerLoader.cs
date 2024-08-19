using System;
using System.Collections.Generic;
using System.Collections;
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
using static Dawnsbury.Mods.Classes.Summoner.SummonerSpells;
using static Dawnsbury.Mods.Classes.Summoner.SummonerClassLoader;
using static System.Collections.Specialized.BitVector32;
using System.Diagnostics;
using static System.Net.Mime.MediaTypeNames;
using System.Runtime.Intrinsics.Arm;
using System.Xml;
using Dawnsbury.Core.Mechanics.Damage;
using System.Runtime.CompilerServices;

namespace Dawnsbury.Mods.Classes.Summoner {

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
        public static Trait tSummoner = ModManager.RegisterTrait("SummonerTrait", GenerateClassProperty(new TraitProperties("Summoner", true)));
        public static Trait tEvolution = ModManager.RegisterTrait("EvolutionTrait", new TraitProperties("Evolution", true));
        public static Trait tTandem = ModManager.RegisterTrait("TandemTrait", new TraitProperties("Tandem", true));
        public static Trait tEidolon = ModManager.RegisterTrait("EidolonCompanion", new TraitProperties("Eidolon", true));
        public static Trait tPrimaryAttackType = ModManager.RegisterTrait("EidolonPrimaryWeaponType", new TraitProperties("Eidolon Primary Weapon Type", false));
        public static Trait tPrimaryAttackStats = ModManager.RegisterTrait("EidolonPrimaryWeaponStats", new TraitProperties("Eidolon Primary Weapon Stats", false));
        public static Trait tSecondaryAttackType = ModManager.RegisterTrait("EidolonSecondaryWeaponType", new TraitProperties("Eidolon Secondary Weapon Type", false));
        public static Trait tAlignment = ModManager.RegisterTrait("EidolonAlignment", new TraitProperties("Eidolon Alignment", false));
        public static Trait tAdvancedWeaponfryAtkType = ModManager.RegisterTrait("AdvancedWeaponAttackType", new TraitProperties("Advanced Weaponry Attack Type", false));
        public static Trait tAdvancedWeaponfryAtkTrait = ModManager.RegisterTrait("AdvancedWeaponAttackTrait", new TraitProperties("Advanced Weaponry Attack Trait", false));
        public static Trait tGrapple = ModManager.RegisterTrait("SummonerGrapple", new TraitProperties("Grapple", true, "You can add your item bonus to grapple checks made using this weapon."));
        public static Trait tBreathWeaponArea = ModManager.RegisterTrait("SummonerBreathWeaponArea", new TraitProperties("Breath Weapon Area", false));
        public static Trait tDragonType = ModManager.RegisterTrait("SummonerDragonType", new TraitProperties("Dragon Type", false));
        public static Trait tRemaster = ModManager.RegisterTrait("SummonerRemasterContent", new TraitProperties("Remaster", true));

        // Feat names
        private static FeatName classSummoner = ModManager.RegisterFeatName("SummonerClass", "Summoner");
        private static FeatName scAngelicEidolon = ModManager.RegisterFeatName("Angelic Eidolon");
        private static FeatName scAngelicEidolonAvenger = ModManager.RegisterFeatName("Angelic Avenger");
        private static FeatName scAngelicEidolonEmmissary = ModManager.RegisterFeatName("Angelic Emmisary");

        private static FeatName scDraconicEidolon = ModManager.RegisterFeatName("Draconic Eidolon");
        private static FeatName scDraconicEidolonCunning = ModManager.RegisterFeatName("Cunning Dragon");
        private static FeatName scDraconicEidolonMarauding = ModManager.RegisterFeatName("Marauding Dragon");
        private static FeatName ftBreathWeaponLine = ModManager.RegisterFeatName("Breath Weapon: Line");
        private static FeatName ftBreathWeaponCone = ModManager.RegisterFeatName("Breath Weapon: Cone");

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

        // Alignment Options
        private static FeatName ftALawfulGood = ModManager.RegisterFeatName("LawfulGood", "Lawful Good");
        private static FeatName ftAGood = ModManager.RegisterFeatName("Good", "Good");
        private static FeatName ftAChaoticGood = ModManager.RegisterFeatName("ChaoticGood", "Chaotic Good");
        private static FeatName ftALawful = ModManager.RegisterFeatName("Lawful", "Lawful");
        private static FeatName ftANeutral = ModManager.RegisterFeatName("TrueNeutral", "Neutral");
        private static FeatName ftAChaotic = ModManager.RegisterFeatName("Chaotic", "Chaotic");
        private static FeatName ftALawfulEvil = ModManager.RegisterFeatName("LawfulEvil", "Lawful Evil");
        private static FeatName ftAEvil = ModManager.RegisterFeatName("Evil", "Evil");
        private static FeatName ftAChaoticEvil = ModManager.RegisterFeatName("ChaoticEvil", "Chaotic Evil");

        // SpellIDs
        private static Dictionary<SummonerSpellId, SpellId> spells = LoadSpells();

        // QEffectIDs
        public static QEffectId qfSharedActions = ModManager.RegisterEnumMember<QEffectId>("Shared Actions");
        public static QEffectId qfSummonerBond = ModManager.RegisterEnumMember<QEffectId>("Shared HP");
        public static QEffectId qfActTogetherToggle = ModManager.RegisterEnumMember<QEffectId>("Act Together Toggle");
        public static QEffectId qfActTogether = ModManager.RegisterEnumMember<QEffectId>("Act Together");

        // Illustrations
        public static ModdedIllustration illActTogether = new ModdedIllustration("SummonerAssets/ActTogether.png");
        public static ModdedIllustration illActTogetherStatus = new ModdedIllustration("SummonerAssets/ActTogether_Status.png");
        public static ModdedIllustration illDismiss = new ModdedIllustration("SummonerAssets/Dismiss.png");
        public static ModdedIllustration illEidolonBoost = new ModdedIllustration("SummonerAssets/EidolonBoost.png");
        public static ModdedIllustration illEvolutionSurge = new ModdedIllustration("SummonerAssets/EvolutionSurge.png");

        // Class and subclass text
        private static readonly string SummonerFlavour = "You can magically beckon a powerful being called an eidolon to your side, serving as the mortal conduit that anchors it to the world. " +
            "Whether your eidolon is a friend, a servant, or even a personal god, your connection to it marks you as extraordinary, shaping the course of your life dramatically.";
        private static readonly string SummonerCrunch =
            "{b}1. Eidolon.{/b} You have a connection with a powerful and usually otherworldly entity called an eidolon, and you can use your life force as a conduit to manifest this ephemeral entity into the mortal world. " +
            "Your bonded eidolon's nature determine your spell casting tradition, in addition to its statistics. In addition, its appearence and attacks are fully customisable.\n\n" +
            "Your eidolon begins combat already manifested, and shares your hit point pool, actions and multiple attack penalty. You can swap between controlling you or your eidolon at any time, without ending your turn.\n\n" +
            "{b}2. Evolution Feat.{/b} etc.\n\n" +
            "{b}3. Link Spells.{/b} Your connection to your eidolon allows you to cast link spells, special spells that have been forged through your shared connection with your eidolon." +
            "You start with tw osuck spells. The focus spell " + AllSpells.CreateModernSpellTemplate(spells[SummonerSpellId.EvolutionSurge], tSummoner).ToSpellLink() + " and the link cantrip " +
            AllSpells.CreateModernSpellTemplate(spells[SummonerSpellId.EidolonBoost], tSummoner).ToSpellLink() + "\n\n" +
            "{b}4. Spontaneous Spellcasting:{/b} You can cast spells. You can cast 1 spell per day and you can choose the spells from among the spells you know. You learn 2 spells of your choice, " +
            "but they must come from the spellcasting tradition of your eidolon. You also learn 5 cantrips — weak spells — that automatically heighten as you level up. You can cast any number of cantrips per day. " +
            "You can gain additional spell slots and spells known from leveling up and from feats. Your spellcasting ability is Charisma" +
            "\n\n{b}At higher levels:{/b}" +
            "\n{b}Level 2:{/b} Summoner feat" +
            "\n{b}Level 3:{/b} General feat, skill increase, level 2 spells {i}(one spell slot){/i}" +
            "\n{b}Level 4:{/b} Summoner feat, additional level 2 spell slot";
        private static readonly string AngelicEidolonFlavour = "Your eidolon is a celestial messenger, a member of the angelic host with a unique link to you, allowing them to carry a special message to the mortal world at your side. " +
            "Most angel eidolons are roughly humanoid in form, with feathered wings, glowing eyes, halos, or similar angelic features. However, some take the form of smaller angelic servitors like the winged helme t" +
            "cassisian angel instead. The two of you are destined for an important role in the plans of the celestial realms. Though a true angel, your angel eidolon's link to you as a mortal prevents them " +
            "from casting the angelic messenger ritual, even if they somehow learn it.";

        private static readonly string AngelicEidolonCrunch = "\n\n• {b}Tradition{/b} Divine\n• {b}Skills{/b} Diplomacy, Religion\n\n{b}Subclass Features{/b}\n\n• {b}Hallowed Strikes.{/b} Your Eidolon's strikes deal +1 good damage.";

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
            Feat angelicEidolon = new EidolonBond(scAngelicEidolon, AngelicEidolonFlavour, AngelicEidolonCrunch, Trait.Divine, new List<FeatName>() { FeatName.Religion, FeatName.Diplomacy }, new Func<Feat, bool>(ft => new FeatName[] { ftALawfulGood, ftAGood, ftAChaoticGood }.Contains(ft.FeatName)))
                .WithOnSheet((Action<CalculatedCharacterSheetValues>)(values => {
                    values.AddSelectionOption((SelectionOption)new SingleFeatSelectionOption("AngelicEidolonArray", "Eidolon Ability Scores", 1, (Func<Feat, bool>)(ft => new FeatName[] { scAngelicEidolonAvenger, scAngelicEidolonEmmissary }.Contains(ft.FeatName))));
                }));
            yield return CreateEidolonFeat(scAngelicEidolonAvenger, "Your eidolon is a fierce warrior of the heavens.", IllustrationName.AnimatedStatue256, new int[6] { 4, 2, 3, -1, 1, 0 }, 2, 3);
            yield return CreateEidolonFeat(scAngelicEidolonEmmissary, "Your eidolon is a regal emmisary of the heavens.", IllustrationName.Flumph256, new int[6] { 1, 4, 1, 0, 1, 2 }, 1, 4);

            Feat draconicEidolon = new Feat(scDraconicEidolon, "FLAVOUR TEXT", "CRUNCH TEXT", new List<Trait>() { }, null)
                .WithOnSheet((Action<CalculatedCharacterSheetValues>)(values => {
                    values.AddSelectionOption((SelectionOption)new SingleFeatSelectionOption("DraconicEidolonArray", "Eidolon Ability Scores", 1, (Func<Feat, bool>)(ft => new FeatName[] { scDraconicEidolonCunning, scDraconicEidolonMarauding }.Contains(ft.FeatName))));
                    values.AddSelectionOption((SelectionOption)new SingleFeatSelectionOption("DragonType", "Dragon Type", 1, (Func<Feat, bool>)(ft => ft.HasTrait(tDragonType))));
                }));

            yield return CreateEidolonFeat(scDraconicEidolonCunning, "Your eidolon is a cunning wyrm.", IllustrationName.FaerieDragon256, new int[6] { 1, 4, 1, 2, 1, 1 }, 1, 4);
            yield return CreateEidolonFeat(scDraconicEidolonMarauding, "Your eidolon is a fierce marauding drake.", IllustrationName.ElectricityDragonWyrmling256, new int[6] { 4, 2, 3, 0, 0, 0 }, 2, 3);

            // Dragon breath feats
            Feat dragonLineBreath = new Feat(ftBreathWeaponLine, "Your dragon eidolon emits a sharp, destructive line of energy.", "Your eidolon's breath weapon hits each creature in a 60-foot line.", new List<Trait>() { tSummoner, tBreathWeaponArea }, null);
            Feat dragonConeBreath = new Feat(ftBreathWeaponCone, "Your dragon eidolon spaws worth a torrent of destructive energy.", "Your eidolon's breath weapon hits each creature in a 30-foot cone.", new List<Trait>() { tSummoner, tBreathWeaponArea }, null);

            // Trait.Arcane, new List<FeatName>() { FeatName.Arcana, FeatName.Intimidation }, new Func<Feat, bool>(ft => ft.HasTrait(tAlignment))

            yield return new EidolonBond(ModManager.RegisterFeatName("AdamantineDragon", "Adamantite Dragon"), "Your eidolon is a gleaming adamantite dragon.", "Your eidolon's breath weapon deals mental damage vs. Will.",
                Trait.Primal, new List<FeatName>() { FeatName.Nature, FeatName.Intimidation }, new Func<Feat, bool>(ft => ft.HasTrait(tAlignment)),
                new List<Trait>() { Trait.Mental, tRemaster, tSummoner, tDragonType }, new List<Feat>() { dragonConeBreath, dragonLineBreath });
            yield return new EidolonBond(ModManager.RegisterFeatName("ConspiratorDragon", "Conspirator Dragon"), "Your eidolon is a cunning conspirator dragon.", "Your eidolon's breath weapon deals poison damage vs. Fortitude.",
                Trait.Occult, new List<FeatName>() { FeatName.Occultism, FeatName.Intimidation }, new Func<Feat, bool>(ft => ft.HasTrait(tAlignment)),
                new List<Trait>() { Trait.Poison, tRemaster, tSummoner, tDragonType }, new List<Feat>() { dragonConeBreath, dragonLineBreath });
            yield return new EidolonBond(ModManager.RegisterFeatName("DiabolicDragon", "Diabolic Dragon"), "Your eidolon is a hellish diabolic dragon.", "Your eidolon's breath weapon deals fire damage vs. Reflex.",
                Trait.Arcane, new List<FeatName>() { FeatName.Arcana, FeatName.Intimidation }, new Func<Feat, bool>(ft => ft.FeatName == ftALawfulEvil),
                new List<Trait>() { Trait.Fire, tRemaster, tSummoner, tDragonType }, new List<Feat>() { dragonConeBreath, dragonLineBreath });
            yield return new EidolonBond(ModManager.RegisterFeatName("EmpyrealDragon", "Empyreal Dragon"), "Your eidolon is a heavenly empyreal dragon.", "Your eidolon's breath weapon deals good damage vs. Reflex.",
                Trait.Divine, new List<FeatName>() { FeatName.Religion, FeatName.Intimidation }, new Func<Feat, bool>(ft => ft.HasTrait(tAlignment) && ft.HasTrait(Trait.Good)),
                new List<Trait>() { Trait.Good, tRemaster, tSummoner, tDragonType }, new List<Feat>() { dragonConeBreath, dragonLineBreath });
            yield return new EidolonBond(ModManager.RegisterFeatName("FortuneDragon", "Fortune Dragon"), "Your eidolon is a bejewled fortune dragon.", "Your eidolon's breath weapon deals force damage vs. Reflex.",
                Trait.Arcane, new List<FeatName>() { FeatName.Arcana, FeatName.Intimidation }, new Func<Feat, bool>(ft => ft.HasTrait(tAlignment)),
                new List<Trait>() { Trait.Force, tRemaster, tSummoner, tDragonType }, new List<Feat>() { dragonConeBreath, dragonLineBreath });
            yield return new EidolonBond(ModManager.RegisterFeatName("HornedDragon", "Horned Dragon"), "Your eidolon is a bestial horned dragon.", "Your eidolon's breath weapon deals poison damage vs. Fortitude.",
                Trait.Primal, new List<FeatName>() { FeatName.Nature, FeatName.Intimidation }, new Func<Feat, bool>(ft => ft.HasTrait(tAlignment)),
                new List<Trait>() { Trait.Poison, tRemaster, tSummoner, tDragonType }, new List<Feat>() { dragonConeBreath, dragonLineBreath });
            yield return new EidolonBond(ModManager.RegisterFeatName("MirageDragon", "Mirage Dragon"), "Your eidolon is an elusive mirage dragon.", "Your eidolon's breath weapon deals mental damage vs. Will.",
                Trait.Arcane, new List<FeatName>() { FeatName.Arcana, FeatName.Intimidation }, new Func<Feat, bool>(ft => ft.HasTrait(tAlignment)),
                new List<Trait>() { Trait.Mental, tRemaster, tSummoner, tDragonType }, new List<Feat>() { dragonConeBreath, dragonLineBreath });
            yield return new EidolonBond(ModManager.RegisterFeatName("OmenDragon", "Omen Dragon"), "Your eidolon is an auspicious omen dragon.", "Your eidolon's breath weapon deals mental damage vs. Will.",
                Trait.Occult, new List<FeatName>() { FeatName.Occultism, FeatName.Intimidation }, new Func<Feat, bool>(ft => ft.HasTrait(tAlignment)),
                new List<Trait>() { Trait.Mental, tRemaster, tSummoner, tDragonType }, new List<Feat>() { dragonConeBreath, dragonLineBreath });

            yield return new EidolonBond(ModManager.RegisterFeatName("BlackDragon", "Black Dragon"), "Your eidolon is a vile black dragon.", "Your eidolon's breath weapon deals acid damage vs. Reflex.",
                Trait.Arcane, new List<FeatName>() { FeatName.Arcana, FeatName.Intimidation }, new Func<Feat, bool>(ft => ft.HasTrait(tAlignment) && ft.HasTrait(Trait.Evil)),
                new List<Trait>() { Trait.Acid, tSummoner, tDragonType }, new List<Feat>() { dragonConeBreath, dragonLineBreath });
            yield return new EidolonBond(ModManager.RegisterFeatName("BlueDragon", "Blue Dragon"), "Your eidolon is a sophisticated blue dragon.", "Your eidolon's breath weapon deals electricity damage vs. Reflex.",
                Trait.Arcane, new List<FeatName>() { FeatName.Arcana, FeatName.Intimidation }, new Func<Feat, bool>(ft => ft.HasTrait(tAlignment) && ft.HasTrait(Trait.Evil)),
                new List<Trait>() { Trait.Electricity, tSummoner, tDragonType }, new List<Feat>() { dragonConeBreath, dragonLineBreath });
            yield return new EidolonBond(ModManager.RegisterFeatName("GreenDragon", "Green Dragon"), "Your eidolon is a cunning green dragon.", "Your eidolon's breath weapon deals poison damage vs. Fortitude.",
                Trait.Arcane, new List<FeatName>() { FeatName.Arcana, FeatName.Intimidation }, new Func<Feat, bool>(ft => ft.HasTrait(tAlignment) && ft.HasTrait(Trait.Evil)),
                new List<Trait>() { Trait.Poison, tSummoner, tDragonType }, new List<Feat>() { dragonConeBreath, dragonLineBreath });
            yield return new EidolonBond(ModManager.RegisterFeatName("RedDragon", "Red Dragon"), "Your eidolon is a tyranical red dragon.", "Your eidolon's breath weapon deals fire damage vs. Reflex.",
                Trait.Arcane, new List<FeatName>() { FeatName.Arcana, FeatName.Intimidation }, new Func<Feat, bool>(ft => ft.HasTrait(tAlignment) && ft.HasTrait(Trait.Evil)),
                new List<Trait>() { Trait.Fire, tSummoner, tDragonType }, new List<Feat>() { dragonConeBreath, dragonLineBreath });
            yield return new EidolonBond(ModManager.RegisterFeatName("WhiteDragon", "White Dragon"), "Your eidolon is a feral white dragon.", "Your eidolon's breath weapon deals cold damage vs. Reflex.",
                Trait.Arcane, new List<FeatName>() { FeatName.Arcana, FeatName.Intimidation }, new Func<Feat, bool>(ft => ft.HasTrait(tAlignment) && ft.HasTrait(Trait.Evil)),
                new List<Trait>() { Trait.Cold, tSummoner, tDragonType }, new List<Feat>() { dragonConeBreath, dragonLineBreath });

            yield return new EidolonBond(ModManager.RegisterFeatName("CopperDragon", "Copper Dragon"), "Your eidolon is a wily copper dragon.", "Your eidolon's breath weapon deals acid damage vs. Reflex.",
                Trait.Arcane, new List<FeatName>() { FeatName.Arcana, FeatName.Intimidation }, new Func<Feat, bool>(ft => ft.FeatName == ftAChaoticGood),
                new List<Trait>() { Trait.Acid, tSummoner, tDragonType }, new List<Feat>() { dragonConeBreath, dragonLineBreath });
            yield return new EidolonBond(ModManager.RegisterFeatName("BronzeDragon", "Bronze Dragon"), "Your eidolon is a scholarly bronze dragon.", "Your eidolon's breath weapon deals electricity damage vs. Reflex.",
                Trait.Arcane, new List<FeatName>() { FeatName.Arcana, FeatName.Intimidation }, new Func<Feat, bool>(ft => ft.HasTrait(tAlignment) && ft.HasTrait(Trait.Good)),
                new List<Trait>() { Trait.Electricity, tSummoner, tDragonType }, new List<Feat>() { dragonConeBreath, dragonLineBreath });
            yield return new EidolonBond(ModManager.RegisterFeatName("BrassDragon", "Brass Dragon"), "Your eidolon is a whimsical brass dragon.", "Your eidolon's breath weapon deals fire damage vs. Reflex.",
                Trait.Arcane, new List<FeatName>() { FeatName.Arcana, FeatName.Intimidation }, new Func<Feat, bool>(ft => ft.HasTrait(tAlignment) && ft.HasTrait(Trait.Good)),
                new List<Trait>() { Trait.Fire, tSummoner, tDragonType }, new List<Feat>() { dragonConeBreath, dragonLineBreath });
            yield return new EidolonBond(ModManager.RegisterFeatName("GoldDragon", "Gold Dragon"), "Your eidolon is an honourable gold dragon.", "Your eidolon's breath weapon deals fire damage vs. Reflex.",
                Trait.Arcane, new List<FeatName>() { FeatName.Arcana, FeatName.Intimidation }, new Func<Feat, bool>(ft => ft.FeatName == ftALawfulGood),
                new List<Trait>() { Trait.Fire, tSummoner, tDragonType }, new List<Feat>() { dragonConeBreath, dragonLineBreath });
            yield return new EidolonBond(ModManager.RegisterFeatName("SilverDragon", "Silver Dragon"), "Your eidolon is a silver white dragon.", "Your eidolon's breath weapon deals cold damage vs. Reflex.",
                Trait.Arcane, new List<FeatName>() { FeatName.Arcana, FeatName.Intimidation }, new Func<Feat, bool>(ft => ft.HasTrait(tAlignment) && ft.HasTrait(Trait.Good)),
                new List<Trait>() { Trait.Cold, tSummoner, tDragonType }, new List<Feat>() { dragonConeBreath, dragonLineBreath });

            // Init class
            yield return new ClassSelectionFeat(classSummoner, SummonerFlavour, tSummoner,
                new EnforcedAbilityBoost(Ability.Charisma), 10, new Trait[5] { Trait.Unarmed, Trait.Simple, Trait.UnarmoredDefense, Trait.Reflex, Trait.Perception }, new Trait[2] { Trait.Fortitude, Trait.Will }, 3, SummonerCrunch, new List<Feat>() { angelicEidolon, draconicEidolon })
                    .WithOnSheet((Action<CalculatedCharacterSheetValues>)(sheet => {
                        sheet.AddFocusSpellAndFocusPoint(tSummoner, Ability.Charisma, spells[SummonerSpellId.EvolutionSurge]);
                    }));
                    //.WithRulesBlockForSpell(spells[SummonerSpellId.EvolutionSurge], tSummoner).WithIllustration((Illustration)illEvolutionSurge).WithIllustration(null)
                    //.WithRulesBlockForSpell(spells[SummonerSpellId.EidolonBoost], tSummoner).WithIllustration((Illustration)illEidolonBoost)
                    //.WithIllustration(null);

            // Init TrueFeats
            yield return new TrueFeat(ModManager.RegisterFeatName("AbundantSpellCastingSummoner1", "Abundant Spellcasting"), 1, "Your strong connect to your eidolon grants you additional spells.", "You gain an extra level 1 spell slot.", new Trait[2] {
                tSummoner,
                Trait.Homebrew
            }).WithOnSheet((Action<CalculatedCharacterSheetValues>)(values => {
                if (!values.SpellRepertoires.ContainsKey(tSummoner))
                    return;
                ++values.SpellRepertoires[tSummoner].SpellSlots[1];
            }));

            yield return new TrueFeat(ModManager.RegisterFeatName("AbundantSpellCastingSummoner4", "Abundant Spellcasting 2"), 4, "Your strong connect to your eidolon grants you additional spells.", "You gain an extra level 2 spell slot.", new Trait[2] {
                tSummoner,
                Trait.Homebrew
            }).WithOnSheet((Action<CalculatedCharacterSheetValues>)(values => {
                if (!values.SpellRepertoires.ContainsKey(tSummoner))
                    return;
                ++values.SpellRepertoires[tSummoner].SpellSlots[2];
            }));

            yield return new EvolutionFeat(ModManager.RegisterFeatName("Airborn Form"), 1, "", "", new Trait[] { Trait.Homebrew }, new QEffect { Id = QEffectId.Flying }, null);

            yield return new EvolutionFeat(ModManager.RegisterFeatName("Advanced Weaponry"), 1, "", "", new Trait[] { }, new QEffect() {
                ExpiresAt = ExpirationCondition.ExpiresAtEndOfAnyTurn,
                StartOfCombat = (async (qf) => {
                    string atkType = GetSummoner(qf.Owner).PersistentCharacterSheet.Calculated.AllFeats.FirstOrDefault((Func<Feat, bool>)(ft => ft.HasTrait(tAdvancedWeaponfryAtkType))).Name;
                    Item naturalWeapon;
                    if (atkType == "Primary Unarmed Attack")
                        naturalWeapon = qf.Owner.UnarmedStrike;
                    else
                        naturalWeapon = qf.Owner.QEffects.FirstOrDefault(qf => qf.AdditionalUnarmedStrike != null && qf.AdditionalUnarmedStrike.WeaponProperties.Melee).AdditionalUnarmedStrike;
                    
                    string traitName = GetSummoner(qf.Owner).PersistentCharacterSheet.Calculated.AllFeats.FirstOrDefault((Func<Feat, bool>)(ft => ft.HasTrait(tAdvancedWeaponfryAtkTrait))).Name;
                    Trait trait;
                    switch (traitName) {
                        case "Disarm":
                            trait = Trait.Disarm;
                            break;
                        case "Grapple":
                            trait = tGrapple;
                            break;
                        case "Shove":
                            trait = Trait.Shove;
                            break;
                        case "Trip":
                            trait = Trait.Trip;
                            break;
                        case "Versatile Piercing":
                            trait = Trait.VersatileP;
                            break;
                        case "Versatile Slashing":
                            trait = Trait.VersatileS;
                            break;
                        default:
                            trait = Trait.Melee;
                            break;
                    }

                    if (naturalWeapon.HasTrait(trait)) {
                        return;
                    }

                    naturalWeapon.Traits.Add(trait);
                })
            } ,new List<Feat> {
                new Feat(ModManager.RegisterFeatName("Primary Unarmed Attack"), "", "", new List<Trait>() { tSummoner, tAdvancedWeaponfryAtkType }, null).WithOnSheet(sheet => {
                sheet.AddSelectionOption((SelectionOption)new SingleFeatSelectionOption("AdvancedWeaponryTrait", "Eidolon Advanced Weaponry Trait", 1, (Func<Feat, bool>)(ft => ft.HasTrait(tAdvancedWeaponfryAtkTrait))));
            }),
                new Feat(ModManager.RegisterFeatName("Secondary Unarmed Attack"), "", "", new List<Trait>() { tSummoner, tAdvancedWeaponfryAtkType }, null).WithOnSheet(sheet => {
                sheet.AddSelectionOption((SelectionOption)new SingleFeatSelectionOption("AdvancedWeaponryTrait", "Eidolon Advanced Weaponry Trait", 1, (Func<Feat, bool>)(ft => ft.HasTrait(tAdvancedWeaponfryAtkTrait))));
            })
            });

            yield return new Feat(ModManager.RegisterFeatName("Disarm"), "Your eidolon's natural weapon is especially adept at prying away their foe's weapons.", "{b}" + Trait.Disarm.HumanizeTitleCase2() + "{/b} " + Trait.Disarm.GetTraitProperties().RulesText, new List<Trait>() { tSummoner, tAdvancedWeaponfryAtkTrait }, null);
            yield return new Feat(ModManager.RegisterFeatName("Grapple"), "Your eidolon's natural weapon is especially adept at ensaring their foes.", "{b}" + tGrapple.HumanizeTitleCase2() + "{/b} " + tGrapple.GetTraitProperties().RulesText, new List<Trait>() { tSummoner, tAdvancedWeaponfryAtkTrait }, null);
            yield return new Feat(ModManager.RegisterFeatName("Shove"), "Your eidolon's natural weapon is especially adept at shoving away their foes.", "{b}" + Trait.Shove.HumanizeTitleCase2() + "{/b} " + Trait.Shove.GetTraitProperties().RulesText, new List<Trait>() { tSummoner, tAdvancedWeaponfryAtkTrait }, null);
            yield return new Feat(ModManager.RegisterFeatName("Trip"), "Your eidolon's natural weapon is especially adept at topplin their enemies.", "{b}" + Trait.Trip.HumanizeTitleCase2() + "{/b} " + Trait.Trip.GetTraitProperties().RulesText, new List<Trait>() { tSummoner, tAdvancedWeaponfryAtkTrait }, null);
            yield return new Feat(ModManager.RegisterFeatName("Versatile Piercing"), "Your eidolon's natural weapon has a deadly piercing appendage.", "{b}" + Trait.VersatileP.HumanizeTitleCase2() + "{/b}" + Trait.VersatileP.GetTraitProperties().RulesText, new List<Trait>() { tSummoner, tAdvancedWeaponfryAtkTrait }, null);
            yield return new Feat(ModManager.RegisterFeatName("Versatile Slashing"), "Your eidolon's natural weapon has a sharp slashing edge.", "{b}" + Trait.VersatileS.HumanizeTitleCase2() + "{/b}" + Trait.VersatileS.GetTraitProperties().RulesText, new List<Trait>() { tSummoner, tAdvancedWeaponfryAtkTrait }, null);

            // Init Natural Attack Options
            yield return new Feat(ftPSword, "Your eidolon wields a sword, or possess a natural blade-like appendage.", "Your eidolon's primary attack deals slashing damage.", new List<Trait> { tPrimaryAttackType, Trait.Strike }, null).WithIllustration(IllustrationName.Longsword);
            yield return new Feat(ftPPolearm, "Your eidolon wields a spear or lance, or possess a natural spear-like appendage.", "Your eidolon's primary attack deals piercing damage.", new List<Trait> { tPrimaryAttackType, Trait.Strike }, null).WithIllustration(IllustrationName.Spear);
            yield return new Feat(ftPMace, "Your eidolon wields a mace, or possess a natural mace-like appendage.", "Your eidolon's primary attack deals bludgeoning damage.", new List<Trait> { tPrimaryAttackType, Trait.Strike }, null).WithIllustration(IllustrationName.Warhammer);
            yield return new Feat(ftPWing, "Your eidolon knocks its enemies aside with a pair of powerful wings.", "Your eidolon's primary attack deals bludgeoning damage.", new List<Trait> { tPrimaryAttackType, Trait.Strike }, null).WithIllustration(IllustrationName.Wing);
            yield return new Feat(ftPKick, "Your eidolon possesses a powerful kick.", "Your eidolon's primary attack deals bludgeoning damage.", new List<Trait> { tPrimaryAttackType, Trait.Strike }, null).WithIllustration(IllustrationName.BootsOfElvenkind);
            yield return new Feat(ftPClaw, "Your eidolon possesses razor sharp claws.", "Your eidolon's primary attack deals slashing damage.", new List<Trait> { tPrimaryAttackType, Trait.Strike }, null).WithIllustration(IllustrationName.DragonClaws);
            yield return new Feat(ftPJaws, "Your eidolon possesses powerful bite attack.", "Your eidolon's primary attack deals piercing damage.", new List<Trait> { tPrimaryAttackType, Trait.Strike }, null).WithIllustration(IllustrationName.Jaws);
            yield return new Feat(ftPFist, "Your eidolon tears or pummels its enemies apart with its bare hands.", "Your eidolon's primary attack deals bludgeoning damage.", new List<Trait> { tPrimaryAttackType, Trait.Strike }, null).WithIllustration(IllustrationName.Fist);
            yield return new Feat(ftPTendril, "Your eidolon possesses crushing tendrils.", "Your eidolon's primary attack deals bludgeoning damage.", new List<Trait> { tPrimaryAttackType, Trait.Strike }, null).WithIllustration(IllustrationName.Tentacle);
            yield return new Feat(ftPHorn, "Your eidolon possesses vicious horns to gore its enemies.", "Your eidolon's primary attack deals piercing damage.", new List<Trait> { tPrimaryAttackType, Trait.Strike }, null).WithIllustration(IllustrationName.Horn);
            yield return new Feat(ftPTail, "Your eidolon possesses deadly stinging tail.", "Your eidolon's primary attack deals piercing damage.", new List<Trait> { tPrimaryAttackType, Trait.Strike }, null).WithIllustration(IllustrationName.Tail);

            yield return new Feat(ftSWing, "Your eidolon knocks its enemies aside with a pair of powerful wings.", "Your eidolon's secondary attack deals 1d6 bludgeoning damage with the agile and finesse traits." +
                "\n\n{b}" + Trait.Agile.GetTraitProperties().HumanizedName + "{/b} " + Trait.Agile.GetTraitProperties().RulesText +
                "\n{b}" + Trait.Finesse.GetTraitProperties().HumanizedName + "{/b} " + Trait.Finesse.GetTraitProperties().RulesText,
                new List<Trait> { tSecondaryAttackType, Trait.Strike }, null).WithIllustration(IllustrationName.Wing);
            yield return new Feat(ftSKick, "Your eidolon possesses a powerful kick.", "Your eidolon's secondary attack deals 1d6 bludgeoning damage with the agile and finesse traits." +
                "\n\n{b}" + Trait.Agile.GetTraitProperties().HumanizedName + "{/b} " + Trait.Agile.GetTraitProperties().RulesText +
                "\n{b}" + Trait.Finesse.GetTraitProperties().HumanizedName + "{/b} " + Trait.Finesse.GetTraitProperties().RulesText,
                new List<Trait> { tSecondaryAttackType, Trait.Strike }, null).WithIllustration(IllustrationName.BootsOfElvenkind);
            yield return new Feat(ftSClaw, "Your eidolon possesses razor sharp claws.", "Your eidolon's secondary attack deals 1d6 slashing damage with the agile and finesse traits." +
                "\n\n{b}" + Trait.Agile.GetTraitProperties().HumanizedName + "{/b} " + Trait.Agile.GetTraitProperties().RulesText +
                "\n{b}" + Trait.Finesse.GetTraitProperties().HumanizedName + "{/b} " + Trait.Finesse.GetTraitProperties().RulesText,
                new List<Trait> { tSecondaryAttackType, Trait.Strike }, null).WithIllustration(IllustrationName.DragonClaws);
            yield return new Feat(ftSJaws, "Your eidolon possesses powerful bite attack.", "Your eidolon's secondary attack deals 1d6 piercing damage with the agile and finesse traits." +
                "\n\n{b}" + Trait.Agile.GetTraitProperties().HumanizedName + "{/b} " + Trait.Agile.GetTraitProperties().RulesText +
                "\n{b}" + Trait.Finesse.GetTraitProperties().HumanizedName + "{/b} " + Trait.Finesse.GetTraitProperties().RulesText,
                new List<Trait> { tSecondaryAttackType, Trait.Strike }, null).WithIllustration(IllustrationName.Jaws);
            yield return new Feat(ftSFist, "Your eidolon tears or pummels its enemies apart with its bare hands.", "Your eidolon's secondary attack deals 1d6 bludgeoning damage with the agile and finesse traits." +
                "\n\n{b}" + Trait.Agile.GetTraitProperties().HumanizedName + "{/b} " + Trait.Agile.GetTraitProperties().RulesText +
                "\n{b}" + Trait.Finesse.GetTraitProperties().HumanizedName + "{/b} " + Trait.Finesse.GetTraitProperties().RulesText,
                new List<Trait> { tSecondaryAttackType, Trait.Strike }, null).WithIllustration(IllustrationName.Fist);
            yield return new Feat(ftSTendril, "Your eidolon possesses crushing tendrils.", "Your eidolon's secondary attack deals 1d6 bludgeoning damage with the agile and finesse traits." +
                "\n\n{b}" + Trait.Agile.GetTraitProperties().HumanizedName + "{/b} " + Trait.Agile.GetTraitProperties().RulesText +
                "\n{b}" + Trait.Finesse.GetTraitProperties().HumanizedName + "{/b} " + Trait.Finesse.GetTraitProperties().RulesText,
                new List<Trait> { tSecondaryAttackType, Trait.Strike }, null).WithIllustration(IllustrationName.Tentacle);
            yield return new Feat(ftSHorn, "Your eidolon possesses vicious horns to gore its enemies.", "Your eidolon's secondary attack deals 1d6 piercing damage with the agile and finesse traits." +
                "\n\n{b}" + Trait.Agile.GetTraitProperties().HumanizedName + "{/b} " + Trait.Agile.GetTraitProperties().RulesText +
                "\n{b}" + Trait.Finesse.GetTraitProperties().HumanizedName + "{/b} " + Trait.Finesse.GetTraitProperties().RulesText,
                new List<Trait> { tSecondaryAttackType, Trait.Strike }, null).WithIllustration(IllustrationName.Horn);
            yield return new Feat(ftSTail, "Your eidolon possesses deadly stinging tail.", "Your eidolon's secondary attack deals 1d6 piercing damage with the agile and finesse traits." +
                "\n\n{b}" + Trait.Agile.GetTraitProperties().HumanizedName + "{/b} " + Trait.Agile.GetTraitProperties().RulesText +
                "\n{b}" + Trait.Finesse.GetTraitProperties().HumanizedName + "{/b} " + Trait.Finesse.GetTraitProperties().RulesText,
                new List<Trait> { tSecondaryAttackType, Trait.Strike }, null).WithIllustration(IllustrationName.Tail);

            // Init Primary Weapon Properties
            yield return new Feat(ftPSPowerful, "Your eidolon possesses great strength, allowing it to easily bully and subdue its enemies.", "Your eidolon's primary deals 1d8 damage and has the disarm, nonlethal, shove and trip traits. Athletics checks made using a weapon with a maneovre trait benefit from the item bonus on your handwraps of mighty blows." +
                "\n\n{b}" + Trait.Disarm.GetTraitProperties().HumanizedName + "{/b} " + Trait.Disarm.GetTraitProperties().RulesText +
                "\n{b}" + Trait.Shove.GetTraitProperties().HumanizedName + "{/b} " + Trait.Shove.GetTraitProperties().RulesText +
                "\n{b}" + Trait.Trip.GetTraitProperties().HumanizedName + "{/b} " + Trait.Trip.GetTraitProperties().RulesText,
                new List<Trait> { tPrimaryAttackStats, Trait.Strike, Trait.Disarm, Trait.Nonlethal, Trait.Shove, Trait.Trip }, null);
            yield return new Feat(ftPSFatal, "Your eidolon waits patiently for the perfect opportunity before closing in on its foes.", "Your eidolon's primary attack deals 1d6 damage and has the fatal d10 traits." +
                "\n\n{b}" + Trait.FatalD10.GetTraitProperties().HumanizedName + "{/b} " + Trait.FatalD10.GetTraitProperties().RulesText,
                new List<Trait> { tPrimaryAttackStats, Trait.Strike, Trait.FatalD10 }, null);
            yield return new Feat(ftPSUnstoppable, "Your eidolon's attacks pick up speed and momentum as it fights.", "Your eidolon's primary attack deals 1d6 damage and has the forceful and sweep traits." +
                "\n\n{b}" + Trait.Forceful.GetTraitProperties().HumanizedName + "{/b} " + Trait.Forceful.GetTraitProperties().RulesText +
                "\n{b}" + Trait.Sweep.GetTraitProperties().HumanizedName + "{/b} " + Trait.Sweep.GetTraitProperties().RulesText,
                new List<Trait> { tPrimaryAttackStats, Trait.Strike, Trait.Forceful, Trait.Sweep }, null);
            yield return new Feat(ftPSGraceful, "Your eidolon possesses dexterous and opportunisitic natural weapons.", "Your eidolon's primary attack deals 1d6 damage and has the deadly d8 and finesse traits." +
                "\n\n{b}" + Trait.DeadlyD8.GetTraitProperties().HumanizedName + "{/b} "+ Trait.DeadlyD8.GetTraitProperties().RulesText +
                "\n{b}" + Trait.Finesse.GetTraitProperties().HumanizedName + "{/b} " + Trait.Finesse.GetTraitProperties().RulesText,
                new List<Trait> { tPrimaryAttackStats, Trait.Strike, Trait.DeadlyD8, Trait.Finesse }, null);

            // Init Eidolon Alignments
            yield return new Feat(ftALawfulGood, "Your eidolon's alignment is lawful good.", "RULES TEXT", new List<Trait> { tAlignment, Trait.Lawful, Trait.Good }, null);
            yield return new Feat(ftAGood, "Your eidolon's alignment is good.", " ", new List<Trait> { tAlignment, Trait.Good }, null);
            yield return new Feat(ftAChaoticGood, "Your eidolon's alignment is chaotic good.", " ", new List<Trait> { tAlignment, Trait.Chaotic, Trait.Good }, null);
            yield return new Feat(ftALawful, "Your eidolon's alignment is lawful.", " ", new List<Trait> { tAlignment, Trait.Lawful }, null);
            yield return new Feat(ftANeutral, "Your eidolon's alignment is true neutral.", "  ", new List<Trait> { tAlignment, Trait.Neutral }, null);
            yield return new Feat(ftAChaotic, "Your eidolon's alignment is chaotic.", " ", new List<Trait> { tAlignment, Trait.Chaotic }, null);
            yield return new Feat(ftALawfulEvil, "Your eidolon's alignment is lawful evil.", "  ", new List<Trait> { tAlignment, Trait.Lawful, Trait.Evil }, null);
            yield return new Feat(ftAEvil, "Your eidolon's alignment is evil.", "", new List<Trait> { tAlignment, Trait.Evil }, null);
            yield return new Feat(ftAChaoticEvil, "Your eidolon's alignment is chaotic evil.", "  ", new List<Trait> { tAlignment, Trait.Chaotic, Trait.Evil }, null);

        }

        public static Creature? GetSummoner(Creature eidolon) {
            return eidolon.QEffects.FirstOrDefault<QEffect>((Func<QEffect, bool>)(qf => qf.Id == qfSummonerBond))?.Source;
        }

        public static Creature? GetEidolon(Creature summoner) {
            return summoner.QEffects.FirstOrDefault<QEffect>((Func<QEffect, bool>)(qf => qf.Id == qfSummonerBond))?.Source;
        }

        private static TraitProperties GenerateClassProperty(TraitProperties property) {
            property.IsClassTrait = true;
            return property;
        }

        public class EvolutionFeat : TrueFeat {
            public QEffect? EffectOnEidolon { get; private set; }
            public EvolutionFeat(FeatName featName, int level, string flavourText, string rulesText, Trait[] traits, QEffect effect, List<Feat>? subfeats) : base(featName, level, flavourText, rulesText, new Trait[] { tSummoner, tEvolution }.Concat(traits).ToArray(), subfeats) {
                EffectOnEidolon = effect;
            }

        }

        public class EidolonBond : Feat {
            public EidolonBond(FeatName featName, string flavourText, string rulesText, Trait spellList, List<FeatName> skills, Func<Feat, bool> alignmentOptions, List<Trait> traits, List<Feat> subfeats) : base(featName, flavourText, rulesText, traits, subfeats) {
                Init(spellList, skills, alignmentOptions);
            }
            
            public EidolonBond(FeatName featName, string flavourText, string rulesText, Trait spellList, List<FeatName> skills, Func<Feat, bool> alignmentOptions) : base(featName, flavourText, rulesText, new List<Trait>() { tSummoner }, null) {
                Init(spellList, skills, alignmentOptions);
            }

            private void Init(Trait spellList, List<FeatName> skills, Func<Feat, bool> alignmentOptions) {
                this.OnSheet = (Action<CalculatedCharacterSheetValues>)(sheet => {
                    sheet.AddSelectionOption((SelectionOption)new SingleFeatSelectionOption("EidolonAlignment", "EidolonAlignment", 1, alignmentOptions));
                    sheet.AddSelectionOption(new SingleFeatSelectionOption("EidolonPrimaryWeaponStats", "Eidolon Primary Weapon Stats", 1, (Func<Feat, bool>)(ft => ft.HasTrait(tPrimaryAttackStats))));
                    sheet.AddSelectionOption(new SingleFeatSelectionOption("EidolonPrimaryWeapon", "Eidolon Primary Natural Weapon", 1, (Func<Feat, bool>)(ft => ft.HasTrait(tPrimaryAttackType))));
                    sheet.AddSelectionOption(new SingleFeatSelectionOption("EidolonSecondaryWeapon", "Eidolon Secondary Natural Weapon", 1, (Func<Feat, bool>)(ft => ft.HasTrait(tSecondaryAttackType))));
                    sheet.SpellTraditionsKnown.Add(spellList);
                    sheet.SpellRepertoires.Add(tSummoner, new SpellRepertoire(Ability.Charisma, spellList));
                    sheet.SetProficiency(Trait.Spell, Proficiency.Trained);
                    foreach (FeatName skill in skills) {
                        sheet.GrantFeat(skill);
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
                    repertoire.SpellsKnown.Add(AllSpells.CreateModernSpellTemplate(spells[SummonerSpellId.EidolonBoost], tSummoner, sheet.MaximumSpellLevel));
                });
            }
        }

        private static string PrintEidolonStatBlock(FeatName bond, int[] abilityScores, int ac, int dexCap) {
            string text =
                "{b}Perception{/b} " + (abilityScores[4] + 3) +
                "\n{b}Skills{/b} Shares all your skill proficiancies\n" +
                "\nStr " + abilityScores[0] + " Dex " + abilityScores[1] + " Con " + abilityScores[2] + " Int " + abilityScores[3] + " Wis " + abilityScores[4] + " Cha " + abilityScores[5] + "\n" +
                "\n{b}{DarkRed}DEFENSE{/b}{/}\n" +
                "{b}AC{/b} " + (10 + ac + Math.Min(abilityScores[1], dexCap)) + "; {b}Fort{/b} " + (5 + abilityScores[2]) + "; {b}Ref{/b} " + (3 + abilityScores[1]) + "; {b}Will{/b} " + (4 + abilityScores[4]) +
                "\n{b}AC{/b} Share's your HP pool\n\n" +
                "{b}{DarkRed}OFFENSE{/b}{/}\n" +
                "{b}Speed{/b} 25 feet\n";

            string actions =
                "{b}Strke (Primary){/b} {icon:Action} +" + (abilityScores[0] + 3) + " [variable] 1dx" + (abilityScores[0] >= 0 ? " +" : " ") + abilityScores[0] + " variable damage\n" +
                "{b}Strke (Secondary){/b} {icon:Action} +" + (Math.Max(abilityScores[0], abilityScores[0]) + 3) + " [agile]" + (abilityScores[0] >= 0 ? " +" : " ") + abilityScores[0] + " variable damage\n";
                
            if (bond == scDraconicEidolonCunning || bond == scDraconicEidolonMarauding) {
                actions += "{b}Breath Weapon{/b} Your eidolon exhales a line or cone of energy and deal 2d6 of the damage associated with your eidolon's dragon type to each target. You can't use breath weapon again for 1d4 rounds.\n";
            }

            actions += "{b}Act Together{/b} {icon:FreeAction} Your eidolon's next action grants you an immediate bonus tandem turn, where you can make a single action.\n";

            string abilities =
                "\n{b}{DarkRed}ABILITIES{/b}{/}\n" +
                "{b}Eidolon Bond.{/b} You and your eidolon share your actions and multiple attack penalty. Each round, you can use any of your actions (including reactions and free actions) for yourself or your eidolon. Your eidolon gains all of your skill proficiancies and uses your spell attack and save DC for its special abilities.";

            if (bond == scAngelicEidolonAvenger || bond == scAngelicEidolonEmmissary) {
                abilities += "\n{b}Hallowed Strikes.{/b} Your eidolon's unarmed strikes deal +1 extra good damage.\n";
            }
            return text + actions + abilities;
        }


        private static Feat CreateEidolonFeat(FeatName featName, string flavorText, IllustrationName token, int[] abilityScores, int ac, int dexCap) {
            return new Feat(featName, flavorText, "Your eidolon has the following characteristics at level 1:\n\n" + PrintEidolonStatBlock(featName, abilityScores, ac, dexCap), new List<Trait>() { }, (List<Feat>)null)
            .WithIllustration(token)
            .WithOnCreature((Action<CalculatedCharacterSheetValues, Creature>)((sheet, summoner) => summoner
            .AddQEffect(new ActionShareEffect() {
                Id = qfSharedActions,
            })
            .AddQEffect(new QEffect() {
                ProvideMainAction = (Func<QEffect, Possibility>)(qfActTogether => {
                    Creature? eidolon = GetEidolon(qfActTogether.Owner);
                    return GenerateActTogetherAction(qfActTogether.Owner, eidolon, qfActTogether.Owner);
                }),
            })
            .AddQEffect(new QEffect("Eidolon", "This character can summon and command an Eidolon.") {
                StartOfCombat = (Func<QEffect, Task>)(async qfSummonerTechnical => {
                    Creature eidolon = CreateEidolon(featName, token, abilityScores, ac, dexCap, summoner);
                    eidolon.MainName = qfSummonerTechnical.Owner.Name + "'s " + eidolon.MainName;
                    eidolon.AddQEffect(new HPShareEffect(eidolon) {
                        Id = qfSummonerBond,
                        Source = summoner
                    });
                    summoner.AddQEffect(new HPShareEffect(summoner) {
                        Id = qfSummonerBond,
                        Source = eidolon
                    });

                    // Share item bonuses
                    List<Item> wornItems = summoner.CarriedItems.Where(item => item.IsWorn == true && item.HasTrait(Trait.Invested) && item.PermanentQEffectActionWhenWorn != null).ToList<Item>();

                    // TODO: Handle armour bonuses in beta update
                    //Item? armour = summoner.BaseArmor;
                    //if (armour != null) {
                    //    armour.ItemModifications

                    //    eidolon.
                    //}

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
                    if (handwraps != null) {
                        Item eidolonHandwraps = handwraps.Duplicate();
                        eidolon.CarriedItems.Add(eidolonHandwraps);
                        eidolonHandwraps.IsWorn = true;
                        //eidolon.CarriedItems.Find(item => item == eidolonHandwraps);

                        //// Add item bonuses
                        //if (summoner.PersistentCharacterSheet.Calculated.AllFeats.FirstOrDefault((Func<Feat, bool>)(ft => ft.HasTrait(tPrimaryAttackStats))).FeatName == ftPSPowerful) {
                        //    eidolon.AddQEffect(new QEffect() {
                        //        BonusToSkillChecks = (Func<Skill, CombatAction, Creature?, Bonus?>)((skill, action, creature) => {
                        //            if (new ActionId[] { ActionId.Trip, ActionId.Disarm, ActionId.Shove }.Contains(action.ActionId)) {
                        //                return new Bonus(1, BonusType.Item, handwraps.Name);
                        //            }
                        //            return null;
                        //        })
                        //    });
                        //}
                    }
                    summoner.Battle.SpawnCreature(eidolon, summoner.OwningFaction, summoner.Occupies);

                    foreach (QEffect qf in eidolon.QEffects) {
                        if (qf.StartOfCombat != null)
                            await qf.StartOfCombat(qf);
                    }
                }),
                StartOfYourTurn = (Func<QEffect, Creature, Task>)(async (qfStartOfTurn, summoner) => {
                    Creature eidolon = GetEidolon(summoner);
                    summoner.PersistentUsedUpResources.UsedUpActions.Remove("Act Together");

                    await (Task)eidolon.Battle.GameLoop.GetType().GetMethod("StartOfTurn", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static).Invoke(eidolon.Battle.GameLoop, new object[] { eidolon });

                    await eidolon.Battle.GameLoop.StateCheck();
                }),
                EndOfYourTurn = (Func<QEffect, Creature, Task>)(async (qfEndOfTurn, summoner) => {
                    Creature eidolon = GetEidolon(summoner);
                    eidolon.Actions.ForgetAllTurnCounters();
                    await eidolon.Battle.GameLoop.EndOfTurn(eidolon);
                }),
                EndOfCombat = (Func<QEffect, bool, Task>)(async (qfCleanup, won) => {
                    summoner.PersistentUsedUpResources.UsedUpActions.Remove("Act Together");
                }),
                ProvideMainAction = (Func<QEffect, Possibility>)(qfSummoner => {
                    Creature? eidolon = GetEidolon(qfSummoner.Owner);
                    if (eidolon == null || !eidolon.Actions.CanTakeActions() || qfSummoner.Owner.QEffects.FirstOrDefault(qf => qf.Id == qfActTogether) != null)
                        return (Possibility)null;

                    return (Possibility)(ActionPossibility)new CombatAction(qfSummoner.Owner, eidolon.Illustration, "Command your Eidolon", new Trait[] { Trait.Basic, tSummoner }, "Swap to Eidolon.", (Target)Target.Self()) {
                        ShortDescription = "Take control of your Eidolon, using your shared action pool."
                    }
                        .WithEffectOnSelf((Func<Creature, Task>)(async self => {
                            await PartnerActs(summoner, eidolon, false);
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

                    HPShareEffect shareHP = (HPShareEffect)qfHealOrHarm.Owner.QEffects.FirstOrDefault<QEffect>((Func<QEffect, bool>)(qf => qf.Id == qfSummonerBond && qf.Source == GetEidolon(qfHealOrHarm.Owner)));
                    HPShareEffect eidolonShareHP = (HPShareEffect)GetEidolon(qfHealOrHarm.Owner).QEffects.FirstOrDefault<QEffect>((Func<QEffect, bool>)(qf => qf.Id == qfSummonerBond && qf.Source == qfHealOrHarm.Owner));

                    if (action == shareHP.CA || action == eidolonShareHP.CA) {
                        return;
                    }

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

                    await HandleHealthShare(summoner, eidolon, action, SummonerClassEnums.InterceptKind.TARGET);
                }),
                EndOfAnyTurn = (Action<QEffect>)(qfHealOrHarm => {
                    HPShareEffect shareHP = (HPShareEffect)qfHealOrHarm.Owner.QEffects.FirstOrDefault<QEffect>((Func<QEffect, bool>)(qf => qf.Id == qfSummonerBond));
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

                    HPShareEffect shareHP = (HPShareEffect)qfPreHazardDamage.Owner.QEffects.FirstOrDefault<QEffect>((Func<QEffect, bool>)(qf => qf.Id == qfSummonerBond));

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

                    await HandleHealthShare(summoner, eidolon, action, SummonerClassEnums.InterceptKind.DAMAGE);
                }),
                AfterYouTakeAction = (Func<QEffect, CombatAction, Task>)(async (qf, action) => {
                    Creature summoner = qf.Owner;
                    Creature eidolon = GetEidolon(summoner);
                    //eidolon.Actions.UseUpActions(action.ActionCost, )
                    if (eidolon != null && action.Traits.Contains(Trait.Attack)) {
                        eidolon.Actions.AttackedThisManyTimesThisTurn += 1;
                    }
                })
            })
            .AddQEffect(new QEffect() {
                ProvideMainAction = (Func<QEffect, Possibility>)(qfManifestEidolon => {
                    Creature? eidolon = GetEidolon(qfManifestEidolon.Owner);
                    QEffect actTogether = new QEffect("Recently Manifested", "Immediately take a single 1 cost action.") {
                        Illustration = IllustrationName.Haste,
                        Id = qfActTogether,
                    };
                    if (eidolon == null) {
                        return (Possibility)null;
                    }

                    Trait spellList = sheet.SpellRepertoires[tSummoner].SpellList;

                    if (eidolon.Destroyed) {
                        return (Possibility)(ActionPossibility)new CombatAction(qfManifestEidolon.Owner, eidolon.Illustration, "Manifest Eidolon", new Trait[] {
                                tSummoner, Trait.Concentrate, Trait.Conjuration, Trait.Manipulate, Trait.Teleportation, spellList
                        },
                            "Your eidolon appears in an open space adjacent to you, and can then take a single action.\r\n\r\nThe conduit that allows your eidolon to manifest is also a tether between you." +
                            "If you are reduced to 0 Hit Points, your eidolon's physical form dissolves: your eidolon unmanifests, and you need to use Manifest Eidolon to manifest it again.", (Target)Target.RangedEmptyTileForSummoning(1)) {
                            ShortDescription = "SHORT DESC."
                        }
                        .WithEffectOnChosenTargets((Func<Creature, ChosenTargets, Task>)(async (self, targets) => {
                            eidolon.Battle.Corpses.Remove(eidolon);
                            eidolon.Occupies = targets.ChosenTile;
                            eidolon.RemoveAllQEffects(qf => qf.Illustration != null);
                            eidolon.AddQEffect(actTogether);
                            eidolon.Battle.SpawnCreature(eidolon, self.OwningFaction, targets.ChosenTile);
                            eidolon.Actions.GetType().GetMethod("AnimateActionUsedTo", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public).Invoke(eidolon.Actions, new object[] { 0, ActionDisplayStyle.UsedUp });
                            eidolon.Actions.GetType().GetMethod("AnimateActionUsedTo", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public).Invoke(eidolon.Actions, new object[] { 1, ActionDisplayStyle.UsedUp });
                            eidolon.Actions.GetType().GetMethod("AnimateActionUsedTo", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public).Invoke(eidolon.Actions, new object[] { 2, ActionDisplayStyle.UsedUp });
                            eidolon.Actions.GetType().GetMethod("AnimateActionUsedTo", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public).Invoke(eidolon.Actions, new object[] { 3, ActionDisplayStyle.Invisible });
                            eidolon.Destroyed = false;
                            eidolon.Actions.ActionsLeft = 0;
                            eidolon.Actions.UsedQuickenedAction = true;
                            // Balance HP
                            HPShareEffect shareHP = (HPShareEffect)summoner.QEffects.FirstOrDefault<QEffect>((Func<QEffect, bool>)(qf => qf.Id == qfSummonerBond));
                            if (eidolon.HP < summoner.HP) {
                                eidolon.Heal($"{summoner.HP - eidolon.HP}", shareHP.CA);
                            } else if (eidolon.HP > summoner.HP) {
                                await summoner.DealDirectDamage(shareHP.CA, DiceFormula.FromText($"{eidolon.HP - summoner.HP}"), eidolon, CheckResult.Success, DamageKind.Untyped);
                            }
                            await eidolon.Battle.GameLoop.StateCheck();
                            await PartnerActs(summoner, eidolon, true);
                            eidolon.RemoveAllQEffects(effect => effect == actTogether);
                        }))
                        .WithActionCost(3);
                    } else {
                        return (Possibility)(ActionPossibility)new CombatAction(qfManifestEidolon.Owner, illDismiss, "Dismiss Eidolon", new Trait[] {
                            tSummoner, Trait.Concentrate, Trait.Conjuration, Trait.Manipulate, Trait.Teleportation, spellList
                        },
                            "Dismiss your eidolon, protecting it and yourself from harm.", Target.RangedFriend(20).WithAdditionalConditionOnTargetCreature((CreatureTargetingRequirement)new EidolonCreatureTargetingRequirement(qfSummonerBond)))
                        .WithEffectOnChosenTargets((Func<Creature, ChosenTargets, Task>)(async (self, targets) => {
                            self.Battle.RemoveCreatureFromGame(eidolon);
                        }))
                        .WithActionCost(3);
                    }
                }),
            })
            ));
        }

        //++combatActionExecution.user.Actions.AttackedThisManyTimesThisTurn

        private static Creature CreateEidolon(FeatName featName, IllustrationName token, int[] abilityScores, int ac, int dexCap, Creature summoner) {
            Creature eidolon = CreateEidolonBase(token, "Eidolon", summoner, abilityScores, ac, dexCap);

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

            Illustration pIcon = pAttack.Illustration;
            Illustration sIcon = sAttack.Illustration;

            Illustration test = IllustrationName.Tail;

            eidolon.WithUnarmedStrike(new Item(pIcon, pAttack.Name.ToLower(), pStats.ToArray()).WithWeaponProperties(new WeaponProperties(damage, primaryDamageType)));
            eidolon.WithAdditionalUnarmedStrike(new Item(sIcon, sAttack.Name.ToLower(), new Trait[] { Trait.Unarmed, Trait.Agile, Trait.Finesse }).WithWeaponProperties(new WeaponProperties("1d6", secondaryDamageType)));

            var evoFeats = summoner.PersistentCharacterSheet.Calculated.AllFeats.Where(ft => ft.HasTrait(tEvolution)).ToArray();
            evoFeats = Array.ConvertAll(evoFeats, ft => (EvolutionFeat)ft);

            eidolon.AddQEffect(new QEffect() {
                ProvideMainAction = (Func<QEffect, Possibility>)(qfActTogether => {
                    Creature? summoner = GetSummoner(qfActTogether.Owner);
                    return GenerateActTogetherAction(qfActTogether.Owner, summoner, summoner);
                }),
            })
            .AddQEffect(new QEffect() {
                ProvideMainAction = (Func<QEffect, Possibility>)(qfEidolon => {
                    Creature? summoner = GetSummoner(qfEidolon.Owner);
                    if (summoner == null || !summoner.Actions.CanTakeActions() || qfEidolon.Owner.QEffects.FirstOrDefault(qf => qf.Id == qfActTogether) != null)
                        return (Possibility)null;
                    return (Possibility)(ActionPossibility)new CombatAction(qfEidolon.Owner, summoner.Illustration, "Return Control",
                        new Trait[] { Trait.Basic, tSummoner }, $"Switch back to controlling {summoner.Name}. All unspent actions will be retained.", (Target)Target.Self((Func<Creature, AI, float>)((cr, ai) =>
                        (float)int.MinValue))).WithActionCost(0).WithActionId(ActionId.EndTurn).WithEffectOnSelf((Action<Creature>)(self => {
                            // Remove act together toggle on eidolon
                            self.RemoveAllQEffects(qf => qf.Id == qfActTogetherToggle);
                            // Remove and log actions
                            ActionShareEffect actionShare = (ActionShareEffect)self.QEffects.FirstOrDefault(qf => qf.Id == qfSharedActions);
                            actionShare.LogTurnEnd(self.Actions);
                            self.Actions.UsedQuickenedAction = true;
                            self.Actions.ActionsLeft = 0;
                            self.Actions.WishesToEndTurn = true;
                        }));
                })
            });

            // Add sbuclasses
            if (featName == scAngelicEidolonAvenger || featName == scAngelicEidolonEmmissary) {
                eidolon.AddQEffect(new QEffect("Hallowed Strikes", "Your eidolon's unarmed strikes deal +1 extra good damage.") {
                    AddExtraKindedDamageOnStrike = (action, target) => {
                        return new KindedDamage(DiceFormula.FromText("1", "Hallowed Strikes"), DamageKind.Good);
                    }
                    //YouDealDamageWithStrike = (Delegates.YouDealDamageWithStrike)((qf, action, diceFormula, defender) =>
                    //{
                    //    if (defender.HasTrait(Trait.Evil)) {
                    //        DiceFormula extraDamage = DiceFormula.FromText("1", "Hallowed Strikes");
                    //        return (DiceFormula)diceFormula.Add(extraDamage);
                    //    }
                    //    return (DiceFormula)diceFormula;
                    //})
                });
            } else if (featName == scDraconicEidolonCunning || featName == scDraconicEidolonMarauding) {
                SpellRepertoire repertoire = summoner.PersistentCharacterSheet.Calculated.SpellRepertoires[tSummoner];
                int saveDC = summoner.GetOrCreateSpellcastingSource(SpellcastingKind.Spontaneous, tSummoner, Ability.Charisma, repertoire.SpellList).GetSpellSaveDC();

                Trait damageTrait = summoner.PersistentCharacterSheet.Calculated.AllFeats.FirstOrDefault((Func<Feat, bool>)(ft => ft.HasTrait(tDragonType))).Traits[0];
                FeatName targetFeat = summoner.PersistentCharacterSheet.Calculated.AllFeats.FirstOrDefault((Func<Feat, bool>)(ft => ft.HasTrait(tBreathWeaponArea))).FeatName;
                Target target;
                if (targetFeat == ftBreathWeaponLine) {
                    target = Target.Cone(6);
                } else {
                    target = Target.Line(12);
                }
                DamageKind damageKind = DamageKind.Fire;
                Defense save;
                string damageName = "ERROR";
                if (damageTrait == Trait.Fire) {
                    damageKind = DamageKind.Fire;
                    save = Defense.Reflex;
                    damageName = "fire";
                } else if (damageTrait == Trait.Cold) {
                    damageKind = DamageKind.Cold;
                    save = Defense.Reflex;
                    damageName = "cold";
                } else if (damageTrait == Trait.Acid) {
                    damageKind = DamageKind.Acid;
                    save = Defense.Reflex;
                    damageName = "acid";
                } else if (damageTrait == Trait.Poison) {
                    damageKind = DamageKind.Poison;
                    save = Defense.Fortitude;
                    damageName = "poison";
                } else if (damageTrait == Trait.Mental) {
                    damageKind = DamageKind.Mental;
                    save = Defense.Will;
                    damageName = "mental";
                } else if (damageTrait == Trait.Electricity) {
                    damageKind = DamageKind.Electricity;
                    save = Defense.Reflex;
                    damageName = "electricity";
                } else if (damageTrait == Trait.Good) {
                    damageKind = DamageKind.Good;
                    save = Defense.Reflex;
                    damageName = "good";
                } else if (damageTrait == Trait.VersatileP) {
                    damageKind = DamageKind.Piercing;
                    save = Defense.Reflex;
                    damageName = "piercing";
                }

                Trait[] traits = new Trait[] { };
                if (damageTrait != Trait.VersatileP) {
                    traits = new Trait[] { damageTrait };
                }

                eidolon.AddQEffect(new QEffect() {
                    ProvideMainAction = (Func<QEffect, Possibility>)(qfEidolon => {
                        Creature summoner = GetSummoner(qfEidolon.Owner);
                        int dice = 1 + (summoner.Level + 1) / 2;
                        return (Possibility)(ActionPossibility)new CombatAction(qfEidolon.Owner, IllustrationName.BreathWeapon, "Breath Weapon", traits, "Your eidolon exhales a breath of destructive energy. Deal " + dice + "d6 " + damageName + " damage to each creature in the area.\n\n{b}Special.{/b}You can't use breath weapon again for 1d4 rounds.\n\nAt 3rd level, and every 2 levels thereafter, damage increases by 1d6.", target)
                        .WithSavingThrow(new SavingThrow(Defense.Reflex, (_ => saveDC)))
                        .WithEffectOnEachTarget(async (action, self, target, checkResult) => {
                            await CommonSpellEffects.DealBasicDamage(action, self, target, checkResult, $"{dice}d6", damageKind);
                        })
                        .WithEffectOnChosenTargets(async(self, defenders) => {
                            int num = R.Next(1, 4);
                            self.AddQEffect(new QEffect("Recharging Fire Breath", "This creature can't use Fire Breath until the value counts down to zero.", ExpirationCondition.CountsDownAtEndOfYourTurn, self, (Illustration)IllustrationName.Recharging) {
                                Id = QEffectId.Recharging,
                                CountsAsADebuff = true,
                                PreventTakingAction = (Func<CombatAction, string>)(ca => !(ca.Name == "Breath Weapon") ? (string)null : "This ability is recharging."),
                                Value = num
                            });
                        })
                        .WithSoundEffect(SfxName.Fireball);
                }),
                });
            } else {
                throw new Exception("ERROR: Invalid eidolon bond chosen. Please check trait names.");
            }

            foreach (EvolutionFeat feat in evoFeats) {
                if (feat.EffectOnEidolon != null) {
                    eidolon.AddQEffect(feat.EffectOnEidolon);
                }
            }

                //List<Item> wornItems = summoner.CarriedItems.Where(item => item.IsWorn == true && item.HasTrait(Trait.Invested) && item.PermanentQEffectActionWhenWorn != null).ToList<Item>();


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
            List<Trait> alignment = summoner.PersistentCharacterSheet.Calculated.AllFeats.FirstOrDefault((Func<Feat, bool>)(ft => ft.HasTrait(tAlignment))).Traits;
            List<Trait> traits = new List<Trait>();
            traits.Add(tEidolon);
            for (int i = 1; i < alignment.Count; i++) {
                traits.Add(alignment[i]);
            }
            int perception = wisdom + num + level;
            int speed1 = 5;
            Defenses defenses = new Defenses(10 + ac + (dexterity < dexCap ? dexterity : dexCap) + num, constitution + num + num, dexterity + num, wisdom + num + num);
            int hp = summoner.MaxHP;
            Skills skills = summoner.Skills;
            Abilities abilities2 = abilities1;
            return new Creature(illustration1, name1, (IList<Trait>)traits, level, perception, speed1, defenses, hp, abilities2, skills).WithProficiency(Trait.Unarmed, Proficiency.Trained).WithEntersInitiativeOrder(false).WithProficiency(Trait.UnarmoredDefense, Proficiency.Trained)
                .AddQEffect(new ActionShareEffect() {
                    Id = qfSharedActions,
                })
                .AddQEffect(new QEffect("Eidolon Bond", "You and your eidolon share your actions and multiple attack penalty. Each round, you can use any of your actions (including reactions and free actions) for yourself or your eidolon. " +
                "Your eidolon gains all of your skill proficiancies and uses your spell attack and save DC for its special abilities.") {
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
                        HPShareEffect shareHP = (HPShareEffect)qfHealOrHarm.Owner.QEffects.FirstOrDefault<QEffect>((Func<QEffect, bool>)(qf => qf.Id == qfSummonerBond));
                        HPShareEffect summonerShareHP = (HPShareEffect)summoner.QEffects.FirstOrDefault<QEffect>((Func<QEffect, bool>)(qf => qf.Id == qfSummonerBond && qf.Source == qfHealOrHarm.Owner));

                        if (action == shareHP.CA || action == summonerShareHP.CA) {
                            return;
                        }

                        shareHP.LogAction(qfHealOrHarm.Owner.HP, action, action.Owner, SummonerClassEnums.InterceptKind.TARGET);
                    }),
                    AfterYouAreTargeted = (Func<QEffect, CombatAction, Task>)(async (qfShareHP, action) => {
                        Creature summoner = GetSummoner(qfShareHP.Owner);
                        Creature eidolon = qfShareHP.Owner;

                        await HandleHealthShare(eidolon, summoner, action, SummonerClassEnums.InterceptKind.TARGET);
                    }),
                    EndOfAnyTurn = (Action<QEffect>)(qfEndOfTurn => {
                        HPShareEffect shareHP = (HPShareEffect)qfEndOfTurn.Owner.QEffects.FirstOrDefault<QEffect>((Func<QEffect, bool>)(qf => qf.Id == qfSummonerBond));
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

                        HPShareEffect shareHP = (HPShareEffect)qfPreHazardDamage.Owner.QEffects.FirstOrDefault<QEffect>((Func<QEffect, bool>)(qf => qf.Id == qfSummonerBond));

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
                        Creature summoner = GetSummoner(qfPostHazardDamage.Owner);
                        Creature eidolon = qfPostHazardDamage.Owner;

                        await HandleHealthShare(eidolon, summoner, action, SummonerClassEnums.InterceptKind.DAMAGE);
                    }),
                    BonusToSkillChecks = (Func<Skill, CombatAction, Creature?, Bonus?>)((skill, action, creature) => {
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

                        Item? handwraps = StrikeRules.GetBestHandwraps(action.Owner);
                        if (handwraps != null) {
                            return new Bonus(1, BonusType.Item, handwraps.Name);
                        } else {
                            string source = "";
                            int bonus = 0;

                            Creature summoner = GetSummoner(action.Owner);

                            Item? mainHand = summoner.PrimaryItem;
                            Item? offHand = summoner.SecondaryItem;

                            if (mainHand != null && mainHand.WeaponProperties != null) {
                                bonus = mainHand.WeaponProperties.ItemBonus;
                                source = mainHand.Name;
                            }
                            if (offHand != null && offHand.WeaponProperties != null && offHand.WeaponProperties.ItemBonus > bonus) {
                                bonus = offHand.WeaponProperties.ItemBonus;
                                source = offHand.Name;
                            }

                            if (bonus > 0)
                                return new Bonus(bonus, BonusType.Item, source);
                            else
                                return null;
                        }
                    }),
                    BonusToAttackRolls = ((qf, action, self) => {
                        if (!action.Name.StartsWith("Strike (")) {
                            return null;
                        }

                        string source = "";
                        int bonus = 0;

                        Item? mainHand = summoner.PrimaryItem;
                        Item? offHand = summoner.SecondaryItem;

                        if (mainHand != null && mainHand.WeaponProperties != null) {
                            bonus = mainHand.WeaponProperties.ItemBonus;
                            source = mainHand.Name;
                        }
                        if (offHand != null && offHand.WeaponProperties != null && offHand.WeaponProperties.ItemBonus > bonus) {
                            bonus = offHand.WeaponProperties.ItemBonus;
                            source = offHand.Name;
                        }

                        if (bonus > 0)
                            return new Bonus(bonus, BonusType.Item, source);
                        else
                            return null;
                    }),
                    YouDealDamageWithStrike = ((qf, action, diceFormula, defender) => {
                        int dice = 1;

                        Item? mainHand = summoner.PrimaryItem;
                        Item? offHand = summoner.SecondaryItem;

                        if (mainHand != null && mainHand.WeaponProperties != null) {
                            dice = mainHand.WeaponProperties.DamageDieCount;
                        }
                        if (offHand != null && offHand.WeaponProperties != null) {
                            dice = offHand.WeaponProperties.DamageDieCount > dice ? offHand.WeaponProperties.DamageDieCount : dice;
                        }

                        int currDice = diceFormula.ToString()[0] - '0';

                        if (currDice >= dice) {
                            return DiceFormula.FromText(diceFormula.ToString());
                        }

                        string output = diceFormula.ToString();
                        char[] ch = output.ToCharArray();
                        ch[0] = dice.ToString().ToCharArray()[0];

                        return DiceFormula.FromText(new string(ch));
                    }),
                    AfterYouTakeAction = (Func<QEffect, CombatAction, Task>)(async (qf, action) => {
                        Creature eidolon = qf.Owner;
                        if (summoner != null && action.Traits.Contains(Trait.Attack)) {
                            summoner.Actions.AttackedThisManyTimesThisTurn += 1;
                        }
                    })
                });
        }

        private async static Task PartnerActs(Creature self, Creature partner) {
            await PartnerActs(self, partner, false);
        }

        private async static Task PartnerActs(Creature self, Creature partner, bool tandem) {
            self.RemoveAllQEffects(qf => qf.Id == qfActTogetherToggle);

            if (partner.OwningFaction.IsEnemy)
                await Task.Delay(500);
            Creature oldActiveCreature = partner.Battle.ActiveCreature;
            await partner.Battle.GameLoop.StateCheck();
            partner.Battle.ActiveCreature = partner;
            Action<Tile> centerIfNotVisible = partner.Battle.SmartCenterIfNotVisible;
            if (centerIfNotVisible != null)
                centerIfNotVisible(partner.Occupies);
            await partner.Battle.GameLoop.StateCheck();
            //Set remaining actions for partner
            bool quickenedActionState = partner.Actions.UsedQuickenedAction;
            ActionDisplayStyle[] actionDisplays = new ActionDisplayStyle[4] { partner.Actions.FirstActionStyle, partner.Actions.SecondActionStyle, partner.Actions.ThirdActionStyle, partner.Actions.FourthActionStyle };
            int partnerActionsLeft = partner.Actions.ActionsLeft;
            if (tandem) {
                partner.Actions.ActionsLeft = 1;
                partner.Actions.UsedQuickenedAction = true;
                partner.Actions.GetType().GetMethod("AnimateActionUsedTo", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public).Invoke(partner.Actions, new object[] { 0, ActionDisplayStyle.Summoned });
                partner.Actions.GetType().GetMethod("AnimateActionUsedTo", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public).Invoke(partner.Actions, new object[] { 1, ActionDisplayStyle.Summoned });
                partner.Actions.GetType().GetMethod("AnimateActionUsedTo", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public).Invoke(partner.Actions, new object[] { 2, ActionDisplayStyle.Available });
                partner.Actions.GetType().GetMethod("AnimateActionUsedTo", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public).Invoke(partner.Actions, new object[] { 3, ActionDisplayStyle.Invisible });
            } else {
                partner.Actions.UseUpActions(partner.Actions.ActionsLeft - self.Actions.ActionsLeft, ActionDisplayStyle.UsedUp);
            }
            if (partner.OwningFaction.IsHumanControlled)
                Sfxs.Play(SfxName.StartOfTurn, 0.1f);
            await partner.Battle.GameLoop.StateCheck();
            // Process partner's turn
            await (Task)partner.Battle.GameLoop.GetType().GetMethod("SubActionPhase", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static).Invoke(partner.Battle.GameLoop, new object[] { partner });
            // Reset partner's actions
            if (tandem) {
                // Reset actions
                partner.Actions.ActionsLeft = partnerActionsLeft;
                partner.Actions.UsedQuickenedAction = quickenedActionState;
                for (int i = 0; i < actionDisplays.Count(); i++) {
                    partner.Actions.GetType().GetMethod("AnimateActionUsedTo", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public).Invoke(partner.Actions, new object[] { i, actionDisplays[i] });
                }
            } else {
                ActionShareEffect actionTracker = (ActionShareEffect)partner.QEffects.FirstOrDefault(qf => qf.Id == qfSharedActions);
                if (actionTracker.ResetRequired) {
                    partner.Actions.ActionsLeft = actionTracker.ActionTally;
                    partner.Actions.UsedQuickenedAction = actionTracker.UsedQuickenedAction;
                    actionTracker.Clear();
                }
                // Update own actions
                self.Actions.UseUpActions(self.Actions.ActionsLeft - partner.Actions.ActionsLeft, ActionDisplayStyle.UsedUp);
            }
            await partner.Battle.GameLoop.StateCheck();
            partner.Actions.WishesToEndTurn = false;
            await partner.Battle.GameLoop.StateCheck();
            partner.Battle.ActiveCreature = oldActiveCreature;
            oldActiveCreature = (Creature)null;
        }

        private async static Task HandleHealthShare(Creature self, Creature partner, CombatAction action, SummonerClassEnums.InterceptKind interceptKind) {
            HPShareEffect selfShareHP = (HPShareEffect)self.QEffects.FirstOrDefault<QEffect>((Func<QEffect, bool>)(qf => qf.Id == qfSummonerBond && qf.Source == partner));
            HPShareEffect partnerShareHP = (HPShareEffect)partner.QEffects.FirstOrDefault<QEffect>((Func<QEffect, bool>)(qf => qf.Id == qfSummonerBond && qf.Source == self));

            if (selfShareHP.Type != interceptKind) {
                return;
            }

            if (action == selfShareHP.CA || action == partnerShareHP.CA) {
                return;
            }

            if (partnerShareHP.CompareEffects(selfShareHP)) {
                // Same effect
                if (selfShareHP.HealOrHarm(self.HP) == SummonerClassEnums.EffectKind.HARM) {
                    if (partner.HP < self.HP) {
                        int damage = self.HP - partner.HP;
                        await partner.DealDirectDamage(partnerShareHP.CA, DiceFormula.FromText($"{damage}"), self, CheckResult.Success, DamageKind.Untyped);
                    } else if (partner.HP > self.HP) {
                        int damage = partner.HP - self.HP;
                        await self.DealDirectDamage(selfShareHP.CA, DiceFormula.FromText($"{damage}"), partner, CheckResult.Success, DamageKind.Untyped);
                    }
                } else if (selfShareHP.HealOrHarm(self.HP) == SummonerClassEnums.EffectKind.HEAL) {
                    if (partner.HP < self.HP) {
                        int healing = self.HP - partner.HP;
                        partner.Heal($"{healing}", selfShareHP.CA);
                    } else if (partner.HP > self.HP) {
                        int healing = partner.HP - self.HP;
                        self.Heal($"{healing}", partnerShareHP.CA);
                    }
                }
            } else {
                // Invividual effect
                if (selfShareHP.HealOrHarm(self.HP) == SummonerClassEnums.EffectKind.HARM) {
                    int damage = partner.HP - self.HP;
                    await self.DealDirectDamage(selfShareHP.CA, DiceFormula.FromText($"{damage}"), partner, CheckResult.Success, DamageKind.Untyped);
                } else if (selfShareHP.HealOrHarm(self.HP) == SummonerClassEnums.EffectKind.HEAL) {
                    int healing = self.HP - partner.HP;
                    partner.Heal($"{healing}", selfShareHP.CA);
                }
            }
        }

        private static Possibility GenerateActTogetherAction(Creature self, Creature partner, Creature summoner) {
            if (partner == null || !partner.Actions.CanTakeActions() || summoner.PersistentUsedUpResources.UsedUpActions.Contains("Act Together") || self.QEffects.FirstOrDefault(qf => qf.Id == qfActTogether) != null)
                return (Possibility)null;
            if (self.QEffects.FirstOrDefault(qf => qf.Id == qfActTogetherToggle) != null) {
                return (Possibility)(ActionPossibility)new CombatAction(self, illActTogether, "Cancel Act Together",
                new Trait[] { tSummoner, tTandem }, $"Cancel act together toggle.", (Target)Target.Self((Func<Creature, AI, float>)((cr, ai) =>
                (float)int.MinValue))).WithActionCost(0).WithEffectOnSelf((Action<Creature>)(self => {
                    // Remove toggle from self
                    self.RemoveAllQEffects(qf => qf.Id == qfActTogetherToggle);
                }));
            } else {
                return (Possibility)(ActionPossibility)new CombatAction(self, illActTogether, "Enable Act Together",
                new Trait[] { tSummoner, tTandem },
                "{b}Frequency: {/b} once per round\n\n" + (self == summoner ? "Your" : "Your eidolon's") + " next action grants " + (self == summoner ? "your eidolon" : "you") + " an immediate bonus tandem turn, where " + (self == summoner ? "they" : "you") + " they can make a single action.\n\n",
                (Target)Target.Self((Func<Creature, AI, float>)((cr, ai) => (float)int.MinValue))).WithActionCost(0).WithEffectOnSelf((Action<Creature>)(self => {
                    // Give toggle qf to self
                    self.AddQEffect(new QEffect("Act Together Toggled", "Your next action of 1+ cost will also grant a single quickened action to your bonded partner.") {
                        Id = qfActTogetherToggle,
                        ExpiresAt = ExpirationCondition.ExpiresAtEndOfYourTurn,
                        Illustration = illActTogetherStatus,
                        AfterYouTakeAction = (Func<QEffect, CombatAction, Task>)(async (qf, action) => {
                            if (action.ActionCost > 0) {
                                self.RemoveAllQEffects(qf => qf.Id == qfActTogetherToggle);
                                summoner.PersistentUsedUpResources.UsedUpActions.Add("Act Together");
                                QEffect actTogether = new QEffect("Act Together", "Immediately take a single 1 cost action.") {
                                    Illustration = IllustrationName.Haste,
                                    Id = qfActTogether,
                                };
                                partner.AddQEffect(actTogether);
                                await PartnerActs(self, partner, true);
                                partner.RemoveAllQEffects(effect => effect == actTogether);
                            }
                        })
                    });
                }));
            }
        }

    }
}

//Yes, indeed.  To prevent an action from showing up on the stat block, add the trait Trait.Basic to its list of trait. Not too intuitive, sorry.