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
//using static Microsoft.Xna.Framework.Point;
//using static Microsoft.Xna.Framework.Vector2;
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
using System.ComponentModel.Design;
using System.Text;
using static Dawnsbury.Core.CharacterBuilder.FeatsDb.TrueFeatDb.BarbarianFeatsDb.AnimalInstinctFeat;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Diagnostics.Metrics;
//using System.Drawing;
using Microsoft.Xna.Framework.Audio;
using static System.Reflection.Metadata.BlobBuilder;
using Dawnsbury.Core.CharacterBuilder.FeatsDb;
using Dawnsbury.Core.Animations.Movement;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Kineticist;
using Dawnsbury.Core.Noncombat;

namespace Dawnsbury.Mods.Classes.Summoner {

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public class EidolonCreatureTargetingRequirement : CreatureTargetingRequirement {
        public QEffectId qfEidolon { get; }

        public EidolonCreatureTargetingRequirement(QEffectId qf) {
            this.qfEidolon = qf;
        }

        public override Usability Satisfied(Creature source, Creature target) {
            return target.QEffects.FirstOrDefault<QEffect>((Func<QEffect, bool>)(qf => qf.Id == this.qfEidolon && qf.Source == source)) != null ? Usability.Usable : Usability.NotUsableOnThisCreature("This ability can only be used on your eidolon.");
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public class SelectFeySpells : AddToSpellRepertoireOption {

        private bool allowCantrips;

        public SelectFeySpells(string key, string name, int level, Trait classRepertoire, int maxSpellLevel, int maximumNumberOfSpells, bool allowCantrips = false) : base(key, name, level, classRepertoire, Trait.Primal, maxSpellLevel, maximumNumberOfSpells) {
            this.allowCantrips = allowCantrips;
        }

        // What spells are shown as optional
        public override bool Eligible(CalculatedCharacterSheetValues values, Spell spell) {
            Trait[] allowedTraits = new Trait[] { Trait.Illusion, Trait.Enchantment, Trait.Mental };
            List<Trait> traits = spell.Traits.ToList().Where(t => allowedTraits.Contains(t)).ToList();

            if ((!spell.HasTrait(Trait.Primal) && !(traits.Count > 0 && spell.HasTrait(Trait.Arcane))) || spell.HasTrait(Trait.SpellCannotBeChosenInCharacterBuilder) || (spell.HasTrait(Trait.Cantrip) && !allowCantrips))
                return false;
            if (allowCantrips && spell.HasTrait(Trait.Cantrip)) {
                return true;
            } else if (allowCantrips && !spell.HasTrait(Trait.Cantrip)) {
                return false;
            }
            return this.MaximumSpellLevel >= 1 && spell.MinimumSpellLevel <= this.MaximumSpellLevel && !spell.HasTrait(Trait.Cantrip);
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public static class SummonerClassLoader {
        // Traits
        internal static Trait tSummoner = ModManager.RegisterTrait("SummonerTrait", new TraitProperties("Summoner", true) { IsClassTrait = true });
        internal static Trait tEvolution = ModManager.RegisterTrait("EvolutionTrait", new TraitProperties("Evolution", true));
        internal static Trait tTandem = ModManager.RegisterTrait("TandemTrait", new TraitProperties("Tandem", true));
        internal static Trait tEidolon = ModManager.RegisterTrait("EidolonCompanion", new TraitProperties("Eidolon", true));
        internal static Trait tPrimaryAttackType = ModManager.RegisterTrait("EidolonPrimaryWeaponType", new TraitProperties("Eidolon Primary Weapon Type", false));
        internal static Trait tPrimaryAttackStats = ModManager.RegisterTrait("EidolonPrimaryWeaponStats", new TraitProperties("Eidolon Primary Weapon Stats", false));
        internal static Trait tSecondaryAttackType = ModManager.RegisterTrait("EidolonSecondaryWeaponType", new TraitProperties("Eidolon Secondary Weapon Type", false));
        internal static Trait tAlignment = ModManager.RegisterTrait("EidolonAlignment", new TraitProperties("Eidolon Alignment", false));
        internal static Trait tAdvancedWeaponryAtkType = ModManager.RegisterTrait("AdvancedWeaponAttackType", new TraitProperties("Advanced Weaponry Attack Type", false));
        internal static Trait tAdvancedWeaponryAtkTrait = ModManager.RegisterTrait("AdvancedWeaponAttackTrait", new TraitProperties("Advanced Weaponry Attack Trait", false));
        internal static Trait tEnergyHeartDamage = ModManager.RegisterTrait("EnergyHeartDamage", new TraitProperties("Energy Heart Damage Type", false));
        internal static Trait tEnergyHeartWeapon = ModManager.RegisterTrait("EnergyHeartWeapon", new TraitProperties("Energy Heart Weapon", false));
        internal static Trait tGrapple = ModManager.RegisterTrait("SummonerGrapple", new TraitProperties("Grapple", true, "You can add your item bonus to grapple checks made using this weapon."));
        internal static Trait tBreathWeaponArea = ModManager.RegisterTrait("SummonerBreathWeaponArea", new TraitProperties("Breath Weapon Area", false));
        internal static Trait tDragonType = ModManager.RegisterTrait("SummonerDragonType", new TraitProperties("Dragon Type", false));
        internal static Trait tPortrait = ModManager.RegisterTrait("EidolonPortrait", new TraitProperties("Portrait", true));
        internal static Trait tPortraitCategory = ModManager.RegisterTrait("EidolonPortraitCategory", new TraitProperties("Portrait Category", true));
        internal static Trait tOutsider = ModManager.RegisterTrait("EidolonPortraitOutsiderCategory", new TraitProperties("Outsider", true));
        internal static Trait tEidolonASI = ModManager.RegisterTrait("EidolonASIBoost", new TraitProperties("Ability Score Boost", false));
        internal static Trait tParry = ModManager.RegisterTrait("EidolonParryTrait", new TraitProperties("Parry", true, "While wielding this weapon, if your proficiency with it is trained or better, you can spend a single action to position your weapon defensively, gaining a +1 circumstance bonus to AC until the start of your next turn."));
        internal static Trait tEidolonSpellLvl1 = ModManager.RegisterTrait("EidolonSpellLevel1", new TraitProperties("", false));
        internal static Trait tEidolonSpellLvl2 = ModManager.RegisterTrait("EidolonSpellLevel2", new TraitProperties("", false));
        internal static Trait tEidolonSpellFeat = ModManager.RegisterTrait("EidolonSpellFeat", new TraitProperties("", false));
        internal static Trait tEidolonsWrathType = ModManager.RegisterTrait("EidolonsWrathDamageType", new TraitProperties("", false));

        // Feat names
        private static FeatName classSummoner = ModManager.RegisterFeatName("SummonerClass", "Summoner");

        private static FeatName scAngelicEidolon = ModManager.RegisterFeatName("Angel Eidolon");
        private static FeatName scAngelicEidolonAvenger = ModManager.RegisterFeatName("Angelic Avenger");
        private static FeatName scAngelicEidolonEmmissary = ModManager.RegisterFeatName("Angelic Emmisary");

        private static FeatName scDraconicEidolon = ModManager.RegisterFeatName("Dragon Eidolon");
        private static FeatName scDraconicEidolonCunning = ModManager.RegisterFeatName("Cunning Dragon");
        private static FeatName scDraconicEidolonMarauding = ModManager.RegisterFeatName("Marauding Dragon");
        private static FeatName ftBreathWeaponLine = ModManager.RegisterFeatName("Breath Weapon: Line");
        private static FeatName ftBreathWeaponCone = ModManager.RegisterFeatName("Breath Weapon: Cone");

        private static FeatName scBeastEidolon = ModManager.RegisterFeatName("Beast Eidolon");
        private static FeatName scBeastEidolonBrutal = ModManager.RegisterFeatName("Brutal Beast");
        private static FeatName scBeastEidolonFleet = ModManager.RegisterFeatName("Fleet Beast");

        private static FeatName scDevoPhantomEidolon = ModManager.RegisterFeatName("Devotion Phantom");
        private static FeatName scDevoPhantomEidolonStalwart = ModManager.RegisterFeatName("Stalward Guardian");
        private static FeatName scDevoPhantomEidolonSwift = ModManager.RegisterFeatName("Swift Protector");

        private static FeatName scAzataEidolon = ModManager.RegisterFeatName("Azata Eidolon");
        private static FeatName scAzataEidolonCrusader = ModManager.RegisterFeatName("Crusader Azata");
        private static FeatName scAzataEidolonPoet = ModManager.RegisterFeatName("Poet Azata");

        private static FeatName scFeyEidolon = ModManager.RegisterFeatName("Fey Eidolon");
        private static FeatName scFeyEidolonSkirmisher = ModManager.RegisterFeatName("Skirmisher Fey");
        private static FeatName scFeyEidolonTrickster = ModManager.RegisterFeatName("Trickster Fey");

        private static FeatName scDevilEidolon = ModManager.RegisterFeatName("Devil Eidolon");
        private static FeatName scDevilEidolonLegionnaire = ModManager.RegisterFeatName("Inferal Legionnaire");
        private static FeatName scDevilEidolonBarrister = ModManager.RegisterFeatName("Infernal Barrister");

        private static FeatName scAngerPhantom = ModManager.RegisterFeatName("Anger Phantom Eidolon");
        private static FeatName scAngerPhantomBerserker = ModManager.RegisterFeatName("Wrathful Berserker");
        private static FeatName scAngerPhantomAssassin = ModManager.RegisterFeatName("Enraged Assassin");

        // Class Feat names
        private static FeatName ftAbundantSpellcasting1 = ModManager.RegisterFeatName("AbundantSpellCastingSummoner1", "Abundant Spellcasting");
        private static FeatName ftAbundantSpellcasting4 = ModManager.RegisterFeatName("AbundantSpellCastingSummoner4", "Abundant Spellcasting 2");
        public static FeatName ftBoostSummons = ModManager.RegisterFeatName("SummonerClassFeatBoostSummons", "Boost Summons");
        public static FeatName ftMagicalUnderstudy = ModManager.RegisterFeatName("SummonerMagicalUnderstudy", "Magical Understudy");
        public static FeatName ftMagicalAdept = ModManager.RegisterFeatName("SummonerMagicalAdept", "Magical Adept");

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

        // QEffectIDs
        internal static QEffectId qfSharedActions = ModManager.RegisterEnumMember<QEffectId>("Shared Actions");
        internal static QEffectId qfSummonerBond = ModManager.RegisterEnumMember<QEffectId>("Shared HP");
        internal static QEffectId qfActTogetherToggle = ModManager.RegisterEnumMember<QEffectId>("Act Together Toggle");
        internal static QEffectId qfActTogether = ModManager.RegisterEnumMember<QEffectId>("Act Together");
        internal static QEffectId qfExtendBoostExtender = ModManager.RegisterEnumMember<QEffectId>("Extend Boost Extended");
        internal static QEffectId qfReactiveStrikeCheck = ModManager.RegisterEnumMember<QEffectId>("Reactive Strike Check");
        internal static QEffectId qfParrying = ModManager.RegisterEnumMember<QEffectId>("Eidolon Parry");
        internal static QEffectId qfInvestedWeapon = ModManager.RegisterEnumMember<QEffectId>("Invested Weapon");
        internal static QEffectId qfDrainedMirror = ModManager.RegisterEnumMember<QEffectId>("Drained (Mirror)");
        internal static QEffectId qfEidolonsWrath = ModManager.RegisterEnumMember<QEffectId>("Eidolon's Wrath QF");
        internal static QEffectId qfOstentatiousArrival = ModManager.RegisterEnumMember<QEffectId>("Ostentatious Arrival Toggled");
        internal static QEffectId qfWhimsicalAura = ModManager.RegisterEnumMember<QEffectId>("Whimsical Aura");
        internal static QEffectId qfSeethingFrenzy = ModManager.RegisterEnumMember<QEffectId>("Seething Frenzy");

        // Actions
        internal static ActionId acCelestialPassion = ModManager.RegisterEnumMember<ActionId>("CelestialPassion");

        // Menus
        internal static PossibilitySectionId psTandemActions = ModManager.RegisterEnumMember<PossibilitySectionId>("TandemActionsSubMenu");
        internal static PossibilitySectionId psTandemActionsMain = ModManager.RegisterEnumMember<PossibilitySectionId>("TandemActionsMainBar");

        // Illustrations
        internal static ModdedIllustration illActTogether = new ModdedIllustration("SummonerAssets/ActTogether.png");
        internal static ModdedIllustration illActTogetherStatus = new ModdedIllustration("SummonerAssets/ActTogetherStatus.png");
        internal static ModdedIllustration illDismiss = new ModdedIllustration("SummonerAssets/Dismiss.png");
        internal static ModdedIllustration illEidolonBoost = new ModdedIllustration("SummonerAssets/EidolonBoost.png");
        internal static ModdedIllustration illReinforceEidolon = new ModdedIllustration("SummonerAssets/ReinforceEidolon.png");
        internal static ModdedIllustration illEvolutionSurge = new ModdedIllustration("SummonerAssets/EvolutionSurge.png");
        internal static ModdedIllustration illLifeLink = new ModdedIllustration("SummonerAssets/LifeLink.png");
        internal static ModdedIllustration illExtendBoost = new ModdedIllustration("SummonerAssets/ExtendBoost.png");
        internal static ModdedIllustration illTandemMovement = new ModdedIllustration("SummonerAssets/TandemMovement.png");
        internal static ModdedIllustration illTandemStrike = new ModdedIllustration("SummonerAssets/TandemStrike.png");
        internal static ModdedIllustration illBeastsCharge = new ModdedIllustration("SummonerAssets/BeastsCharge.png");
        internal static ModdedIllustration illInvest = new ModdedIllustration("SummonerAssets/Parry.png");
        internal static ModdedIllustration illAngelicAegis = new ModdedIllustration("SummonerAssets/AngelicAegis.png");
        internal static ModdedIllustration illDevoStance = new ModdedIllustration("SummonerAssets/DevotionStance.png");
        internal static ModdedIllustration illPrimalRoar = new ModdedIllustration("SummonerAssets/PrimalRoar.png");
        internal static ModdedIllustration illDraconicFrenzy = new ModdedIllustration("SummonerAssets/DraconicFrenzy.png");
        internal static ModdedIllustration illOstentatiousArrival = new ModdedIllustration("SummonerAssets/OstentatiousArrival.png");
        internal static ModdedIllustration illFrenziedAssault = new ModdedIllustration("SummonerAssets/FrenziedAssault.png");
        internal static ModdedIllustration illSeethingFrenzy = new ModdedIllustration("SummonerAssets/SeethingFrenzy.png");
        internal static ModdedIllustration illDisciplineTheLegion = new ModdedIllustration("SummonerAssets/DisciplineTheLegion.png");
        internal static ModdedIllustration illConstrictingHold = new ModdedIllustration("SummonerAssets/ConstrictingHold.png");

        // Portraits
        internal static List<ModdedIllustration> portraits = new List<ModdedIllustration>();

        // SpellIDs
        private static Dictionary<SummonerSpellId, SpellId> spells = LoadSpells();

        // Class and subclass text
        private static readonly string SummonerFlavour = "You can magically beckon a powerful being called an eidolon to your side, serving as the mortal conduit that anchors it to the world. " +
            "Whether your eidolon is a friend, a servant, or even a personal god, your connection to it marks you as extraordinary, shaping the course of your life dramatically.";
        private static readonly string SummonerCrunch =
            "{b}1. Eidolon.{/b} You have a connection with a powerful and usually otherworldly entity called an eidolon, and you can use your life force as a conduit to manifest this ephemeral entity into the mortal world. " +
            "Your bonded eidolon's nature determine your spell casting tradition, in addition to its statistics. In addition, its appearence and attacks are fully customisable.\n\n" +
            "Your eidolon begins combat already manifested, and shares your hit point pool, actions and multiple attack penalty. You can swap between controlling you or your eidolon at any time, without ending your turn.\n\n" +
            "Your eidolon benefits from the skill bonuses on any invested magical items you're wearing, and all of your fundermental armour runes.\n\n" +
            "{b}2. Invest Weapon {icon:Action}.{/b} Your eidolon's unarmed strikes also benefit from the fundermental and property runes of a single weapon you're wielding, or your handwraps of mighty blows. " +
            "At the start of combat, you automatically elect a weapon for your eidolon to use, in the following priority order: Your handwraps of mighty blows, your main-hand weapon, your off-hand weapon. If you drop an invested weapon, " +
            "your eidolon can also no longer benefit from it. You can select a new magic weapon from the items you're holding, or swap back to your handwraps, by using the Invest Weapon {icon:Action} action, under 'Other Maneuvers'." +
            "\n\n{b}3. Evolution Feat.{/b} Gain a single 1st level evolution feat. Evolution feats affect your eidolon instead of you.\n\n" +
            "{b}4. Link Spells.{/b} Your connection to your eidolon allows you to cast link spells, special spells that have been forged through your shared connection with your eidolon." +
            "You start with two such spells. The focus spell " + AllSpells.CreateModernSpellTemplate(spells[SummonerSpellId.EvolutionSurge], tSummoner).ToSpellLink() + " and the link cantrip " +
            AllSpells.CreateModernSpellTemplate(spells[SummonerSpellId.EidolonBoost], tSummoner).ToSpellLink() + "\n\n" +
            "{b}4. Spontaneous Spellcasting:{/b} You can cast spells. You can cast 1 spell per day and you can choose the spells from among the spells you know. You learn 2 spells of your choice, " +
            "but they must come from the spellcasting tradition of your eidolon. You also learn 5 cantrips — weak spells — that automatically heighten as you level up. You can cast any number of cantrips per day. " +
            "You can gain additional spell slots and spells known from leveling up and from feats. Your spellcasting ability is Charisma" +
            "\n\n{b}At higher levels:{/b}" +
            "\n{b}Level 2:{/b} Summoner feat" +
            "\n{b}Level 3:{/b} General feat, skill increase, expert perception, level 2 spells {i}(one spell slot){/i}" +
            "\n{b}Level 4:{/b} Summoner feat, level 2 spells (1 slot, one spell konwn)" +
            "\n{b}Level 5:{/b} Ability boosts {i}(for you and your eidolon){/i}, ancestry feat, skill increase, level 3 spells (Two spell slots, but you lose all level 1 spell slots), repick all spells known" +
            "\n{b}Level 6:{/b} Summoner Feat" +
            "\n{b}Level 7:{/b} General feat, skill increase, eidolon weapon specialization {i}(your eidolon deals 2 extra additional damage with unarmed attacks){/i}, level 4 spells (Two spell slots, but you lose all level 2 spell slots), repick all spells known" +
            "\n{b}Level 8:{/b} Summoner Feat";
        private static readonly string AngelicEidolonFlavour = "Your eidolon is a celestial messenger, a member of the angelic host with a unique link to you, allowing them to carry a special message to the mortal world at your side. " +
            "Most angel eidolons are roughly humanoid in form, with feathered wings, glowing eyes, halos, or similar angelic features. However, some take the form of smaller angelic servitors like the winged helmet" +
            "cassisian angel instead. The two of you are destined for an important role in the plans of the celestial realms. Though a true angel, your angel eidolon's link to you as a mortal prevents them " +
            "from casting the angelic messenger ritual, even if they somehow learn it.";

        private static readonly string AngelicEidolonCrunch = "\n\n• {b}Tradition{/b} Divine\n• {b}Skills{/b} Diplomacy, Religion\n\n{b}Initial Eidolon Ability (Hallowed Strikes).{/b} Your Eidolon's strikes deal +1 good damage." +
            "\n\n{i}At level 7{/i}\n{b}Symbiosis Eidolon Ability (Angelic Aegis).{/b} Your eidolon's primary natural weapon attack gains the parry trait. While parrying they can use the Angelic Aegis {icon:FreeAction} action, " +
            "which grants an adjacent ally a +2 circumstance bonus to AC until the start of their next turn. In addition, your eidolon can intercept {icon:Reaction} attacks that deal physical damage against the subject of their aegis, reducing the damage by an amount equal to their level.\n\n" +
            "These benefits extend only while the ally is currently adjacent to your eidolon.";

        private static readonly string DraconicEidolonFlavour = "Because dragons have a strong connection to magic, their minds can often leave an echo floating in the Astral Plane. Such an entity is extremely powerful " +
            "but unable to interact with the outside world on its own. Dragon eidolons manifest in the powerful, scaled forms they had in life; most take the form of true dragons (albeit smaller), but some manifest as " +
            "drakes or other draconic beings. You have forged a connection with such a dragon eidolon and together, you seek to grow as powerful as an ancient wyrm.";

        private static readonly string DraconicEidolonCrunch = "\n\n• {b}Tradition{/b} Varies\n• {b}Skills{/b} You gain Intimidation and the knowledge skill associated with your dragon eidolon's magical tradition." +
            "\n\n{b}Initial Eidolon Ability (Breath Weapon) {icon:TwoActions}.{/b} Your eidolon exhales a 60-foot line or 30-foot cone of energy and deal 2d6 of the damage associated with your eidolon's dragon type to each target. " +
            "You can't use breath weapon again for 1d4 rounds. This damage increases by 1d6 at 3rd level and every two levels thereafter.\n\n{b}Special.{/b} " +
            "You must select a specific breed for your dragon. This will determine your spell tradition, one of your bonus skills and the damage type of your eidolon's breath weapon. Your dragon's type also determines the save targeted by its breath weapon." +
            "\n\n{i}At level 7{/i}\n{b}Symbiosis Eidolon Ability (Draconic Frenzy) {icon:TwoActions}.{/b} Your eidolon makes 3 consecutive attacks, one with its primary natural weapon attack and two with its secondary natural weapon attack. If any of these attacks result in a " +
            "critical hit, your eidolon's Breath Weapon is immediately recharged.";

        //private static readonly string DraconicEidolonCrunch = "\n\n• {b}Tradition{/b} Arcane\n• {b}Skills{/b} Arcana, Intimidation" +
        //    "\n\n{b}Eidolon Ability (Breath Weapon).{/b} {icon:TwoActions} Your eidolon exhales a 60-foot line or 30-foot cone of energy and deal 2d6 of the damage associated with your eidolon's dragon type to each target. " +
        //    "You can't use breath weapon again for 1d4 rounds. This damage increases by 1d6 at 3rd level and every two levels thereafter.\n\n{b}Special.{/b} " +
        //    "You must select a specific breed for your dragon. This will determine the damage type of your eidolon's breath weapon and the save it targets.";

        private static readonly string BeastEidolonFlavour = "Your eidolon is a manifestation of the life force of nature in the form of a powerful magical beast that often has animal features, possibly even several from different species. " +
            "You might have learned the way to connect with the world's life force via a specific philosophy or practice, such as the beliefs of the god callers of Sarkoris, or formed a bond on your own. Regardless, your link to your eidolon " +
            "allows you both to grow in power and influence to keep your home safe from those who would despoil it.";

        private static readonly string BeastEidolonCrunch = "\n\n• {b}Tradition{/b} Primal\n• {b}Skills{/b} Intimidation, Nature\n\n{b}Initial Eidolon Ability (Beast's Charge) {icon:TwoActions}.{/b} Stride twice. " +
            "If you end your movement within melee reach of at least one enemy, you can make a melee Strike against that enemy. If your eidolon moved at least 20ft and ends it's movement in a cardinal diraction, " +
            "it gains a +1 circumstance bonus to this attack roll.\n\n{i}At level 7{/i}\n{b}Symbiosis Eidolon Ability (Primal Roar) {icon:TwoActions}.{/b} Your eidolon unleashes a primal roar or other such terrifying noise that fits your eidolon's form. " +
            "Your eidolon attempts Intimidation checks to Demoralize each enemy that can hear the roar; these Demoralize attempts don't take any penalty for not sharing a language, and gain a +2 bonus.";

        private static readonly string DevoPhantomEidolonFlavour = "Your eidolon is a lost soul, unable to escape the mortal world due to a strong sense of duty, an undying devotion, or a need to complete an important task. " +
            "Most phantom eidolons are humanoid with a spectral or ectoplasmic appearance, though some take far stranger forms. Your link with your eidolon prevents them from succumbing to corruption and undeath, and together, " +
            "you will grow in strength and fulfill your phantom's devotion.";

        private static readonly string DevoPhantomEidolonCrunch = "\n\n• {b}Tradition{/b} Occult\n• {b}Skills{/b} Medicine, Occultism\n\n" +
            "{b}Initial Eidolon Ability (Dutiful Retaliation) {icon:Reaction}.{/b} Your eidolon makes a strike again an enemy that damaged you. Both your eidolon and your attacker must be within 15ft of you." +
            "\n\n{i}At level 7{/i}\n{b}Symbiosis Eidolon Ability (Devotion Stance) {icon:Action}.{/b} Your eidolon takes on a patient defensive stance, steeling their focus with thoughts of their devotion." +
            "\n\nUntil the start of their next turn, they gain a +2 circumstance bonus to AC, and a +4 bonus to damage to attacks made outside their turn.";

        private static readonly string AzataEidolonFlavour = "Your eidolon is an azata, a celestial embodiment of freedom, creativity, whimsy and revelry. They usually take humanoid forms, sometimes incorporating nature motifs. " +
            "Your eidolon is happy to serve as your protector and muse as long as you show the same kindness and respect for freedom and autonomy to others as it does.";

        private static readonly string AzataEidolonCrunch = "\n\n• {b}Tradition{/b} Divine\n• {b}Skills{/b} Divine, Persuasion\n\n" +
            "{b}Initial Eidolon Ability (Celestial Passion) {icon:Action}.{/b} One ally within 15-feet of your eidolon gains temporary HP equal to its level, and a +1 bonus to attack and skill checks for 1 round. Cannot be used on the same ally more than once per encounter." +
            "\n\n{i}At level 7{/i}\n{b}Symbiosis Eidolon Ability (Whimsical Aura).{/b} The wonder and whimsy of Elysium manifests around your eidolon in an aura. Your eidolon and all allies within a 15-foot aura gain a +5-foot status bonus to their speed at the " +
            "start of their turn. In addition, each ally within this aura at the end of your turn, reduces their frightened value by 1." +
            "\n\n{b}Credits{/b} {i}Designed by LeoRandger{/i}";

        private static readonly string DevilEidolonFlavour = "Your eidolon is a devil - a creature born in the depths of Nine Hells,the embodiment of order and tyranny. Whether tricked into being linked to each other through an infernal " +
            "contract or connected through means, your companion represents the interest of his infernal patrons in mortal affairs. He might act authoritative or submit to you, but while your goals align, it shall follow you on your adventures. " +
            "You only have to worry whether your soul is destined for its home...";

        private static readonly string DevilEidolonCrunch = "\n\n• {b}Tradition{/b} Divine\n• {b}Skills{/b} Religion, Intimidation\n\n" +
            "{b}Initial Eidolon Ability (Hellfire Scourage).{/b} Your eidolon gains resistance to fire equal to their level (minimum 1), and an equivalent weakness to good (minimum 1). In addition, the first attack they make against a Frightened creature each turn, deals an additional 1d4 fire damage." +
            "\n\n{i}At level 7{/i}\n{b}Symbiosis Eidolon Ability (Legion Commander) {icon:Action}.{/b} Your eidolon shouts a commands at one ally within 30-feet. The next time that ally attacks or makes a skill check before the start of your next turn, your eidolon can use their reaction " +
            "to make an Intimidation check against an easy DC for their level.\n{b}Critical Success{/b} You grant your ally a +2 circumstance bonus to the triggering check. If you’re a master with the check you attempted, the bonus is +3, and if you’re legendary, it’s +4." +
            "\n{b}Success{/b} You grant your ally a +1 circumstance bonus to the triggering check.\n{b}Critical Failure{/b} Your ally takes a –1 circumstance penalty to the triggering check.\n\nYour ally also deals extra fire damage equal to half your level, " +
            "if the action your eidolon was assisting them with was an attack, and double if the attack was a critial success." +
            "\n\n{b}Credits{/b} {i}Designed by LeoRandger{/i}";

        private static readonly string FeyEidolonFlavour = "Your eidolon is a fey, a capricious being of the mysterious First World. Many fey appear similar to mortal humanoids with unusual features such as pointed ears, " +
            "wings, or bodies composed of natural elements, but the full variety of fey is endless, and many others appear completely inhuman. Fey from the First World never truly die, instead forming a new creature. " +
            "Fey eidolons usually come about when a summoner helps stabilize a difficult reformation. This means your fey eidolon likely lived a different life just before meeting you and might remember fragments of its old memories. " +
            "Together, you might have to unravel a memory from your eidolon's past life among the fey.";

        private static readonly string FeyEidolonCrunch = "\n\n• {b}Tradition{/b} Primal\n• {b}Skills{/b} Nature, Deception\n\n" +
            "{b}Initial Eidolon Ability (Fey Gifts).{/b} Your eidolon expands your primal magic with enchantment and illusion magic, allowing both of you to wield the power of fey charm and glamour. When you add spells to your repertoire, " +
            "you can choose from the primal list as well as from enchantment and illusion spells that appear on the arcane spell list. As usual for when you add spells of a different tradition to your spell list, you're still a primal spellcaster, " +
            "so all of your spells are primal spells.\n\nYour eidolon gains the Magical Understudy summoner feat, despite not meeting the prerequisite level, and it can choose fey gift cantrips in addition to primal cantrips." +
            "\n\n{i}At level 7{/i}\n{b}Symbiosis Eidolon Ability (Fey Mischief).{/b} Your eidolon's fey magic becomes more powerful and mischievous. Your eidolon gains the Magical Adept feat, despite not meeting the prerequisite level, " +
            "and can choose from fey gift spells in addition to primal spells.";

        private static readonly string AngerPhantomFlavour = "Your eidolon is a lost soul, bound to the mortal world by undying anger or a bitter grudge. Most phantom eidolons are humanoids with a spectral or ectoplasmic appearance, " +
            "though some take far stranger forms. Your link with your eidolon prevents it from succumbing to corruption and undeath. Together, you will need to decide whether to work with your eidolon to control its anger, or channel its wrath into power.";

        private static readonly string AngerPhantomCrunch = "\n\n• {b}Tradition{/b} Occult\n• {b}Skills{/b} Occultism, Intimidation\n\n" +
            "{b}Initial Eidolon Ability (Fenzied Assault) {icon:TwoActions}.{/b} Your eidolon makes two strikes against a single target, one with each of its unarmed attacks, at its current MAP penalty. " +
            "The damage from both attacks are combined for the purposes of damage resistance." +
            "\n\n{i}At level 7{/i}\n{b}Symbiosis Eidolon Ability (Seething Frenzy) {icon:Action}.{/b} Your eidolon enters a seething frenzy, disregarding its own safety to tear your foes apart. " +
            "It gains temporary HP equal to its level, and a +4 damage bonus to its unarmed strike attacks, but it takes a -2 penalty to AC. The rage lasts until the end of the encounter, and leaves your eidolon fatigued if they leave early.";

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
            string[] divineTypes = new string[] { "Angel Eidolon", "Empyreal Dragon", "Diabolic Dragon", "Azata Eidolon", "Psychopmp Eidolon", "Demon Eidolon", "Devil Eidolon" };

            //string rootLocation = new FileInfo("../").Directory.Parent.Parent.FullName;
            //Directory.GetFiles(typeof(SomeTypeInsideYourDll).Location.Fullname.Directory
            //rootLocation += "/workshop/content/2693730/3315725529/CustomMods/SummonerAssets/EidolonPortraits/";
            string rootLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            rootLocation.Substring(0, rootLocation.Length - "/DawnsburyDaysSummonerClass.dll".Length);
            string extraPath = "/SummonerAssets/EidolonPortraits/";

            // Create portrait feats
            List<string> portraitDir = Directory.GetFiles(rootLocation + extraPath + "Beast")
                .Concat(Directory.GetFiles(rootLocation + extraPath + "Construct"))
                .Concat(Directory.GetFiles(rootLocation + extraPath + "Dragon"))
                .Concat(Directory.GetFiles(rootLocation + extraPath + "Elemental"))
                .Concat(Directory.GetFiles(rootLocation + extraPath + "Humanoid"))
                .Concat(Directory.GetFiles(rootLocation + extraPath + "Outsider"))
                .Concat(Directory.GetFiles(rootLocation + extraPath + "Undead"))
                .Concat(Directory.GetFiles(rootLocation + extraPath + "ConvertedBaseGameAssets/Beast"))
                .Concat(Directory.GetFiles(rootLocation + extraPath + "ConvertedBaseGameAssets/Construct"))
                .Concat(Directory.GetFiles(rootLocation + extraPath + "ConvertedBaseGameAssets/Dragon"))
                .Concat(Directory.GetFiles(rootLocation + extraPath + "ConvertedBaseGameAssets/Elemental"))
                .Concat(Directory.GetFiles(rootLocation + extraPath + "ConvertedBaseGameAssets/Humanoid"))
                .Concat(Directory.GetFiles(rootLocation + extraPath + "ConvertedBaseGameAssets/Outsider"))
                .Concat(Directory.GetFiles(rootLocation + extraPath + "ConvertedBaseGameAssets/Undead"))
                .ToList();

            List<string> nonImages = new List<string>();
            foreach (string file in portraitDir) {
                if (!file.EndsWith(".png")) {
                    nonImages.Add(file);
                }
            }
            while (nonImages.Count > 0) {
                portraitDir.Remove(nonImages.Last());
                nonImages.Remove(nonImages.Last());
            }

            foreach (string file in portraitDir) {
                string clippedDir = file.Substring(rootLocation.Length + 1);
                //throw new Exception(clippedDir);

                portraits.Add(new ModdedIllustration(clippedDir));
            }

            List<Feat> portraitFeatList = new List<Feat>();

            foreach (ModdedIllustration portrait in portraits) {
                Trait category = Trait.None;
                string featName = DirToFeatName(portrait.Filename, out category);
                portraitFeatList.Add(new Feat(ModManager.RegisterFeatName(featName), "", "", new List<Trait>() { tPortrait, category }, null).WithIllustration(portrait));
                yield return portraitFeatList.Last();
            }

            // Create portrait category feats
            yield return new Feat(ModManager.RegisterFeatName("BeastPortraits", "Category: Beast"), "", "", new List<Trait>() { tPortraitCategory }, portraitFeatList.Where(ft => ft.HasTrait(Trait.Beast)).ToList());
            yield return new Feat(ModManager.RegisterFeatName("ConstructPortraits", "Category: Construct"), "", "", new List<Trait>() { tPortraitCategory }, portraitFeatList.Where(ft => ft.HasTrait(Trait.Construct)).ToList());
            yield return new Feat(ModManager.RegisterFeatName("DragonPortraits", "Category: Dragon"), "", "", new List<Trait>() { tPortraitCategory }, portraitFeatList.Where(ft => ft.HasTrait(Trait.Dragon)).ToList());
            yield return new Feat(ModManager.RegisterFeatName("ElementalPortraits", "Category: Elemental"), "", "", new List<Trait>() { tPortraitCategory }, portraitFeatList.Where(ft => ft.HasTrait(Trait.Elemental)).ToList());
            yield return new Feat(ModManager.RegisterFeatName("HumanoidPortraits", "Category: Humanoid"), "", "", new List<Trait>() { tPortraitCategory }, portraitFeatList.Where(ft => ft.HasTrait(Trait.Humanoid)).ToList());
            yield return new Feat(ModManager.RegisterFeatName("OutsiderPortraits", "Category: Outsider"), "", "", new List<Trait>() { tPortraitCategory }, portraitFeatList.Where(ft => ft.HasTrait(tOutsider)).ToList());
            yield return new Feat(ModManager.RegisterFeatName("UndeadPortraits", "Category: Undead"), "", "", new List<Trait>() { tPortraitCategory }, portraitFeatList.Where(ft => ft.HasTrait(Trait.Undead)).ToList());

            // Init subclasses
            Feat angelEidolon = new EidolonBond(scAngelicEidolon, AngelicEidolonFlavour, AngelicEidolonCrunch, Trait.Divine, new List<FeatName>() { FeatName.Religion, FeatName.Diplomacy }, new Func<Feat, bool>(ft => new FeatName[] { ftALawfulGood, ftAGood, ftAChaoticGood }.Contains(ft.FeatName)))
            .WithOnSheet((Action<CalculatedCharacterSheetValues>)(values => {
                values.AddSelectionOption((SelectionOption)new SingleFeatSelectionOption("AngelicEidolonArray", "Eidolon Ability Scores", 1, (Func<Feat, bool>)(ft => new FeatName[] { scAngelicEidolonAvenger, scAngelicEidolonEmmissary }.Contains(ft.FeatName))));
            }));

            yield return CreateEidolonFeat(scAngelicEidolonAvenger, "Your eidolon is a fierce warrior of the heavens.", new int[6] { 4, 2, 3, -1, 1, 0 }, 2, 3);
            yield return CreateEidolonFeat(scAngelicEidolonEmmissary, "Your eidolon is a regal emmisary of the heavens.", new int[6] { 1, 4, 1, 0, 1, 2 }, 1, 4);

            Feat azataEidolon = new EidolonBond(scAzataEidolon, AzataEidolonFlavour, AzataEidolonCrunch, Trait.Divine, new List<FeatName>() { FeatName.Religion, FeatName.Diplomacy },
                new Func<Feat, bool>(ft => new FeatName[] { ftAChaoticGood }.Contains(ft.FeatName)), new List<Trait>() { Trait.Homebrew }, null)
            .WithOnSheet((Action<CalculatedCharacterSheetValues>)(values => {
                values.AddSelectionOption((SelectionOption)new SingleFeatSelectionOption("AzataEidolonArray", "Eidolon Ability Scores", 1, (Func<Feat, bool>)(ft => new FeatName[] { scAzataEidolonCrusader, scAzataEidolonPoet }.Contains(ft.FeatName))));
            }));

            yield return CreateEidolonFeat(scAzataEidolonCrusader, "Your eidolon is a benevolant crusader of Elysium.", new int[6] { 4, 2, 3, -1, 0, 1 }, 2, 3);
            yield return CreateEidolonFeat(scAzataEidolonPoet, "Your eidolon is an inspiring muse of Elysium.", new int[6] { 1, 4, 1, -1, 1, 3 }, 1, 4);

            Feat beastEidolon = new EidolonBond(scBeastEidolon, BeastEidolonFlavour, BeastEidolonCrunch, Trait.Primal, new List<FeatName>() { FeatName.Nature, FeatName.Intimidation }, new Func<Feat, bool>(ft => ft.HasTrait(tAlignment)))
                .WithOnSheet((Action<CalculatedCharacterSheetValues>)(values => {
                    values.AddSelectionOption((SelectionOption)new SingleFeatSelectionOption("AngelicEidolonArray", "Eidolon Ability Scores", 1, (Func<Feat, bool>)(ft => new FeatName[] { scBeastEidolonBrutal, scBeastEidolonFleet }.Contains(ft.FeatName))));
                }));

            yield return CreateEidolonFeat(scBeastEidolonBrutal, "Your eidolon is a powerful and brutally strong beast.", new int[6] { 4, 2, 3, -1, 1, 0 }, 2, 3);
            yield return CreateEidolonFeat(scBeastEidolonFleet, "Your eidolon is a fleet and agile beast.", new int[6] { 2, 4, 3, -1, 1, 0 }, 1, 4);

            Feat devoPhantomEidolon = new EidolonBond(scDevoPhantomEidolon, DevoPhantomEidolonFlavour, DevoPhantomEidolonCrunch, Trait.Occult, new List<FeatName>() { FeatName.Occultism, FeatName.Medicine }, new Func<Feat, bool>(ft => ft.HasTrait(tAlignment)))
                .WithOnSheet((Action<CalculatedCharacterSheetValues>)(values => {
                    values.AddSelectionOption((SelectionOption)new SingleFeatSelectionOption("DevoPhantomEidolonArray", "Eidolon Ability Scores", 1, (Func<Feat, bool>)(ft => new FeatName[] { scDevoPhantomEidolonStalwart, scDevoPhantomEidolonSwift }.Contains(ft.FeatName))));
                }));

            yield return CreateEidolonFeat(scDevoPhantomEidolonStalwart, "Your eidolon is an unyielding guardian.", new int[6] { 4, 2, 3, 0, 0, 0 }, 2, 3);
            yield return CreateEidolonFeat(scDevoPhantomEidolonSwift, "Your eidolon is a vigilant protector.", new int[6] { 2, 4, 3, 0, 0, 0 }, 1, 4);

            Feat devilEidolon = new EidolonBond(scDevilEidolon, DevilEidolonFlavour, DevilEidolonCrunch, Trait.Divine, new List<FeatName>() { FeatName.Religion, FeatName.Intimidation },
                new Func<Feat, bool>(ft => new FeatName[] { ftALawfulEvil }.Contains(ft.FeatName)), new List<Trait>() { Trait.Homebrew }, null)
            .WithOnSheet((Action<CalculatedCharacterSheetValues>)(values => {
                values.AddSelectionOptionRightNow((SelectionOption)new SingleFeatSelectionOption("DevilEidolonArray", "Eidolon Ability Scores", 1, (Func<Feat, bool>)(ft => new FeatName[] { scDevilEidolonBarrister, scDevilEidolonLegionnaire }.Contains(ft.FeatName))));
            }));

            yield return CreateEidolonFeat(scDevilEidolonLegionnaire, "Your eidolon is a ruthlessly professional legionnaire of hell.", new int[6] { 4, 2, 2, 0, -1, 2 }, 2, 3);
            yield return CreateEidolonFeat(scDevilEidolonBarrister, "Your eidolon is a cunning and corruptive barrister of hell.", new int[6] { 1, 4, 1, 0, 1, 2 }, 1, 4);

            Feat angerPhantom = new EidolonBond(scAngerPhantom, AngerPhantomFlavour, AngerPhantomCrunch, Trait.Occult, new List<FeatName>() { FeatName.Occultism, FeatName.Intimidation }, new Func<Feat, bool>(ft => ft.HasTrait(tAlignment)))
            .WithOnSheet((Action<CalculatedCharacterSheetValues>)(values => {
                values.AddSelectionOptionRightNow((SelectionOption)new SingleFeatSelectionOption("AngerPhantomEidolonArray", "Eidolon Ability Scores", 1, (Func<Feat, bool>)(ft => new FeatName[] { scAngerPhantomBerserker, scAngerPhantomAssassin }.Contains(ft.FeatName))));
            }));

            yield return CreateEidolonFeat(scAngerPhantomBerserker, "Your eidolon is an unyielding guardian.", new int[6] { 4, 2, 3, -1, 0, 1 }, 2, 3);
            yield return CreateEidolonFeat(scAngerPhantomAssassin, "Your eidolon is a vigilant protector.", new int[6] { 2, 4, 3, 0, -1, 1 }, 1, 4);

            Feat feyEidolon = new EidolonBond(scFeyEidolon, FeyEidolonFlavour, FeyEidolonCrunch, Trait.Primal, new List<FeatName>() { FeatName.Nature, FeatName.Deception },
                new Func<Feat, bool>(ft => ft.HasTrait(tAlignment)))
            .WithOnSheet((Action<CalculatedCharacterSheetValues>)(values => {
                values.AddSelectionOptionRightNow((SelectionOption)new SingleFeatSelectionOption("FeyEidolonArray", "Eidolon Ability Scores", 1, (Func<Feat, bool>)(ft => new FeatName[] { scFeyEidolonSkirmisher, scFeyEidolonTrickster }.Contains(ft.FeatName))));
            }));

            yield return CreateEidolonFeat(scFeyEidolonSkirmisher, "Your eidolon is an illusive predator of the first world.", new int[6] { 2, 4, 2, 0, 0, 1 }, 1, 4);
            yield return CreateEidolonFeat(scFeyEidolonTrickster, "Your eidolon is a mercurial trickster of the first world.", new int[6] { 1, 4, 1, 1, -1, 3 }, 1, 4);

            Feat dragonEidolon = new Feat(scDraconicEidolon, DraconicEidolonFlavour, DraconicEidolonCrunch, new List<Trait>() { }, null)
                .WithOnSheet((Action<CalculatedCharacterSheetValues>)(values => {
                    values.AddSelectionOption((SelectionOption)new SingleFeatSelectionOption("DraconicEidolonArray", "Eidolon Ability Scores", 1, (Func<Feat, bool>)(ft => new FeatName[] { scDraconicEidolonCunning, scDraconicEidolonMarauding }.Contains(ft.FeatName))));
                    values.AddSelectionOption((SelectionOption)new SingleFeatSelectionOption("DragonType", "Dragon Type", 1, (Func<Feat, bool>)(ft => ft.HasTrait(tDragonType))));
                }));

            yield return CreateEidolonFeat(scDraconicEidolonCunning, "Your eidolon is a cunning wyrm.", new int[6] { 1, 4, 1, 2, 1, 1 }, 1, 4);
            yield return CreateEidolonFeat(scDraconicEidolonMarauding, "Your eidolon is a fierce marauding drake.", new int[6] { 4, 2, 3, 0, 0, 0 }, 2, 3);

            // Dragon breath feats
            Feat dragonLineBreath = new Feat(ftBreathWeaponLine, "Your dragon eidolon emits a sharp, destructive line of energy.", "Your eidolon's breath weapon hits each creature in a 60-foot line.", new List<Trait>() { tBreathWeaponArea }, null);
            Feat dragonConeBreath = new Feat(ftBreathWeaponCone, "Your dragon eidolon spaws worth a torrent of destructive energy.", "Your eidolon's breath weapon hits each creature in a 30-foot cone.", new List<Trait>() { tBreathWeaponArea }, null);

            // Primal
            yield return new EidolonBond(ModManager.RegisterFeatName("BrineDragon", "Brine Dragon"), "Your eidolon is an orderly brine dragon from the elemental plane of water.", "Your eidolon's breath weapon deals acid damage vs. Reflex.\n\nBrine dragons are associated with the {b}Primal{/b} spellcasting tradition.",
                Trait.Primal, new List<FeatName>() { FeatName.Nature, FeatName.Intimidation }, new Func<Feat, bool>(ft => ft.HasTrait(tAlignment) && ft.HasTrait(Trait.Lawful)),
                new List<Trait>() { Trait.Acid, tDragonType }, new List<Feat>() { dragonConeBreath, dragonLineBreath });
            yield return new EidolonBond(ModManager.RegisterFeatName("Cloud Dragon", "Cloud Dragon"), "Your eidolon is an adventurous cloud dragon from the elemental plane of air.", "Your eidolon's breath weapon deals electricity damage vs. Reflex.\n\nCloud dragons are associated with the {b}Primal{/b} spellcasting tradition.",
                Trait.Primal, new List<FeatName>() { FeatName.Nature, FeatName.Intimidation }, new Func<Feat, bool>(ft => ft.HasTrait(tAlignment)),
                new List<Trait>() { Trait.Electricity, tDragonType }, new List<Feat>() { dragonConeBreath, dragonLineBreath });
            yield return new EidolonBond(ModManager.RegisterFeatName("CrystalDragon", "Crystal Dragon"), "Your eidolon is a beautiful crystal dragon from the elemental plane of earth.", "Your eidolon's breath weapon deals piercing damage vs. Reflex.\n\nCrystal dragons are associated with the {b}Primal{/b} spellcasting tradition.",
                Trait.Primal, new List<FeatName>() { FeatName.Nature, FeatName.Intimidation }, new Func<Feat, bool>(ft => ft.HasTrait(tAlignment)),
                new List<Trait>() { Trait.VersatileP, tDragonType }, new List<Feat>() { dragonConeBreath, dragonLineBreath });
            yield return new EidolonBond(ModManager.RegisterFeatName("MagmaDragon", "Magma Dragon"), "Your eidolon is a volatile magma dragon from the elemental plane of fire.", "Your eidolon's breath weapon deals fire damage vs. Reflex.\n\nMagma dragons are associated with the {b}Primal{/b} spellcasting tradition.",
                Trait.Primal, new List<FeatName>() { FeatName.Arcana, FeatName.Intimidation }, new Func<Feat, bool>(ft => ft.HasTrait(tAlignment)),
                new List<Trait>() { Trait.Fire, tDragonType }, new List<Feat>() { dragonConeBreath, dragonLineBreath });
            yield return new EidolonBond(ModManager.RegisterFeatName("UmbralDragon", "Umbral Dragon"), "Your eidolon is a shadowy umbral dragon from the Shadowfell.",
                "Your eidolon's breath weapon deals negative damage vs. Reflex.\n\nUmbral dragons are associated with the {b}Occult{/b} spellcasting tradition.\n\n{b}Special{/b} Your dragon's ghostkilling breath deals force damage to undead.",
                Trait.Occult, new List<FeatName>() { FeatName.Occultism, FeatName.Intimidation }, new Func<Feat, bool>(ft => ft.HasTrait(tAlignment) && ft.HasTrait(Trait.Evil)),
                new List<Trait>() { Trait.Negative, tDragonType }, new List<Feat>() { dragonConeBreath, dragonLineBreath });

            // Chromatic
            yield return new EidolonBond(ModManager.RegisterFeatName("BlackDragon", "Black Dragon"), "Your eidolon is a vile black dragon.", "Your eidolon's breath weapon deals acid damage vs. Reflex.\n\nBlack dragons are associated with the {b}Arcane{/b} spellcasting tradition.",
                Trait.Arcane, new List<FeatName>() { FeatName.Arcana, FeatName.Intimidation }, new Func<Feat, bool>(ft => ft.HasTrait(tAlignment) && ft.HasTrait(Trait.Evil)),
                new List<Trait>() { Trait.Acid, tDragonType }, new List<Feat>() { dragonConeBreath, dragonLineBreath });
            yield return new EidolonBond(ModManager.RegisterFeatName("BlueDragon", "Blue Dragon"), "Your eidolon is a sophisticated blue dragon.", "Your eidolon's breath weapon deals electricity damage vs. Reflex.\n\nBlue dragons are associated with the {b}Arcane{/b} spellcasting tradition.",
                Trait.Arcane, new List<FeatName>() { FeatName.Arcana, FeatName.Intimidation }, new Func<Feat, bool>(ft => ft.HasTrait(tAlignment) && ft.HasTrait(Trait.Evil)),
                new List<Trait>() { Trait.Electricity, tDragonType }, new List<Feat>() { dragonConeBreath, dragonLineBreath });
            yield return new EidolonBond(ModManager.RegisterFeatName("GreenDragon", "Green Dragon"), "Your eidolon is a cunning green dragon.", "Your eidolon's breath weapon deals poison damage vs. Fortitude.\n\nGreen dragons are associated with the {b}Arcane{/b} spellcasting tradition.",
                Trait.Arcane, new List<FeatName>() { FeatName.Arcana, FeatName.Intimidation }, new Func<Feat, bool>(ft => ft.HasTrait(tAlignment) && ft.HasTrait(Trait.Evil)),
                new List<Trait>() { Trait.Poison, tDragonType }, new List<Feat>() { dragonConeBreath, dragonLineBreath });
            yield return new EidolonBond(ModManager.RegisterFeatName("RedDragon", "Red Dragon"), "Your eidolon is a tyranical red dragon.", "Your eidolon's breath weapon deals fire damage vs. Reflex.\n\nRed dragons are associated with the {b}Arcane{/b} spellcasting tradition.",
                Trait.Arcane, new List<FeatName>() { FeatName.Arcana, FeatName.Intimidation }, new Func<Feat, bool>(ft => ft.HasTrait(tAlignment) && ft.HasTrait(Trait.Evil)),
                new List<Trait>() { Trait.Fire, tDragonType }, new List<Feat>() { dragonConeBreath, dragonLineBreath });
            yield return new EidolonBond(ModManager.RegisterFeatName("WhiteDragon", "White Dragon"), "Your eidolon is a feral white dragon.", "Your eidolon's breath weapon deals cold damage vs. Reflex.\n\nWhite dragons are associated with the {b}Arcane{/b} spellcasting tradition.",
                Trait.Arcane, new List<FeatName>() { FeatName.Arcana, FeatName.Intimidation }, new Func<Feat, bool>(ft => ft.HasTrait(tAlignment) && ft.HasTrait(Trait.Evil)),
                new List<Trait>() { Trait.Cold, tDragonType }, new List<Feat>() { dragonConeBreath, dragonLineBreath });

            // Metallic
            yield return new EidolonBond(ModManager.RegisterFeatName("CopperDragon", "Copper Dragon"), "Your eidolon is a wily copper dragon.", "Your eidolon's breath weapon deals acid damage vs. Reflex.\n\nCopper dragons are associated with the {b}Arcane{/b} spellcasting tradition.",
                Trait.Arcane, new List<FeatName>() { FeatName.Arcana, FeatName.Intimidation }, new Func<Feat, bool>(ft => ft.FeatName == ftAChaoticGood),
                new List<Trait>() { Trait.Acid, tDragonType }, new List<Feat>() { dragonConeBreath, dragonLineBreath });
            yield return new EidolonBond(ModManager.RegisterFeatName("BronzeDragon", "Bronze Dragon"), "Your eidolon is a scholarly bronze dragon.", "Your eidolon's breath weapon deals electricity damage vs. Reflex.\n\nBronze dragons are associated with the {b}Arcane{/b} spellcasting tradition.",
                Trait.Arcane, new List<FeatName>() { FeatName.Arcana, FeatName.Intimidation }, new Func<Feat, bool>(ft => ft.HasTrait(tAlignment) && ft.HasTrait(Trait.Good)),
                new List<Trait>() { Trait.Electricity, tDragonType }, new List<Feat>() { dragonConeBreath, dragonLineBreath });
            yield return new EidolonBond(ModManager.RegisterFeatName("BrassDragon", "Brass Dragon"), "Your eidolon is a whimsical brass dragon.", "Your eidolon's breath weapon deals fire damage vs. Reflex.\n\nBrass dragons are associated with the {b}Arcane{/b} spellcasting tradition.",
                Trait.Arcane, new List<FeatName>() { FeatName.Arcana, FeatName.Intimidation }, new Func<Feat, bool>(ft => ft.HasTrait(tAlignment) && ft.HasTrait(Trait.Good)),
                new List<Trait>() { Trait.Fire, tDragonType }, new List<Feat>() { dragonConeBreath, dragonLineBreath });
            yield return new EidolonBond(ModManager.RegisterFeatName("GoldDragon", "Gold Dragon"), "Your eidolon is an honourable gold dragon.", "Your eidolon's breath weapon deals fire damage vs. Reflex.\n\nGold dragons are associated with the {b}Divine{/b} spellcasting tradition.",
                Trait.Divine, new List<FeatName>() { FeatName.Arcana, FeatName.Intimidation }, new Func<Feat, bool>(ft => ft.FeatName == ftALawfulGood),
                new List<Trait>() { Trait.Fire, tDragonType }, new List<Feat>() { dragonConeBreath, dragonLineBreath });
            yield return new EidolonBond(ModManager.RegisterFeatName("SilverDragon", "Silver Dragon"), "Your eidolon is a silver white dragon.", "Your eidolon's breath weapon deals cold damage vs. Reflex.\n\nSilver dragons are associated with the {b}Arcane{/b} spellcasting tradition.",
                Trait.Arcane, new List<FeatName>() { FeatName.Religion, FeatName.Intimidation }, new Func<Feat, bool>(ft => ft.HasTrait(tAlignment) && ft.HasTrait(Trait.Good)),
                new List<Trait>() { Trait.Cold, tDragonType }, new List<Feat>() { dragonConeBreath, dragonLineBreath });

            // Imperial
            // TODO: Add imperial dragons

            // Init class
            yield return new ClassSelectionFeat(classSummoner, SummonerFlavour, tSummoner,
                new EnforcedAbilityBoost(Ability.Charisma), 10, new Trait[5] { Trait.Unarmed, Trait.Simple, Trait.UnarmoredDefense, Trait.Reflex, Trait.Perception }, new Trait[2] { Trait.Fortitude, Trait.Will }, 3, SummonerCrunch, new List<Feat>() {
                    // Sublcasses:
                    angelEidolon, angerPhantom, azataEidolon, beastEidolon, devilEidolon, devoPhantomEidolon, dragonEidolon, feyEidolon
                })
                    .WithOnSheet((Action<CalculatedCharacterSheetValues>)(sheet => {
                        sheet.AddFocusSpellAndFocusPoint(tSummoner, Ability.Charisma, spells[SummonerSpellId.EvolutionSurge]);
                        sheet.AddSelectionOption(new SingleFeatSelectionOption("EidolonPortrait", "Eidolon Portrait", 1, ft => ft.HasTrait(tPortraitCategory)));
                        sheet.AddSelectionOption((SelectionOption)new SingleFeatSelectionOption("EvolutionFeat", "Evolution Feat", 1, (Func<Feat, bool>)(ft => ft.HasTrait(tEvolution) && ft.HasTrait(tSummoner))));
                        sheet.AddAtLevel(3, _ => _.SetProficiency(Trait.Perception, Proficiency.Expert));
                        sheet.AddAtLevel(5, _ => _.AddSelectionOption(new MultipleFeatSelectionOption("EidolonASI-5", "Eidolon Ability Boosts", 5, ft => ft.HasTrait(tEidolonASI), 4)));
                        sheet.AddAtLevel(9, _ => _.SetProficiency(Trait.Spell, Proficiency.Expert));
                        sheet.AddAtLevel(9, _ => _.SetProficiency(Trait.Reflex, Proficiency.Expert));
                        sheet.AddAtLevel(11, _ => _.SetProficiency(Trait.Fortitude, Proficiency.Master));
                        sheet.AddAtLevel(11, _ => _.SetProficiency(Trait.Simple, Proficiency.Expert));
                        sheet.AddAtLevel(11, _ => _.SetProficiency(Trait.Unarmed, Proficiency.Expert));
                        sheet.AddAtLevel(11, _ => _.SetProficiency(Trait.UnarmoredDefense, Proficiency.Expert));
                        sheet.AddAtLevel(15, _ => _.SetProficiency(Trait.Will, Proficiency.Master));
                    }));
            //.WithRulesBlockForSpell(spells[SummonerSpellId.EvolutionSurge], tSummoner).WithIllustration((Illustration)illEvolutionSurge).WithIllustration(null)
            //.WithRulesBlockForSpell(spells[SummonerSpellId.EidolonBoost], tSummoner).WithIllustration((Illustration)illEidolonBoost)
            //.WithIllustration(null);

            // Init eidolon ability boosts
            yield return new EvolutionFeat(ModManager.RegisterFeatName("EidolonStrengthBoost", "Strength Boost"), 5, "Your eidolon grows stronger.",
                "Your eidolon increases its strength modifier by +1.", new Trait[] { tEidolonASI }, e => e.Abilities.Strength += 1, null);
            yield return new EvolutionFeat(ModManager.RegisterFeatName("EidolonDexterityBoost", "Dexterity Boost"), 5, "Your eidolon grows fasters.",
                "Your eidolon increases its dexterity modifier by +1.", new Trait[] { tEidolonASI }, e => e.Abilities.Dexterity += 1, null);
            yield return new EvolutionFeat(ModManager.RegisterFeatName("EidolonConstitutionBoost", "Constitution Boost"), 5, "Your eidolon becomes sturdier.",
                "Your eidolon increases its constitution modifier by +1.\n\nThis does not affect its max HP.", new Trait[] { tEidolonASI }, e => e.Abilities.Constitution += 1, null);
            yield return new EvolutionFeat(ModManager.RegisterFeatName("EidolonIntelligenceBoost", "Intelligence Boost"), 5, "Your eidolon grows more cunning.",
                "Your eidolon increases its intelligence modifier by +1.", new Trait[] { tEidolonASI }, e => e.Abilities.Intelligence += 1, null);
            yield return new EvolutionFeat(ModManager.RegisterFeatName("EidolonWisdomBoost", "Wisdom Boost"), 5, "Your eidolon's insticts grow sharper.",
                "Your eidolon increases its wisdom modifier by +1.", new Trait[] { tEidolonASI }, e => e.Abilities.Wisdom += 1, null);
            yield return new EvolutionFeat(ModManager.RegisterFeatName("EidolonCharismaBoost", "Charisma Boost"), 5, "Your eidolon's presence grows.",
                "Your eidolon increases its charisma modifier by +1.", new Trait[] { tEidolonASI }, e => e.Abilities.Charisma += 1, null);

            // Generate energy optinon feats
            DamageKind[] energyDamageTypes = new DamageKind[] { DamageKind.Acid, DamageKind.Cold, DamageKind.Electricity, DamageKind.Fire, DamageKind.Sonic, DamageKind.Positive, DamageKind.Negative };
            DamageKind[] alignmentDamageTypes = new DamageKind[] { DamageKind.Good, DamageKind.Evil, DamageKind.Lawful, DamageKind.Chaotic };

            // Energy heart
            foreach (DamageKind energy in energyDamageTypes.Concat(alignmentDamageTypes)) {
                Feat temp = new Feat(ModManager.RegisterFeatName("EnergyHeart" + energy.HumanizeTitleCase2(), "Energy Heart: " + energy.HumanizeTitleCase2()), "Your eidolon's corporeal form is infused with a particular element.", $"Your eidolon's chosen natural weapon deals {energy.HumanizeTitleCase2()} damage, and it gains {energy.HumanizeTitleCase2()} resistance equal to half your level (minimum 1)", new List<Trait>() { DamageToTrait(energy), tEnergyHeartDamage }, null);
                if (alignmentDamageTypes.Contains(energy)) {
                    temp.WithPrerequisite((sheet => {
                        if (sheet.AllFeats.FirstOrDefault(ft => divineTypes.Contains(ft.FeatName.HumanizeTitleCase2())) == null)
                            return false;
                        if (sheet.AllFeats.FirstOrDefault(ft => ft.HasTrait(DamageToTrait(energy)) && ft.HasTrait(tAlignment)) == null)
                            return false;
                        return true;
                    }), $"Your eidolon must be of {DamageToTrait(energy).HumanizeTitleCase2()} alignment, and celestial origin.");
                }
                yield return temp;
            }

            // Eidolon's Wrath
            //List<Feat> ewSubFeats = new List<Feat>();
            //foreach (DamageKind energy in energyDamageTypes.Concat(alignmentDamageTypes)) {
            //    EvolutionFeat temp = new EvolutionFeat(ModManager.RegisterFeatName("EidolonsWrath" + energy.HumanizeTitleCase2(), "Eidolon's Wrath: " + energy.HumanizeTitleCase2()), 6, "", $"The Eidolon's Wrath focus spell deals {energy.HumanizeTitleCase2()} damage", new Trait[] { DamageToTrait(energy) }, e => e.AddQEffect(new QEffect() { Id = qfEidolonsWrath, Tag = energy }), null);
            //    if (alignmentDamageTypes.Contains(energy)) {
            //        temp.WithPrerequisite((sheet => {
            //            if (sheet.AllFeats.FirstOrDefault(ft => divineTypes.Contains(ft.FeatName.HumanizeTitleCase2())) == null)
            //                return false;
            //            if (sheet.AllFeats.FirstOrDefault(ft => ft.HasTrait(DamageToTrait(energy)) && ft.HasTrait(tAlignment)) == null)
            //                return false;
            //            return true;
            //        }), $"Your eidolon must be of {DamageToTrait(energy).HumanizeTitleCase2()} alignment, and celestial origin.");
            //    }
            //    ewSubFeats.Add(temp);
            //}

            List<Feat> ewSubFeats = new List<Feat>();
            foreach (DamageKind energy in energyDamageTypes.Concat(alignmentDamageTypes)) {
                Feat temp = new Feat(ModManager.RegisterFeatName("EidolonsWrath" + energy.HumanizeTitleCase2(), "Eidolon's Wrath: " + energy.HumanizeTitleCase2()), "", $"The Eidolon's Wrath focus spell deals {energy.HumanizeTitleCase2()} damage", new List<Trait> { DamageToTrait(energy), tEidolonsWrathType }, null);
                if (alignmentDamageTypes.Contains(energy)) {
                    temp.WithPrerequisite((sheet => {
                        if (sheet.AllFeats.FirstOrDefault(ft => divineTypes.Contains(ft.FeatName.HumanizeTitleCase2())) == null)
                            return false;
                        if (sheet.AllFeats.FirstOrDefault(ft => ft.HasTrait(DamageToTrait(energy)) && ft.HasTrait(tAlignment)) == null)
                            return false;
                        return true;
                    }), $"Your eidolon must be of {DamageToTrait(energy).HumanizeTitleCase2()} alignment, and celestial origin.");
                }
                ewSubFeats.Add(temp);
            }

            // Init TrueFeats
            yield return new TrueFeat(ftAbundantSpellcasting1, 1, "Your strong connect to your eidolon grants you additional spells.",
                "You gain an extra level 1 spell slot, and learn a 1st level spell based on your spellcasting tradition:\n" +
                "• Arcane. " + AllSpells.CreateModernSpellTemplate(SpellId.MageArmor, tSummoner).ToSpellLink() +
                "\n• Divine. " + AllSpells.CreateModernSpellTemplate(SpellId.Bless, tSummoner).ToSpellLink() +
                "\n• Primal. " + AllSpells.CreateModernSpellTemplate(SpellId.Grease, tSummoner).ToSpellLink() +
                "\n• Occult. " + AllSpells.CreateModernSpellTemplate(SpellId.Fear, tSummoner).ToSpellLink() +
                "\n\nUnlike your other summoner spells, the spell you gain from this feat is not a signature spell.",
                new Trait[2] { tSummoner, Trait.Homebrew })
            .WithOnSheet((Action<CalculatedCharacterSheetValues>)(values => {
                if (!values.SpellRepertoires.ContainsKey(tSummoner))
                    return;
                ++values.SpellRepertoires[tSummoner].SpellSlots[1];
            }));

            yield return new TrueFeat(ftAbundantSpellcasting4, 4, "Your strong connect to your eidolon grants you additional spells.",
                "You gain an extra level 2 spell slot, and learn a 2nd level spell based on your spellcasting tradition:\n" +
                "• Arcane. " + AllSpells.CreateModernSpellTemplate(SpellId.Blur, tSummoner).ToSpellLink() +
                "\n• Divine. " + AllSpells.CreateModernSpellTemplate(SpellId.BloodVendetta, tSummoner).ToSpellLink() +
                "\n• Primal. " + AllSpells.CreateModernSpellTemplate(SpellId.Barkskin, tSummoner).ToSpellLink() +
                "\n• Occult. " + AllSpells.CreateModernSpellTemplate(SpellId.HideousLaughter, tSummoner).ToSpellLink() +
                "\n\nUnlike your other summoner spells, the spell you gain from this feat is not a signature spell.",
                new Trait[2] { tSummoner, Trait.Homebrew })
            .WithOnSheet((Action<CalculatedCharacterSheetValues>)(values => {
                if (!values.SpellRepertoires.ContainsKey(tSummoner))
                    return;
                ++values.SpellRepertoires[tSummoner].SpellSlots[2];
            }));

            yield return new EvolutionFeat(ModManager.RegisterFeatName("Airborn Form"), 1, "Your eidolon can take to the skies, either via great wings, a blimp like appendage or levitation.",
                "Your eidolon can fly. It gains a fly Speed equal to its Speed.", new Trait[] { Trait.Homebrew, tSummoner }, e => e.AddQEffect(new QEffect { Id = QEffectId.Flying }), null);

            yield return new EvolutionFeat(ModManager.RegisterFeatName("Advanced Weaponry"), 1, "Your eidolon's attack evolves.", "Choose one of your eidolon's starting melee unarmed attacks. " +
                "It gains one of the following traits, chosen when you gain the feat: disarm, grapple, shove, trip, or versatile piercing or slashing.", new Trait[] { tSummoner }, e => e.AddQEffect(new QEffect {
                    StartOfCombat = (async (qf) => {
                        string atkType = GetSummoner(qf.Owner).PersistentCharacterSheet.Calculated.AllFeats.FirstOrDefault((Func<Feat, bool>)(ft => ft.HasTrait(tAdvancedWeaponryAtkType))).Name;
                        Item naturalWeapon;
                        if (atkType == "Primary Unarmed Attack")
                            naturalWeapon = qf.Owner.UnarmedStrike;
                        else
                            naturalWeapon = qf.Owner.QEffects.FirstOrDefault(qf => qf.AdditionalUnarmedStrike != null && qf.AdditionalUnarmedStrike.WeaponProperties.Melee).AdditionalUnarmedStrike;

                        List<Feat> test = GetSummoner(qf.Owner).PersistentCharacterSheet.Calculated.AllFeats;
                        string traitName = GetSummoner(qf.Owner).PersistentCharacterSheet.Calculated.AllFeats.FirstOrDefault((Func<Feat, bool>)(ft => ft.HasTrait(tAdvancedWeaponryAtkTrait))).Name;
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
                            case "Versatile Bludgeoning":
                                trait = Trait.VersatileB;
                                break;
                            default:
                                trait = Trait.None;
                                break;
                        }

                        if (naturalWeapon.HasTrait(trait)) {
                            return;
                        }

                        naturalWeapon.Traits.Add(trait);
                    })
                }), new List<Feat> {
                new Feat(ModManager.RegisterFeatName("AW_PrimaryUnarmedAttack", "Primary Unarmed Attack"), "", "This evolution will apply to your eidolon's primary natural weapon attack.", new List<Trait>() { tAdvancedWeaponryAtkType }, null)
                .WithOnSheet(sheet => {
                    sheet.AddSelectionOptionRightNow((SelectionOption)new SingleFeatSelectionOption("AdvancedWeaponryTrait", "Eidolon Advanced Weaponry Trait", sheet.CurrentLevel, (Func<Feat, bool>)(ft => ft.HasTrait(tAdvancedWeaponryAtkTrait))));
                }),
                new Feat(ModManager.RegisterFeatName("AW_SecondaryUnarmedAttack", "Secondary Unarmed Attack"), "", "This evolution will apply to your eidolon's secondary natural weapon attack.", new List<Trait>() { tAdvancedWeaponryAtkType }, null)
                .WithOnSheet(sheet => {
                    sheet.AddSelectionOptionRightNow((SelectionOption)new SingleFeatSelectionOption("AdvancedWeaponryTrait", "Eidolon Advanced Weaponry Trait", sheet.CurrentLevel, (Func<Feat, bool>)(ft => ft.HasTrait(tAdvancedWeaponryAtkTrait))));
                })
            });

            yield return new TrueFeat(ModManager.RegisterFeatName("LifelinkSurgeFeat", "Lifelink Surge"), 4, "", "You learn the lifelink surge link spell. Increase the number of Focus Points in your focus pool by 1.", new Trait[] { tSummoner }, null).WithOnSheet(sheet => {
                sheet.AddFocusSpellAndFocusPoint(tSummoner, Ability.Charisma, spells[SummonerSpellId.LifelinkSurge]);
            })
            .WithRulesBlockForSpell(spells[SummonerSpellId.LifelinkSurge], tSummoner);

            yield return new TrueFeat(ModManager.RegisterFeatName("ExtendBoostFeat", "Extend Boost"), 1, "You can increase the duration of your eidolon's boosts.", "You learn the extend boost link spell. Increase the number of Focus Points in your focus pool by 1.",
                new Trait[] { tSummoner }, null).WithOnSheet(sheet => {
                    sheet.AddFocusSpellAndFocusPoint(tSummoner, Ability.Charisma, spells[SummonerSpellId.ExtendBoost]);
                })
            .WithRulesBlockForSpell(spells[SummonerSpellId.ExtendBoost], tSummoner);

            yield return new EvolutionFeat(ModManager.RegisterFeatName("Alacritous Action"), 2, "Your eidolon moves more quickly.", "Your eidolon gains a +10-foot status bonus to its Speed.", new Trait[] { tSummoner }, e => e.AddQEffect(new QEffect {
                BonusToAllSpeeds = (qf => {
                    return new Bonus(2, BonusType.Status, "Alacritous Action");
                })
            }), null);

            yield return new EvolutionFeat(ModManager.RegisterFeatName("Tandem Movement {icon:FreeAction}"), 4, "You and your eidolon move together.", "You and your eidolon gain the Tandem Movement action. After toggling on this action, your next action must be to stride. " +
                "Then, your bonded partner gains an immediate turn where they can do the same.", new Trait[] { tSummoner, tTandem }, e => e.AddQEffect(new QEffect {
                    ProvideActionIntoPossibilitySection = (qf, section) => {
                        if (section.Name == "Tandem Actions") {
                            return GenerateTandemMovementAction(qf.Owner, GetSummoner(qf.Owner), GetSummoner(qf.Owner));
                        }
                        return null;
                    }
                }), null)
            .WithOnCreature((sheet, self) => {
                self.AddQEffect(new QEffect {
                    ProvideActionIntoPossibilitySection = (qf, section) => {
                        Creature eidolon = GetEidolon(qf.Owner);
                        if (eidolon != null) {
                            if (section.Name == "Tandem Actions") {
                                return GenerateTandemMovementAction(qf.Owner, GetSummoner(qf.Owner), GetSummoner(qf.Owner));
                            }
                        }
                        return null;
                    }
                });
            })
            .WithIllustration(illTandemMovement);

            yield return new EvolutionFeat(ModManager.RegisterFeatName("Tandem Strike {icon:FreeAction}"), 6, "You and your eidolon strike together.",
                "You and your eidolon each attack, seamlessly targeting the same foe without interfering with each other's movements. Your eidolon makes a melee Strike, " +
                "and then you make a melee Strike against the same creature. Both attacks count toward your multiple attack penalty, but the penalty doesn't increase until " +
                "after both attacks have been made.", new Trait[] { tSummoner, tTandem }, e => e.AddQEffect(new QEffect {
                    ProvideActionIntoPossibilitySection = (qf, section) => {
                        if (section.Name == "Tandem Actions") {
                            return GenerateTandemStrikeAction(qf.Owner, GetSummoner(qf.Owner), GetSummoner(qf.Owner));
                        }
                        return null;
                    }
                }), null)
            .WithOnCreature((sheet, self) => {
                self.AddQEffect(new QEffect {
                    ProvideActionIntoPossibilitySection = (qf, section) => {
                        Creature eidolon = GetEidolon(qf.Owner);
                        if (eidolon != null) {
                            if (section.Name == "Tandem Actions") {
                                return GenerateTandemStrikeAction(qf.Owner, GetSummoner(qf.Owner), GetSummoner(qf.Owner));
                            }
                        }
                        return null;
                    }
                });
            })
            .WithIllustration(illTandemStrike);

            yield return new EvolutionFeat(ModManager.RegisterFeatName("Eidolon's Wrath {icon:TwoActions}"), 6, "Your eidolon gains the ability to expel its planar essence in a surge of destructive energy.",
                "Your eidolon releases a powerful energy attack that deals 5d6 damage of the type you chose when you took the Eidolon's Wrath feat, with a basic Reflex save.",
                new Trait[] { tSummoner }, e => {
                    Feat dmgTypeFeat = GetSummoner(e).PersistentCharacterSheet.Calculated.AllFeats.FirstOrDefault(ft => ft.HasTrait(tEidolonsWrathType));
                    if (dmgTypeFeat != null) {
                        e.AddQEffect(new QEffect() { Id = qfEidolonsWrath, Tag = TraitToDamage(dmgTypeFeat.Traits[0]) });
                        e.Spellcasting.PrimarySpellcastingSource.FocusSpells.Add(AllSpells.CreateSpellInCombat(spells[SummonerSpellId.EidolonsWrath], e, 3, tSummoner));
                    }
                }, ewSubFeats)
            .WithOnSheet(sheet => {
                if (sheet.FocusPointCount < 3) {
                    sheet.FocusPointCount += 1;
                }
            })
            .WithRulesBlockForSpell(spells[SummonerSpellId.EidolonsWrath], tSummoner)
            .WithIllustration(IllustrationName.DivineWrath);

            // Generate spell selection feats
            List<Spell> allSpells = AllSpells.All.Where(sp => (sp.HasTrait(Trait.Cantrip) || sp.SpellLevel <= 2) && !sp.HasTrait(Trait.Focus) && !sp.HasTrait(Trait.Uncommon)).ToList();

            List<SpellId> moddedSpells = (List<SpellId>)typeof(ModManager).GetProperty("NewSpells", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static).GetValue(null);
            List<Spell> moddedSpells2 = new List<Spell>();
            foreach (SpellId spell in (List<SpellId>)moddedSpells) {
                moddedSpells2.Add(AllSpells.CreateModernSpellTemplate(spell, tSummoner));
            }
            moddedSpells2 = moddedSpells2.Where(sp => (sp.HasTrait(Trait.Cantrip) || sp.SpellLevel <= 2) && (!sp.HasTrait(Trait.Focus) && !sp.HasTrait(Trait.Uncommon))).ToList();
            allSpells = allSpells.Concat(moddedSpells2).ToList();

            foreach (Spell spell in allSpells) {
                List<Trait> traits = new List<Trait>() { tEidolonSpellFeat };

                if (spell.HasTrait(Trait.Cantrip)) {
                    yield return new EvolutionFeat(ModManager.RegisterFeatName($"EidolonSpellGainFeat({spell.Name}-Cantrip)", spell.Name), spell.MinimumSpellLevel, "", AllSpells.CreateModernSpell(spell.SpellId, null, 1, false, spell.CombatActionSpell.SpellInformation).CombatActionSpell.Description, spell.Traits.ToList().Concat(traits).ToArray(), e => {
                        e.Spellcasting.PrimarySpellcastingSource.WithSpells(new SpellId[] { spell.SpellId });
                    }, null)
                    .WithIllustration(spell.Illustration);
                }

                if (spell.MinimumSpellLevel <= 1 && !spell.HasTrait(Trait.Cantrip)) {
                    traits.Add(tEidolonSpellLvl1);
                    yield return new EvolutionFeat(ModManager.RegisterFeatName($"EidolonSpellGainFeat({spell.Name}-1)", spell.Name), spell.MinimumSpellLevel, "", AllSpells.CreateModernSpell(spell.SpellId, null, 1, false, spell.CombatActionSpell.SpellInformation).CombatActionSpell.Description, spell.Traits.ToList().Concat(traits).ToArray(), e => {
                        e.Spellcasting.PrimarySpellcastingSource.WithSpells(new SpellId[] { spell.SpellId });
                    }, null)
                    .WithIllustration(spell.Illustration);
                }
                traits.Remove(tEidolonSpellLvl1);

                if (spell.MinimumSpellLevel <= 2 && !spell.HasTrait(Trait.Cantrip)) {
                    traits.Add(tEidolonSpellLvl2);
                    yield return new EvolutionFeat(ModManager.RegisterFeatName($"EidolonSpellGainFeat({spell.Name}-2)", spell.Name), spell.MinimumSpellLevel, "", AllSpells.CreateModernSpell(spell.SpellId, null, 1, false, spell.CombatActionSpell.SpellInformation).CombatActionSpell.Description, spell.Traits.ToList().Concat(traits).ToArray(), e => {
                        e.Spellcasting.PrimarySpellcastingSource.WithSpells(null, new SpellId[] { spell.SpellId });
                    }, null)
                    .WithIllustration(spell.Illustration);
                }
                traits.Remove(tEidolonSpellLvl2);
            }

            yield return new EvolutionFeat(ftMagicalUnderstudy, 2, "Your eidolon evolves to cast spells.",
                "Your eidolon gains the Cast a Spell activity and learns two cantrips of its tradition, which it can cast as innate spells.\n\nYour eidolon's spell DC and attack moddifier for these spells is equal to yours.",
                new Trait[] { tSummoner }, (Action<Creature>)null, null)
            .WithOnSheet(sheet => {
                if (sheet.HasFeat(scFeyEidolon)) {
                    sheet.AddSelectionOptionRightNow(new MultipleFeatSelectionOption("EidolonCantrip", "Eidolon Cantrips", -1, ft => ft.HasTrait(tEidolonSpellFeat) && ft.HasTrait(Trait.Cantrip) &&
                    (ft.HasTrait(sheet.SpellRepertoires[tSummoner].SpellList) || (ft.HasTrait(Trait.Arcane) && (ft.HasTrait(Trait.Enchantment) || ft.HasTrait(Trait.Illusion) || ft.HasTrait(Trait.Mental)))), 2));
                } else {
                    sheet.AddSelectionOptionRightNow(new MultipleFeatSelectionOption("EidolonCantrip", "Eidolon Cantrips", -1, ft => ft.HasTrait(tEidolonSpellFeat) && ft.HasTrait(Trait.Cantrip) && ft.HasTrait(sheet.SpellRepertoires[tSummoner].SpellList), 2));
                }
            });

            yield return new EvolutionFeat(ftMagicalAdept, 8, "Your eidolon gains more magic.",
                "Choose one 2nd-level spell and one 1st-level spell of your eidolon's tradition. Your eidolon can cast them each once per day as innate spells.",
                new Trait[] { tSummoner }, (Action<Creature>)null, null)
            .WithOnSheet(sheet => {
                if (sheet.HasFeat(scFeyEidolon)) {
                    sheet.AddSelectionOptionRightNow(new MultipleFeatSelectionOption("Eidolon2ndLevelSpell", "Level 2 Eidolon Spell", -1, ft => ft.HasTrait(tEidolonSpellFeat) &&
                    ft.HasTrait(tEidolonSpellLvl2) && (ft.HasTrait(sheet.SpellRepertoires[tSummoner].SpellList) || (ft.HasTrait(Trait.Arcane) && (ft.HasTrait(Trait.Enchantment) || ft.HasTrait(Trait.Illusion) || ft.HasTrait(Trait.Mental)))), 1));
                    sheet.AddSelectionOptionRightNow(new MultipleFeatSelectionOption("Eidolon1stLevelSpell", "Level 1 Eidolon Spell", -1, ft => ft.HasTrait(tEidolonSpellFeat) &&
                    ft.HasTrait(tEidolonSpellLvl1) && (ft.HasTrait(sheet.SpellRepertoires[tSummoner].SpellList) || (ft.HasTrait(Trait.Arcane) && (ft.HasTrait(Trait.Enchantment) || ft.HasTrait(Trait.Illusion) || ft.HasTrait(Trait.Mental)))), 1));
                } else {
                    sheet.AddSelectionOptionRightNow(new MultipleFeatSelectionOption("Eidolon2ndLevelSpell", "Level 2 Eidolon Spell", -1, ft => ft.HasTrait(tEidolonSpellFeat) && ft.HasTrait(tEidolonSpellLvl2) && ft.HasTrait(sheet.SpellRepertoires[tSummoner].SpellList), 1));
                    sheet.AddSelectionOptionRightNow(new MultipleFeatSelectionOption("Eidolon1stLevelSpell", "Level 1 Eidolon Spell", -1, ft => ft.HasTrait(tEidolonSpellFeat) && ft.HasTrait(tEidolonSpellLvl1) && ft.HasTrait(sheet.SpellRepertoires[tSummoner].SpellList), 1));
                }
            });

            yield return new EvolutionFeat(ModManager.RegisterFeatName("Eidolon's Opportunity {icon:Reaction}"), 6,
                "Your eidolon makes a melee Strike against the triggering creature.", "If the attack is a critical hit and the trigger was a manipulate action, " +
                "your eidolon disrupts that action. This Strike doesn't count toward your multiple attack penalty, and your multiple attack penalty doesn't apply to this Strike.",
                new Trait[] { tSummoner }, e => e.AddQEffect(QEffect.AttackOfOpportunity("Eidolon's Opportunity", "Can make attacks of opportunity, and disrupt actions on a critical hit.", null)), null);

            yield return new EvolutionFeat(ModManager.RegisterFeatName("Constricting Hold {icon:Action}"), 8,
                "Your eidolon develops a long serpentine appendage, or a powerful choking grip, perfect for constricting the life out of its victims.", "Your eidolon constricts the creature, dealing bludgeoning damage equal to your eidolon's level plus its Strength modifier, with a basic Fortitude save against your spell DC.",
                new Trait[] { tSummoner }, e => e.AddQEffect(new QEffect() {
                    ProvideContextualAction = (qf => {
                        List<Creature> grappledCreatures = qf.Owner.Battle.AllCreatures.Where(c => c.OwningFaction != e.OwningFaction && c.HasEffect(QEffectId.Grappled) && c.FindQEffect(QEffectId.Grappled).Source == e).ToList();
                        if (grappledCreatures.Count > 0) {
                            int saveDC = GetSummoner(e).ClassOrSpellDC();
                            return (Possibility)(ActionPossibility)new CombatAction(e, illConstrictingHold, "Constricting Hold", new Trait[] { },
                                "{b}Target{/b} 1 creature currently grappled or restrained by your eidolon\n{b}Saving throw{/b} basic Fortitude\n\nDeal level + Strength modifier bludgeoning damage (basic Fortitude save mitigates) to the grappled creature.",
                                (Target) Target.Distance(1).WithAdditionalConditionOnTargetCreature(new GrappledCreatureOnlyCreatureTargetingRequirement()))
                            .WithActionCost(1)
                            .WithSoundEffect(SfxName.Boneshaker)
                            .WithSavingThrow(new SavingThrow(Defense.Fortitude, (_ => saveDC)))
                            .WithEffectOnChosenTargets(async (action, self, targets) => {
                                CommonSpellEffects.DealBasicDamage(action, self, targets.ChosenCreature, action.CheckResult, DiceFormula.FromText($"{e.Level + e.Abilities.Strength}"), DamageKind.Bludgeoning);
                            });
                        }
                        return null;
                    }),
                }), null)
                .WithIllustration(illConstrictingHold);

            yield return new TrueFeat(ftBoostSummons, 8, "Augmenting your eidolon extends to creatures you summon.",
                $"When you cast {AllSpells.CreateSpellLink(spells[SummonerSpellId.EidolonBoost], tSummoner)} or {AllSpells.CreateSpellLink(spells[SummonerSpellId.ReinforceEidolon], tSummoner)}, " +
                "in addition to your eidolon, it also targets your summoned creatures within 60 feet.", new Trait[] { tSummoner }, null)
            .WithIllustration(illReinforceEidolon);

            yield return new TrueFeat(ModManager.RegisterFeatName("Master Summoner"), 6, "You've become particularly adept at calling upon the aid of lesser beings, in addition to your eidolon.", "You gain an additional slot of your spell level, that can only be used to cast summon spells.", new Trait[] { tSummoner }, null)
            .WithOnSheet(sheet => {
                sheet.SpellRepertoires[tSummoner].SpellSlots[sheet.MaximumSpellLevel]++;
            })
            .WithOnCreature((sheet, self) => {
                self.AddQEffect(new QEffect("Master Summoner", "You gain an additional max level spell slot that can only be used to cast summon spells.") {
                    StartOfCombat = async qf => {
                        if (qf.Owner.PersistentUsedUpResources.UsedUpActions.Contains("Master Summoner")) {
                            qf.Name = "Master Summoner (Expended)";
                        }
                        return;
                    },
                    AfterYouTakeAction = async (qf, action) => {
                        if (action.SpellId == SpellId.None) {
                            return;
                        }

                        if (!action.Name.StartsWith("Summon ") && action.Name != "Animate Dead") {
                            return;
                        }

                        if (action.SpellLevel != action.Owner.PersistentCharacterSheet.Calculated.MaximumSpellLevel) {
                            return;
                        }

                        qf.Owner.PersistentUsedUpResources.UsedUpActions.Add("Master Summoner");
                        qf.Name = "Master Summoner (Expended)";
                    },
                    PreventTakingAction = action => {
                        if (action.Owner.PersistentUsedUpResources.UsedUpActions.Contains("Master Summoner")) {
                            return null;
                        }

                        if (action.SpellId == SpellId.None) {
                            return null;
                        }

                        if (action.Name.StartsWith("Summon ") || action.Name == "Animate Dead") {
                            return null;
                        }

                        if (action.SpellLevel != action.Owner.PersistentCharacterSheet.Calculated.MaximumSpellLevel) {
                            return null;
                        }

                        if (action.Owner.Spellcasting.PrimarySpellcastingSource.SpontaneousSpellSlots[action.Owner.PersistentCharacterSheet.Calculated.MaximumSpellLevel] != 1) {
                            return null;
                        }

                        return "This spell slot can only be used to cast summoning spells.";
                    },
                });
            });

            yield return new TrueFeat(ModManager.RegisterFeatName("Ostentatious Arrival {icon:FreeAction}"), 6, "Your summons manifest in an explosive wave of destructive energy.",
                "If the next action you take is to Manifest your Eidolon as a three-action activity, or to Cast a three-action summoning Spell, the creature appears in an explosion. " +
                "All creatures in a 10-foot emanation around the creature you summoned or manifested take 1d4 fire damage per spell level for a summoning spell, or 1d4 damage per 2 levels for Manifesting your Eidolon. " +
                "If your eidolon has an elemental trait, they deal that damage type instead.",
                new Trait[] { tSummoner, Trait.Concentrate, Trait.Metamagic, Trait.Manipulate }, null)
            .WithOnSheet(sheet => {
                sheet.SpellRepertoires[tSummoner].SpellSlots[sheet.MaximumSpellLevel]++;
            })
            .WithIllustration(illOstentatiousArrival)
            .WithOnCreature((sheet, self) => {
                self.AddQEffect(new QEffect() {
                    ProvideMainAction = qf => {
                        Possibility output = null;
                        if (qf.Owner.HasEffect(qfOstentatiousArrival)) {
                            output = (ActionPossibility)new CombatAction(qf.Owner, illOstentatiousArrival, "Cancel Ostentatious Arrival", new Trait[] { tSummoner }, "Cancel ostentatious arrival, to cast a non-explosive summoning spell instead.", Target.Self())
                            .WithEffectOnSelf(self => self.RemoveAllQEffects(effect => effect.Id == qfOstentatiousArrival))
                            .WithSoundEffect(SfxName.Button)
                            .WithActionCost(0);

                            output.WithPossibilityGroup("Summoner");
                            return output;
                        }

                        output = (ActionPossibility)new CombatAction(qf.Owner, illOstentatiousArrival, "Ostentatious Arrival", new Trait[] { tSummoner, Trait.Concentrate, Trait.Metamagic, Trait.Manipulate },
                            "If the next action you take is to Manifest your Eidolon as a three-action activity, or to Cast a three-action summoning Spell, the creature appears in an explosion. " +
                            "All creatures in a 10-foot emanation around the creature you summoned or manifested take 1d4 fire damage per spell level for a summoning spell, or 1d4 damage per 2 levels for Manifesting your Eidolon. " +
                            "If your eidolon has an elemental trait, they deal that damage type instead.", Target.Self())
                        .WithSoundEffect(SfxName.Abjuration)
                        .WithActionCost(0)
                        .WithEffectOnSelf(self => {
                            self.AddQEffect(new QEffect("Ostentatious Arrival Toggled", "Your next summoning spell will create a 10-foot burst, dealing 1d4 fire damage to all creatures caught inside.") {
                                Id = qfOstentatiousArrival,
                                PreventTakingAction = action => {
                                    if (action.Name == "Cancel Ostentatious Arrival" || action.Name == "Manifest Eidolon" || action.Name.StartsWith("Summon ") || action.Name == "Animate Dead") {
                                        return null;
                                    }

                                    return "Ostentatious Arrival can only be used with summon spells, or the manifest eidolon action.";
                                },
                                YouBeginAction = async (qf, action) => {
                                    // Check if summon spell
                                    if ((action.SpellId == SpellId.None || !(action.Name.StartsWith("Summon ") || action.Name == "Animate Dead")) && action.Name != "Manifest Eidolon") {
                                        return;
                                    }

                                    Tile target = action.ChosenTargets.ChosenTile;
                                    if (target != null) {
                                        string damage = "";
                                        DamageKind type = DamageKind.Fire;
                                        Trait? element = action.Traits.FirstOrDefault(t => TraitToDamage(t) != DamageKind.Untyped);
                                        if (action.Name == "Manifest Eidolon") {
                                            damage = $"{(qf.Owner.Level + 1) / 2}d4";
                                            Feat energyHeart = qf.Owner.PersistentCharacterSheet.Calculated.AllFeats.FirstOrDefault(ft => ft.HasTrait(tEnergyHeartDamage));
                                            if (energyHeart != null) {
                                                type = TraitToDamage(energyHeart.Traits[0]);
                                            }
                                        } else if (element != null) {
                                            type = TraitToDamage((Trait)element);
                                        } else {
                                            damage = $"{action.SpellLevel}d4";
                                        }
                                        Sfxs.Play(SfxName.Fireball, 0.5f);
                                        BurstAreaTarget burst = new BurstAreaTarget(0, 2);
                                        await CommonAnimations.CreateConeAnimation(qf.Owner.Battle, target.ToCenterVector(), DetermineTilesCopy(qf.Owner, burst, target.ToCenterVector()).TargetedTiles.ToList(), 20, ProjectileKind.Cone, IllustrationName.Fireball);
                                        // Target
                                        foreach (Creature creature in qf.Owner.Battle.AllCreatures) {
                                            if (creature.DistanceTo(target) <= 2) {
                                                List<QEffect> effects = creature.QEffects.ToList();
                                                for (int i = 0; i < effects.Count; i++) {
                                                    if (effects[i].YouAreTargeted != null) {
                                                        await effects[i].YouAreTargeted(effects[i], action);
                                                    }
                                                }
                                            }
                                        }
                                        // Damage
                                        foreach (Creature creature in qf.Owner.Battle.AllCreatures) {
                                            if (creature.DistanceTo(target) <= 2) {
                                                await qf.Owner.DealDirectDamage(action, DiceFormula.FromText(damage), creature, CheckResult.Success, type);
                                            }
                                        }
                                        // Resolve target
                                        foreach (Creature creature in qf.Owner.Battle.AllCreatures) {
                                            if (creature.DistanceTo(target) <= 2) {
                                                List<QEffect> effects = creature.QEffects.ToList();
                                                for (int i = 0; i < effects.Count; i++) {
                                                    if (effects[i].AfterYouAreTargeted != null) {
                                                        await effects[i].AfterYouAreTargeted(effects[i], action);
                                                    }
                                                }
                                            }
                                        }
                                        qf.ExpiresAt = ExpirationCondition.Immediately;
                                    }
                                }
                            });
                        });

                        output.WithPossibilityGroup("Summoner");
                        return output;
                    },
                    AfterYouTakeAction = async (qf, action) => {
                        if (action.SpellId == SpellId.None) {
                            return;
                        }

                        if (!action.Name.StartsWith("Summon ") && action.Name != "Animate Dead") {
                            return;
                        }

                        if (action.SpellLevel != action.Owner.PersistentCharacterSheet.Calculated.MaximumSpellLevel) {
                            return;
                        }

                        qf.Owner.PersistentUsedUpResources.UsedUpActions.Add("Master Summoner");
                        qf.Name = "Master Summoner (Expended)";
                    },
                    PreventTakingAction = action => {
                        if (action.Owner.PersistentUsedUpResources.UsedUpActions.Contains("Master Summoner")) {
                            return null;
                        }

                        if (action.SpellId == SpellId.None) {
                            return null;
                        }

                        if (action.Name.StartsWith("Summon ") || action.Name == "Animate Dead") {
                            return null;
                        }

                        if (action.SpellLevel != action.Owner.PersistentCharacterSheet.Calculated.MaximumSpellLevel) {
                            return null;
                        }

                        if (action.Owner.Spellcasting.PrimarySpellcastingSource.SpontaneousSpellSlots[action.Owner.PersistentCharacterSheet.Calculated.MaximumSpellLevel] != 1) {
                            return null;
                        }

                        return "This spell slot can only be used to cast summoning spells.";
                    },
                });
            });

            yield return new TrueFeat(ModManager.RegisterFeatName("Reinforce Eidolon"), 2, "You buffer your eidolon.", "You gain the reinforce eidolon link cantrip.", new Trait[] { tSummoner }, null)
            .WithOnSheet(sheet => {
                if (!sheet.SpellRepertoires.ContainsKey(tSummoner)) {
                    return;
                }
                sheet.SpellRepertoires[tSummoner].SpellsKnown.Add(AllSpells.CreateModernSpellTemplate(spells[SummonerSpellId.ReinforceEidolon], tSummoner, sheet.MaximumSpellLevel));
            })
            .WithRulesBlockForSpell(spells[SummonerSpellId.ReinforceEidolon], tSummoner);

            yield return new EvolutionFeat(ModManager.RegisterFeatName("Energy Heart"), 1, "Your eidolon's heart beats with energy.",
                "Choose an energy damage type other than force. One of your eidolon's unarmed attacks changes its damage type to the chosen type, and it gains resistance to that type equal to half your level (minimum 1).",
                new Trait[] { tSummoner }, e => e.AddQEffect(new QEffect {
                StartOfCombat = (async (qf) => {
                    DamageKind kind = TraitToDamage(GetSummoner(qf.Owner).PersistentCharacterSheet.Calculated.AllFeats.FirstOrDefault(ft => ft.HasTrait(tEnergyHeartDamage)).Traits[0]);
                    qf.Owner.WeaknessAndResistance.AddResistance(kind, Math.Max(1, (qf.Owner.Level / 2)));
                })
            }), new List<Feat> {
                new Feat(ModManager.RegisterFeatName("EH_PrimaryUnarmedAttack", "Primary Unarmed Attack"), "", "This evolution will change the damage type of your eidolon's primary natural weapon attack.", new List<Trait>() { tEnergyHeartWeapon }, null).WithOnSheet(sheet => {
                sheet.AddSelectionOptionRightNow((SelectionOption)new SingleFeatSelectionOption("EnergyHeartType", "Energy Heart Type", sheet.CurrentLevel, (Func<Feat, bool>)(ft => ft.HasTrait(tEnergyHeartDamage))));
            }),
                new Feat(ModManager.RegisterFeatName("EH_SecondaryUnarmedAttack", "Secondary Unarmed Attack"), "", "This evolution will change the damage type of your eidolon's secondary natural weapon attack.", new List<Trait>() { tEnergyHeartWeapon }, null).WithOnSheet(sheet => {
                sheet.AddSelectionOptionRightNow((SelectionOption)new SingleFeatSelectionOption("EnergyHeartType", "Energy Heart Type", sheet.CurrentLevel, (Func<Feat, bool>)(ft => ft.HasTrait(tEnergyHeartDamage))));
            })
            });

            yield return new EvolutionFeat(ModManager.RegisterFeatName("Bloodletting Claws"), 4,
                "Your eidolon inflicts bleeding wounds on a telling blow.",
                "If your eidolon critically hits with a melee unarmed Strike that deals slashing or piercing damage, its target takes 1d6 persistent bleed damage. " +
                "Your eidolon gains an item bonus to this bleed damage equal to the unarmed attack's item bonus to attack rolls.", new Trait[] { tSummoner }, e => e.AddQEffect(new QEffect {
                AfterYouDealDamageOfKind = (async (self, action, damageType, target) => {
                    if (!action.Name.StartsWith("Strike (") || !action.HasTrait(Trait.Unarmed)) {
                        return;
                    }

                    int bonus = 0;

                    if (StrikeRules.GetBestHandwraps(self) != null) {
                        bonus = StrikeRules.GetBestHandwraps(self).WeaponProperties.ItemBonus;
                    }

                    if ((damageType == DamageKind.Slashing || damageType == DamageKind.Piercing) && action.CheckResult == CheckResult.CriticalSuccess) {
                        target.AddQEffect(QEffect.PersistentDamage("1d6" + (bonus > 0 ? $"+{bonus}" : ""), DamageKind.Bleed));
                    }
                })
            }), null);

            yield return new EvolutionFeat(ModManager.RegisterFeatName("RangedCombatant", "Ranged Combatant"), 2, "Spines, flame jets, and holy blasts are just some of the ways your eidolon might strike from a distance.",
                "Your eidolon gains a ranged unarmed attack with a range increment of 30 feet that deals 1d4 damage and has the magical and propulsive traits." +
                " When you select this feat, choose a damage type: acid, bludgeoning, cold, electricity, fire, negative, piercing, positive, or slashing." +
                " If your eidolon is a celestial, fiend, or monitor with an alignment other than true neutral, you can choose a damage type in its alignment.", new Trait[] { tSummoner }, null, new List<Feat> {
                new EvolutionFeat(ModManager.RegisterFeatName("Acid_RangedCombatant", "Acid"), 1, "", "Your eidolon's ranged attack deals acid damage.", new Trait[] {}, e => e.AddQEffect(new QEffect {
                    AdditionalUnarmedStrike = new Item(IllustrationName.AcidArrow, "Acid Spit", new Trait[] { Trait.Unarmed, Trait.Ranged, Trait.Magical, Trait.Propulsive }).WithWeaponProperties(new WeaponProperties("1d4", DamageKind.Acid).WithRangeIncrement(6)
                )}), null),
                new EvolutionFeat(ModManager.RegisterFeatName("Bludgeoning_RangedCombatant", "Bludgeoning"), 1, "", "Your eidolon's ranged attack deals bludgeoning damage.", new Trait[] {}, e => e.AddQEffect(new QEffect {
                    AdditionalUnarmedStrike = new Item(IllustrationName.TelekineticProjectile, "Telekinesis", new Trait[] { Trait.Unarmed, Trait.Ranged, Trait.Magical, Trait.Propulsive }).WithWeaponProperties(new WeaponProperties("1d4", DamageKind.Bludgeoning).WithRangeIncrement(6)
                )}), null),
                new EvolutionFeat(ModManager.RegisterFeatName("Cold_RangedCombatant", "Cold"), 1, "", "Your eidolon's ranged attack deals cold damage.", new Trait[] {}, e => e.AddQEffect(new QEffect {
                    AdditionalUnarmedStrike = new Item(IllustrationName.RayOfFrost, "Chill", new Trait[] { Trait.Unarmed, Trait.Ranged, Trait.Magical, Trait.Propulsive }).WithWeaponProperties(new WeaponProperties("1d4", DamageKind.Cold).WithRangeIncrement(6)
                )}), null),
                new EvolutionFeat(ModManager.RegisterFeatName("Electricity_RangedCombatant", "Electricity"), 1, "", "Your eidolon's ranged attack deals electricity damage.", new Trait[] {}, e => e.AddQEffect(new QEffect {
                    AdditionalUnarmedStrike = new Item(IllustrationName.ElectricArc, "Zap", new Trait[] { Trait.Unarmed, Trait.Ranged, Trait.Magical, Trait.Propulsive }).WithWeaponProperties(new WeaponProperties("1d4", DamageKind.Electricity).WithRangeIncrement(6)
                )}), null),
                new EvolutionFeat(ModManager.RegisterFeatName("Fire_RangedCombatant", "Fire"), 1, "", "Your eidolon's ranged attack deals fire damage.", new Trait[] {}, e => e.AddQEffect(new QEffect {
                    AdditionalUnarmedStrike = new Item(IllustrationName.ProduceFlame, "Scorch", new Trait[] { Trait.Unarmed, Trait.Ranged, Trait.Magical, Trait.Propulsive }).WithWeaponProperties(new WeaponProperties("1d4", DamageKind.Fire).WithRangeIncrement(6)
                )}), null),
                new EvolutionFeat(ModManager.RegisterFeatName("Negative_RangedCombatant", "Negative"), 1, "", "Your eidolon's ranged attack deals negative damage.", new Trait[] {}, e => e.AddQEffect(new QEffect {
                    AdditionalUnarmedStrike = new Item(IllustrationName.ElectricArc, "Wilt", new Trait[] { Trait.Unarmed, Trait.Ranged, Trait.Magical, Trait.Propulsive }).WithWeaponProperties(new WeaponProperties("1d4", DamageKind.Negative).WithRangeIncrement(6)
                )}), null),
                new EvolutionFeat(ModManager.RegisterFeatName("Piercing_RangedCombatant", "Piercing"), 1, "", "Your eidolon's ranged attack deals piercing damage.", new Trait[] {}, e => e.AddQEffect(new QEffect {
                    AdditionalUnarmedStrike = new Item(IllustrationName.ArrowProjectile, "Shoot", new Trait[] { Trait.Unarmed, Trait.Ranged, Trait.Magical, Trait.Propulsive }).WithWeaponProperties(new WeaponProperties("1d4", DamageKind.Piercing).WithRangeIncrement(6)
                )}), null),
                new EvolutionFeat(ModManager.RegisterFeatName("Positive_RangedCombatant", "Positive"), 1, "", "Your eidolon's ranged attack deals positive damage.", new Trait[] {}, e => e.AddQEffect(new QEffect {
                    AdditionalUnarmedStrike = new Item(IllustrationName.DivineLance, "Smite", new Trait[] { Trait.Unarmed, Trait.Ranged, Trait.Magical, Trait.Propulsive }).WithWeaponProperties(new WeaponProperties("1d4", DamageKind.Positive).WithRangeIncrement(6)
                )}), null),
                new EvolutionFeat(ModManager.RegisterFeatName("Slashing_RangedCombatant", "Slashing"), 1, "", "Your eidolon's ranged attack deals slashing damage.", new Trait[] {}, e => e.AddQEffect(new QEffect {
                    AdditionalUnarmedStrike = new Item(IllustrationName.FourWinds, "Razor Wind", new Trait[] { Trait.Unarmed, Trait.Ranged, Trait.Magical, Trait.Propulsive }).WithWeaponProperties(new WeaponProperties("1d4", DamageKind.Slashing).WithRangeIncrement(6)
                )}), null),
                new EvolutionFeat(ModManager.RegisterFeatName("Good_RangedCombatant", "Good"), 1, "", "Your eidolon's ranged attack deals good damage.", new Trait[] {}, e => e.AddQEffect(new QEffect {
                    AdditionalUnarmedStrike = new Item(IllustrationName.DivineLance, "Rebuke", new Trait[] { Trait.Unarmed, Trait.Ranged, Trait.Magical, Trait.Propulsive }).WithWeaponProperties(new WeaponProperties("1d4", DamageKind.Good).WithRangeIncrement(6)
                )}), null).WithPrerequisite((sheet => {
                    if (sheet.AllFeats.FirstOrDefault(ft => divineTypes.Contains(ft.FeatName.HumanizeTitleCase2())) == null)
                       return false;
                    if (sheet.AllFeats.FirstOrDefault(ft => ft.HasTrait(Trait.Good) && ft.HasTrait(tAlignment)) == null)
                       return false;
                    return true;
                }), "Your eidolon must be of good alignment, and celestial origin."),
                new EvolutionFeat(ModManager.RegisterFeatName("Evil_RangedCombatant", "Evil"), 1, "", "Your eidolon's ranged attack deals evil damage.", new Trait[] {}, e => e.AddQEffect(new QEffect {
                    AdditionalUnarmedStrike = new Item(IllustrationName.DivineLance, "Rebuke", new Trait[] { Trait.Unarmed, Trait.Ranged, Trait.Magical, Trait.Propulsive }).WithWeaponProperties(new WeaponProperties("1d4", DamageKind.Evil).WithRangeIncrement(6)
                )}), null).WithPrerequisite((sheet => {
                    if (sheet.AllFeats.FirstOrDefault(ft => divineTypes.Contains(ft.FeatName.HumanizeTitleCase2())) == null)
                       return false;
                    if (sheet.AllFeats.FirstOrDefault(ft => ft.HasTrait(Trait.Evil) && ft.HasTrait(tAlignment)) == null)
                       return false;
                    return true;
                }), "Your eidolon must be of evil alignment, and celestial origin."),
                new EvolutionFeat(ModManager.RegisterFeatName("Chaotic_RangedCombatant", "Chaotic"), 1, "", "Your eidolon's ranged attack deals chaos damage.", new Trait[] {}, e => e.AddQEffect(new QEffect {
                    AdditionalUnarmedStrike = new Item(IllustrationName.DivineLance, "Rebuke", new Trait[] { Trait.Unarmed, Trait.Ranged, Trait.Magical, Trait.Propulsive }).WithWeaponProperties(new WeaponProperties("1d4", DamageKind.Chaotic).WithRangeIncrement(6)
                )}), null).WithPrerequisite((sheet => {
                    if (sheet.AllFeats.FirstOrDefault(ft => divineTypes.Contains(ft.FeatName.HumanizeTitleCase2())) == null)
                       return false;
                    if (sheet.AllFeats.FirstOrDefault(ft => ft.HasTrait(Trait.Chaotic) && ft.HasTrait(tAlignment)) == null)
                       return false;
                    return true;
                }), "Your eidolon must be of chaotic alignment, and celestial origin."),
                new EvolutionFeat(ModManager.RegisterFeatName("Lawful_RangedCombatant", "Lawful"), 1, "", "Your eidolon's ranged attack deals law damage.", new Trait[] {}, e => e.AddQEffect(new QEffect {
                    AdditionalUnarmedStrike = new Item(IllustrationName.DivineLance, "Rebuke", new Trait[] { Trait.Unarmed, Trait.Ranged, Trait.Magical, Trait.Propulsive }).WithWeaponProperties(new WeaponProperties("1d4", DamageKind.Lawful).WithRangeIncrement(6)
                )}), null).WithPrerequisite((sheet => {
                    if (sheet.AllFeats.FirstOrDefault(ft => divineTypes.Contains(ft.FeatName.HumanizeTitleCase2())) == null)
                       return false;
                    if (sheet.AllFeats.FirstOrDefault(ft => ft.HasTrait(Trait.Lawful) && ft.HasTrait(tAlignment)) == null)
                       return false;
                    return true;
                }), "Your eidolon must be of lawful alignment, and celestial origin.")
            });

            yield return new Feat(ModManager.RegisterFeatName("Disarm"), "Your eidolon's natural weapon is especially adept at prying away their foe's weapons.", "{b}" + Trait.Disarm.HumanizeTitleCase2() + "{/b} " + Trait.Disarm.GetTraitProperties().RulesText, new List<Trait>() { tAdvancedWeaponryAtkTrait }, null);
            yield return new Feat(ModManager.RegisterFeatName("Grapple"), "Your eidolon's natural weapon is especially adept at ensaring their foes.", "{b}" + tGrapple.HumanizeTitleCase2() + "{/b} " + tGrapple.GetTraitProperties().RulesText, new List<Trait>() { tAdvancedWeaponryAtkTrait }, null);
            yield return new Feat(ModManager.RegisterFeatName("Shove"), "Your eidolon's natural weapon is especially adept at shoving away their foes.", "{b}" + Trait.Shove.HumanizeTitleCase2() + "{/b} " + Trait.Shove.GetTraitProperties().RulesText, new List<Trait>() { tAdvancedWeaponryAtkTrait }, null);
            yield return new Feat(ModManager.RegisterFeatName("Trip"), "Your eidolon's natural weapon is especially adept at topplin their enemies.", "{b}" + Trait.Trip.HumanizeTitleCase2() + "{/b} " + Trait.Trip.GetTraitProperties().RulesText, new List<Trait>() { tAdvancedWeaponryAtkTrait }, null);
            yield return new Feat(ModManager.RegisterFeatName("Versatile Piercing"), "Your eidolon's natural weapon has a deadly piercing appendage.", "{b}" + Trait.VersatileP.HumanizeTitleCase2() + "{/b} " + Trait.VersatileP.GetTraitProperties().RulesText, new List<Trait>() { tAdvancedWeaponryAtkTrait }, null);
            yield return new Feat(ModManager.RegisterFeatName("Versatile Slashing"), "Your eidolon's natural weapon has a sharp slashing edge.", "{b}" + Trait.VersatileS.HumanizeTitleCase2() + "{/b} " + Trait.VersatileS.GetTraitProperties().RulesText, new List<Trait>() { tAdvancedWeaponryAtkTrait }, null);
            yield return new Feat(ModManager.RegisterFeatName("Versatile Bludgeoning"), "Your eidolon's natural weapon has a heavy, crushing weight.", "{b}" + Trait.VersatileB.HumanizeTitleCase2() + "{/b} " + Trait.VersatileB.GetTraitProperties().RulesText, new List<Trait>() { tAdvancedWeaponryAtkTrait }, null);

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
            yield return new Feat(ftPSPowerful, "Your eidolon possesses great strength, allowing it to easily bully and subdue its enemies.", "Your eidolon's primary deals 1d8 damage and has the disarm, nonlethal, shove and trip traits.\n\nAthletics checks made using a weapon with a maneouvre trait benefit your eidolon's item bonus." +
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
            yield return new Feat(ftALawfulGood, "Your eidolon's alignment is lawful good.", "", new List<Trait> { tAlignment, Trait.Lawful, Trait.Good }, null);
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

        public static DamageKind TraitToDamage(Trait trait) {
            switch (trait) {
                case Trait.Acid:
                    return DamageKind.Acid;
                case Trait.Cold:
                    return DamageKind.Cold;
                case Trait.Electricity:
                    return DamageKind.Electricity;
                case Trait.Fire:
                    return DamageKind.Fire;
                case Trait.Sonic:
                    return DamageKind.Sonic;
                case Trait.Positive:
                    return DamageKind.Positive;
                case Trait.Negative:
                    return DamageKind.Negative;
                case Trait.Good:
                    return DamageKind.Good;
                case Trait.Evil:
                    return DamageKind.Evil;
                case Trait.Chaotic:
                    return DamageKind.Chaotic;
                case Trait.Lawful:
                    return DamageKind.Lawful;
                default:
                    return DamageKind.Untyped;
            }
        }

        public static Trait DamageToTrait(DamageKind type) {
            switch (type) {
                case DamageKind.Acid:
                    return Trait.Acid;
                case DamageKind.Cold:
                    return Trait.Cold;
                case DamageKind.Electricity:
                    return Trait.Electricity;
                case DamageKind.Fire:
                    return Trait.Fire;
                case DamageKind.Sonic:
                    return Trait.Sonic;
                case DamageKind.Positive:
                    return Trait.Positive;
                case DamageKind.Negative:
                    return Trait.Negative;
                case DamageKind.Good:
                    return Trait.Good;
                case DamageKind.Evil:
                    return Trait.Evil;
                case DamageKind.Chaotic:
                    return Trait.Chaotic;
                case DamageKind.Lawful:
                    return Trait.Lawful;
                default:
                    return Trait.None;
            }
        }

        private static Spell TraditionToSpell(Trait tradition, int spellLevel) {
            if (spellLevel == 1) {
                switch (tradition) {
                    case Trait.Arcane:
                        return AllSpells.CreateModernSpellTemplate(SpellId.MageArmor, tSummoner, spellLevel);
                        break;
                    case Trait.Divine:
                        return AllSpells.CreateModernSpellTemplate(SpellId.Bless, tSummoner, spellLevel);
                        break;
                    case Trait.Primal:
                        return AllSpells.CreateModernSpellTemplate(SpellId.Grease, tSummoner, spellLevel);
                        break;
                    case Trait.Occult:
                        return AllSpells.CreateModernSpellTemplate(SpellId.Fear, tSummoner, spellLevel);
                        break;
                    default:
                        throw new Exception("Summoner Class Mod: Invalid spell casting tradition");
                        return null;
                }
            }
            switch (tradition) {
                case Trait.Arcane:
                    return AllSpells.CreateModernSpellTemplate(SpellId.Blur, tSummoner, spellLevel);
                    break;
                case Trait.Divine:
                    return AllSpells.CreateModernSpellTemplate(SpellId.BloodVendetta, tSummoner, spellLevel);
                    break;
                case Trait.Primal:
                    return AllSpells.CreateModernSpellTemplate(SpellId.Barkskin, tSummoner, spellLevel);
                    break;
                case Trait.Occult:
                    return AllSpells.CreateModernSpellTemplate(SpellId.HideousLaughter, tSummoner, spellLevel);
                    break;
                default:
                    throw new Exception("Summoner Class Mod: Invalid spell casting tradition");
                    return null;
            }
        }

        private static string Int2Mod(int num) {
            if (num >= 0) {
                return $"+{num}";
            }
            return $"-{num - num * 2}";
        }

        public class EvolutionFeat : TrueFeat {
            public Action<Creature>? EffectOnEidolon { get; private set; }
            public EvolutionFeat(FeatName featName, int level, string flavourText, string rulesText, Trait[] traits, Action<Creature> effect, List<Feat>? subfeats) : base(featName, level, flavourText, rulesText, new Trait[] { tEvolution }.Concat(traits).ToArray(), subfeats) {
                EffectOnEidolon = effect;
            }

        }

        public class EidolonBond : Feat {
            public EidolonBond(FeatName featName, string flavourText, string rulesText, Trait spellList, List<FeatName> skills, Func<Feat, bool> alignmentOptions, List<Trait> traits, List<Feat> subfeats) : base(featName, flavourText, rulesText, traits, subfeats) {
                Init(spellList, skills, alignmentOptions);
            }
            
            public EidolonBond(FeatName featName, string flavourText, string rulesText, Trait spellList, List<FeatName> skills, Func<Feat, bool> alignmentOptions) : base(featName, flavourText, rulesText, new List<Trait>() { }, null) {
                Init(spellList, skills, alignmentOptions);
            }

            private void Init(Trait spellList, List<FeatName> skills, Func<Feat, bool> alignmentOptions) {
                this.OnSheet = (Action<CalculatedCharacterSheetValues>)(sheet => {
                    sheet.AddSelectionOption((SelectionOption)new SingleFeatSelectionOption("EidolonAlignment", "Eidolon Alignment", 1, alignmentOptions));
                    sheet.AddSelectionOption(new SingleFeatSelectionOption("EidolonPrimaryWeaponStats", "Eidolon Primary Weapon Stats", 1, (Func<Feat, bool>)(ft => ft.HasTrait(tPrimaryAttackStats))));
                    sheet.AddSelectionOption(new SingleFeatSelectionOption("EidolonPrimaryWeapon", "Eidolon Primary Natural Weapon", 1, (Func<Feat, bool>)(ft => ft.HasTrait(tPrimaryAttackType))));
                    sheet.AddSelectionOption(new SingleFeatSelectionOption("EidolonSecondaryWeapon", "Eidolon Secondary Natural Weapon", 1, (Func<Feat, bool>)(ft => ft.HasTrait(tSecondaryAttackType))));
                    sheet.SpellTraditionsKnown.Add(spellList);
                    sheet.SpellRepertoires.Add(tSummoner, new SpellRepertoire(Ability.Charisma, spellList));
                    sheet.SetProficiency(Trait.Spell, Proficiency.Trained);
                    foreach (FeatName skill in skills) {
                        sheet.GrantFeat(skill);
                    }
                    if (this.FeatName == scFeyEidolon)
                        sheet.AddFeat(AllFeats.All.FirstOrDefault(ft => ft.FeatName == ftMagicalUnderstudy), null);
                    SpellRepertoire repertoire = sheet.SpellRepertoires[tSummoner];
                    if (this.FeatName != scFeyEidolon) {
                        sheet.AddSelectionOption((SelectionOption)new AddToSpellRepertoireOption("SummonerCantrips", "Cantrips", 1, tSummoner, spellList, 0, 5));
                        sheet.AddSelectionOption((SelectionOption)new AddToSpellRepertoireOption("SummonerSpells1", "Level 1 spells", 1, tSummoner, spellList, 1, 2));
                        sheet.AddSelectionOption((SelectionOption)new AddToSpellRepertoireOption("SummonerSpells2", "Level 1 spell", 2, tSummoner, spellList, 1, 1));
                        sheet.AddSelectionOption((SelectionOption)new AddToSpellRepertoireOption("SummonerSpells3", "Level 2 spells", 3, tSummoner, spellList, 2, 1));
                        sheet.AddSelectionOption((SelectionOption)new AddToSpellRepertoireOption("SummonerSpells4", "Level 2 spell", 4, tSummoner, spellList, 2, 1));
                    } else {
                        sheet.AddSelectionOption((SelectionOption)new SelectFeySpells("SummonerCantrips", "Cantrips", 1, tSummoner, 0, 5, true));
                        sheet.AddSelectionOption((SelectionOption)new SelectFeySpells("SummonerSpells1", "Level 1 spells", 1, tSummoner, 1, 2));
                        sheet.AddSelectionOption((SelectionOption)new SelectFeySpells("SummonerSpells2", "Level 1 spell", 2, tSummoner, 1, 1));
                        sheet.AddSelectionOption((SelectionOption)new SelectFeySpells("SummonerSpells3", "Level 2 spells", 3, tSummoner, 2, 1));
                        sheet.AddSelectionOption((SelectionOption)new SelectFeySpells("SummonerSpells4", "Level 2 spell", 4, tSummoner, 2, 1));
                    }

                    repertoire.SpellSlots[1] = 1;
                    sheet.AddAtLevel(2, (Action<CalculatedCharacterSheetValues>)(_ => ++repertoire.SpellSlots[1]));
                    sheet.AddAtLevel(3, (Action<CalculatedCharacterSheetValues>)(_ => ++repertoire.SpellSlots[2]));
                    sheet.AddAtLevel(4, (Action<CalculatedCharacterSheetValues>)(_ => ++repertoire.SpellSlots[2]));

                    if (this.FeatName == scFeyEidolon)
                        sheet.AddAtLevel(7, sheet => sheet.AddFeat(AllFeats.All.FirstOrDefault(ft => ft.FeatName == ftMagicalAdept), null));

                    for (int index = 5; index <= 17; index += 2) {
                        int thisLevel = index;
                        sheet.AddAtLevel(thisLevel, (Action<CalculatedCharacterSheetValues>)(values => {
                            int num = (thisLevel + 1) / 2;
                            int removedLevel = num - 2;
                            values.SpellRepertoires[tSummoner].SpellSlots[removedLevel]--;
                            values.SpellRepertoires[tSummoner].SpellSlots[removedLevel]--;
                            values.SpellRepertoires[tSummoner].SpellSlots[num]++;
                            values.SpellRepertoires[tSummoner].SpellSlots[num]++;

                            repertoire.SpellsKnown.RemoveAll(spell => spell.HasTrait(Trait.Focus) == false && spell.HasTrait(Trait.Cantrip) == false);

                            int tradition = (int)spellList;
                            int maximumSpellLevel = num;
                            AddToSpellRepertoireOption repertoireOption1;
                            AddToSpellRepertoireOption repertoireOption2;
                            if (this.FeatName != scFeyEidolon) {
                                repertoireOption1 = new AddToSpellRepertoireOption($"SummonerSpells{sheet.CurrentLevel}-1", $"Level {num - 1} spells", thisLevel, tSummoner, spellList, maximumSpellLevel - 1, 3);
                                repertoireOption2 = new AddToSpellRepertoireOption($"SummonerSpells{sheet.CurrentLevel}-2", $"Level {num} spells", thisLevel, tSummoner, spellList, maximumSpellLevel, 2);
                            } else {
                                repertoireOption1 = new SelectFeySpells($"SummonerSpells{sheet.CurrentLevel}-1", $"Level {num - 1} spells", thisLevel, tSummoner, maximumSpellLevel - 1, 3);
                                repertoireOption2 = new SelectFeySpells($"SummonerSpells{sheet.CurrentLevel}-2", $"Level {num} spells", thisLevel, tSummoner, maximumSpellLevel, 2);
                            }
                            values.AddSelectionOption((SelectionOption)repertoireOption1);
                            values.AddSelectionOption((SelectionOption)repertoireOption2);

                        }));
                    }
                    repertoire.SpellsKnown.Add(AllSpells.CreateModernSpellTemplate(spells[SummonerSpellId.EidolonBoost], tSummoner, sheet.MaximumSpellLevel));
                });
                this.OnCreature = ((sheet, creature) => {
                    // Signature-afy spells
                    SpellRepertoire repertoire = sheet.SpellRepertoires[tSummoner];
                    List<Spell> spells = repertoire.SpellsKnown.Where(spell => spell.HasTrait(Trait.Cantrip) == false).ToList();

                    if (spells.Count() > 5) {
                        return;
                    }

                    for (int i = 0; i < spells.Count(); i++) {
                        for (int spellLvl = spells[i].MinimumSpellLevel; spellLvl < 10; spellLvl++) {
                            if (spells.FirstOrDefault(s => s.SpellId == spells[i].SpellId && s.SpellLevel == spellLvl) == null) {
                                repertoire.SpellsKnown.Add(AllSpells.CreateModernSpellTemplate(spells[i].SpellId, tSummoner, spellLvl));
                            }
                        }
                    }
                    if (sheet.HasFeat(ftAbundantSpellcasting1)) {
                        Spell spell = TraditionToSpell(sheet.SpellRepertoires[tSummoner].SpellList, 1);
                        if (repertoire.SpellsKnown.FirstOrDefault(s => s.SpellId == spell.SpellId && s.SpellLevel == spell.SpellLevel) == null) {
                            repertoire.SpellsKnown.Add(spell);
                        }
                    }
                    if (sheet.HasFeat(ftAbundantSpellcasting4)) {
                        Spell spell = TraditionToSpell(sheet.SpellRepertoires[tSummoner].SpellList, 2);
                        var test = repertoire.SpellsKnown;
                        if (repertoire.SpellsKnown.FirstOrDefault(s => s.SpellId == spell.SpellId && s.SpellLevel == spell.SpellLevel) == null) {
                            repertoire.SpellsKnown.Add(spell);
                        }
                    }
                });
            }
        }

        private static string PrintEidolonStatBlock(FeatName bond, int[] abilityScores, int ac, int dexCap) {
            string general =
                "{b}Perception{/b} " + Int2Mod((abilityScores[4] + 3)) +
                "\n{b}Skills{/b} Shares all your skill proficiancies\n" +
                "\nStr " + Int2Mod(abilityScores[0]) + " Dex " + Int2Mod(abilityScores[1]) + " Con " + Int2Mod(abilityScores[2]) + " Int " + Int2Mod(abilityScores[3]) + " Wis " + Int2Mod(abilityScores[4]) + " Cha " + Int2Mod(abilityScores[5]) + "\n" +
                "\n{b}{DarkRed}DEFENSE{/b}{/}\n" +
                "{b}AC{/b} " + (10 + ac + Math.Min(abilityScores[1], dexCap)) + "; {b}Fort{/b} " + Int2Mod((5 + abilityScores[2])) + ", {b}Ref{/b} " + Int2Mod((3 + abilityScores[1])) + ", {b}Will{/b} " + Int2Mod((4 + abilityScores[4])) +
                "\n{b}HP{/b} Share's your HP pool";

            if (bond == scDevilEidolonBarrister || bond == scDevilEidolonLegionnaire) {
                general += "\n{b}Resistances{/b} fire 1; {b}Weaknesses{/b} good 1";
            }

            general +=
                "\n\n{b}{DarkRed}OFFENSE{/b}{/}\n" +
                "{b}Speed{/b} 25 feet\n";

            string actions =
                "{b}Strke (Primary){/b} {icon:Action} " + Int2Mod((abilityScores[0] + 3)) + " [variable] 1dx" + (abilityScores[0] >= 0 ? " +" : " ") + abilityScores[0] + " variable damage\n" +
                "{b}Strke (Secondary){/b} {icon:Action} " + Int2Mod((Math.Max(abilityScores[0], abilityScores[1]) + 3)) + " [agile] " + Int2Mod(abilityScores[0]) + " variable damage\n";

            if (bond == scDraconicEidolonCunning || bond == scDraconicEidolonMarauding) {
                actions += "{b}Breath Weapon{/b} {{icon:TwoActions} Your eidolon exhales a line or cone of energy and deal 2d6 of the damage associated with your eidolon's dragon type to each target. You can't use breath weapon again for 1d4 rounds.\n";
            }

            if (bond == scBeastEidolonBrutal || bond == scBeastEidolonFleet) {
                actions += "{b}Beast's Charge{/b} {{icon:TwoActions} Stride twice. If you end your movement within melee reach of at least one enemy, you can make a melee Strike against that enemy. If your eidolon moved at least 20ft and ends it's movement in a cardinal diraction, it gains a +1 circumstance bonus to this attack roll.\n";
            }

            if (bond == scAzataEidolonCrusader || bond == scAzataEidolonPoet) {
                actions += "{b}Celestial Passion{/b} {{icon:Action} One ally within 15-feet of your eidolon gains temporary HP equal to its level, and a +1 bonus to attack and skill checks for 1 round. Cannot be used on the same ally more than once per encounter.\n";
            }

            if (bond == scAngerPhantomAssassin || bond == scAngerPhantomBerserker) {
                actions += "{b}Frenzied Assault{/b} {{icon:TwoActions} Your eidolon makes two strikes against a single target, one with each of its unarmed attacks, at its current MAP penalty. " +
                "The damage from both attacks are combined for the purposes of damage resistance.\n";
            }

            actions += "{b}Act Together{/b} {icon:FreeAction} Your eidolon's next action grants you an immediate bonus tandem turn, where you can make a single action.\n";

            string abilities =
                "\n{b}{DarkRed}ABILITIES{/b}{/}\n" +
                "{b}Eidolon Bond.{/b} You and your eidolon share your actions and multiple attack penalty. Each round, you can use any of your actions (including reactions and free actions) for yourself or your eidolon. Your eidolon gains all of your skill proficiancies and uses your spell attack and save DC for its special abilities.";

            if (bond == scAngelicEidolonAvenger || bond == scAngelicEidolonEmmissary) {
                abilities += "\n{b}Hallowed Strikes.{/b} Your eidolon's unarmed strikes deal +1 extra good damage.\n";
            }

            if (bond == scDevoPhantomEidolonStalwart || bond == scDevoPhantomEidolonSwift) {
                abilities += "\n{b}Dutiful Retaliation {icon:Reaction}.{/b} Your eidolon makes a strike again an enemy that damaged you. Both your eidolon and your attacker must be within 15ft of you.\n";
            }

            if (bond == scDevilEidolonLegionnaire || bond == scDevilEidolonBarrister) {
                abilities += "\n{b}Hellfire Scourge{/b} Your eidolon deals +1d4 fire damage to the first frightened creature it strikes each round.\n";
            }

            return general + actions + abilities;
        }


        private static Feat CreateEidolonFeat(FeatName featName, string flavorText, int[] abilityScores, int ac, int dexCap) {
            return new Feat(featName, flavorText, "Your eidolon has the following characteristics at level 1:\n\n" + PrintEidolonStatBlock(featName, abilityScores, ac, dexCap), new List<Trait>() { }, (List<Feat>)null)
            .WithOnCreature((Action<CalculatedCharacterSheetValues, Creature>)((sheet, summoner) => summoner
            .AddQEffect(new ActionShareEffect() {
                Id = qfSharedActions,
            })
            // TODO: Bookmark: Summoner act together
            .AddQEffect(new QEffect() {
                ProvideMainAction = (effect) => {
                    if (summoner.PersistentCharacterSheet.Calculated.AllFeats.Where(ft => ft.HasTrait(tTandem)).ToList().Count > 0) {
                        SubmenuPossibility tandemActions = new SubmenuPossibility(illActTogether, "Tandem Actions");
                        tandemActions.Subsections.Add(new PossibilitySection("Tandem Actions"));
                        tandemActions.Subsections[0].PossibilitySectionId = psTandemActions;
                        return tandemActions;
                    }

                    return GenerateActTogetherAction(effect.Owner, GetEidolon(effect.Owner), summoner);

                },
                ProvideActionIntoPossibilitySection = (effect, section) => {
                    if (summoner.PersistentCharacterSheet.Calculated.AllFeats.Where(ft => ft.HasTrait(tTandem)).ToList().Count == 0) {
                        return null;
                    } else if (section.PossibilitySectionId == psTandemActions) {
                        return GenerateActTogetherAction(effect.Owner, GetEidolon(effect.Owner), summoner);
                    }
                    return null;
                },
            })
            .AddQEffect(new QEffect("Eidolon", "This character can summon and command an Eidolon.") {
                StartOfCombat = (Func<QEffect, Task>)(async qfSummonerTechnical => {
                    Creature eidolon = CreateEidolon(featName, abilityScores, ac, dexCap, summoner);
                    eidolon.MainName = qfSummonerTechnical.Owner.Name + "'s " + eidolon.MainName;

                    Item? armour = summoner.BaseArmor;
                    if (armour != null) {
                        List<Item> runes = armour.Runes;

                        foreach (Item rune in runes) {
                            switch (rune.RuneProperties.RuneKind) {
                                case RuneKind.ArmorResilient:
                                    eidolon.Defenses.Set(Defense.Fortitude, eidolon.Defenses.GetSavingThrow(Defense.Fortitude).Bonus + 1);
                                    eidolon.Defenses.Set(Defense.Reflex, eidolon.Defenses.GetSavingThrow(Defense.Reflex).Bonus + 1);
                                    eidolon.Defenses.Set(Defense.Will, eidolon.Defenses.GetSavingThrow(Defense.Will).Bonus + 1);
                                    break;
                                case RuneKind.ArmorPotency:
                                    eidolon.Defenses.Set(Defense.AC, eidolon.Defenses.GetSavingThrow(Defense.AC).Bonus + 1);
                                    break;
                            }
                        }
                    }

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

                    // TODO: Bookmark for invested weapon code
                    // Share benfits of handwraps
                    Item handwraps = StrikeRules.GetBestHandwraps(summoner);
                    List<Item> weapons = summoner.HeldItems;
                    if (handwraps != null) {
                        Item eidolonHandwraps = handwraps.Duplicate();
                        eidolon.CarriedItems.Add(eidolonHandwraps);
                        eidolonHandwraps.IsWorn = true;
                        summoner.AddQEffect(new QEffect($"Invested Weapon ({eidolonHandwraps.Name})", "Your eidolon also benefits from these handwraps of mighty blows.") {
                            Tag = handwraps,
                            Id = qfInvestedWeapon,
                            Illustration = handwraps.Illustration
                        });
                    } else if (weapons.Count() > 0 && weapons[0].Runes.Count > 0) {
                        Item eidolonHandwraps = new Item(ItemName.HandwrapsOfMightyBlows, null, weapons[0].Name, 2, 0, new Trait[] { Trait.Invested, Trait.Magical, Trait.Transmutation }) {
                            WeaponProperties = new WeaponProperties("1d6", DamageKind.Bludgeoning)
                        }.WithWornAt(Trait.Gloves);
                        eidolonHandwraps.Name = $"{weapons[0]}";
                        foreach (Item rune in weapons[0].Runes) {
                            eidolonHandwraps.Runes.Add(rune);
                            rune.RuneProperties.ModifyItem(eidolonHandwraps);
                        }
                        summoner.AddQEffect(new QEffect($"Invested Weapon ({weapons[0].Name})", "While wielding this weapon, your eidolon benefits from its runestones.") {
                            Tag = weapons[0],
                            Id = qfInvestedWeapon,
                            Illustration = weapons[0].Illustration
                        });
                        eidolon.CarriedItems.Add(eidolonHandwraps);
                        eidolonHandwraps.IsWorn = true;
                    } else if (weapons.Count() == 2 && weapons[1].Runes.Count > 0) {
                        Item eidolonHandwraps = new Item(ItemName.HandwrapsOfMightyBlows, null, weapons[1].Name, 2, 0, new Trait[] { Trait.Invested, Trait.Magical, Trait.Transmutation }) {
                            WeaponProperties = new WeaponProperties("1d6", DamageKind.Bludgeoning)
                        }.WithWornAt(Trait.Gloves);
                        foreach (Item rune in weapons[1].Runes) {
                            eidolonHandwraps.Runes.Add(rune);
                            rune.RuneProperties.ModifyItem(eidolonHandwraps);
                        }
                        summoner.AddQEffect(new QEffect($"Invested Weapon ({weapons[1].Name})", "While wielding this weapon, your eidolon benefits from its runestones.") {
                            Tag = weapons[1],
                            Id = qfInvestedWeapon,
                            Illustration = weapons[1].Illustration
                        });
                        eidolon.CarriedItems.Add(eidolonHandwraps);
                        eidolonHandwraps.IsWorn = true;
                    }

                    summoner.Battle.SpawnCreature(eidolon, summoner.OwningFaction, summoner.Occupies);

                    /*

                    // TODO: Remove dominate debug code
                    Creature caster = summoner.Battle.Pseudocreature;
                    foreach (Creature c in summoner.Battle.AllCreatures) {
                        if (c.OwningFaction != summoner.OwningFaction) {
                            caster = c;
                            break;
                        }
                    }

                    Creature target = summoner.Battle.Pseudocreature;
                    //foreach (Creature c in summoner.Battle.AllCreatures) {
                    //    if (c.HasFeat(FeatName.Ranger)) {
                    //        target = c;
                    //        break;
                    //    }
                    //}

                    target = summoner;

                    Faction originalFaction = target.OwningFaction;
                    target.OwningFaction = caster.OwningFaction;
                    target.AddQEffect(new QEffect("Controlled", "You're controlled by " + caster?.ToString() + ".") {
                        Illustration = illDevoStance,
                        Source = caster,
                        AfterYouTakeHostileAction = (Action<QEffect, CombatAction>)((qf, action) => qf.UsedThisTurn = true),
                        StateCheck = qf => {
                            if (caster.Alive)
                                return;
                            qf.Owner.Occupies.Overhead("end of control", Color.Lime, caster?.ToString() + " died and so can no longer dominate " + target?.ToString() + ".");
                            if (qf.Owner.OwningFaction != caster.OwningFaction)
                                return;
                            qf.Owner.OwningFaction = originalFaction;
                            qf.ExpiresAt = ExpirationCondition.Immediately;
                        },
                        EndOfYourTurn = async (effect, dominatedCreature) => {
                            effect.Owner.OwningFaction = originalFaction;
                            effect.ExpiresAt = ExpirationCondition.Immediately;
                        },
                    });
                    */
                }),
                StartOfYourTurn = (Func<QEffect, Creature, Task>)(async (qfStartOfTurn, summoner) => {
                    Creature eidolon = GetEidolon(summoner);

                    await (Task)eidolon.Battle.GameLoop.GetType().GetMethod("StartOfTurn", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static).Invoke(eidolon.Battle.GameLoop, new object[] { eidolon });

                    await eidolon.Battle.GameLoop.StateCheck();
                }),
                StateCheckWithVisibleChanges = (async qf => {
                    Creature eidolon = GetEidolon(qf.Owner);
                    if (eidolon == null) {
                        return;
                    }

                    // Handle drained mirror addition
                    QEffect drained = qf.Owner.FindQEffect(QEffectId.Drained);
                    if (drained != null) {
                        eidolon.AddQEffect(new QEffect() {
                            Id = qfDrainedMirror,
                            Key = "DrainedMirror",
                            StateCheck = drained.StateCheck,
                            Value = drained.Value,
                        });
                    } else if (qf.Owner.HasEffect(qfDrainedMirror) && eidolon.HasEffect(QEffectId.Drained) == false) {
                        qf.Owner.RemoveAllQEffects(effect => effect.Id == qfDrainedMirror);
                    }

                    // PAST THIS POINT, INACTIVE EIDOLON NOT AFFECTED
                    if (eidolon.Destroyed == true) {
                        return;
                    }

                    // Reaction
                    if (qf.Owner.Actions.IsReactionUsedUp == true) {
                        eidolon.Actions.UseUpReaction();
                    }

                    // Handle AoO
                    if (qf.Owner.HasEffect(qfReactiveStrikeCheck)) {
                        qf.Owner.RemoveAllQEffects(qf => qf.Id == qfReactiveStrikeCheck);
                        HPShareEffect shareHP = (HPShareEffect)qf.Owner.QEffects.FirstOrDefault<QEffect>((Func<QEffect, bool>)(qf => qf.Id == qfSummonerBond));
                        await HandleHealthShare(qf.Owner, eidolon, shareHP.LoggedAction, SummonerClassEnums.InterceptKind.TARGET);
                    }
                    if (eidolon.HasEffect(qfReactiveStrikeCheck)) {
                        summoner.RemoveAllQEffects(qf => qf.Id == qfReactiveStrikeCheck);
                        HPShareEffect shareHP = (HPShareEffect)eidolon.QEffects.FirstOrDefault<QEffect>((Func<QEffect, bool>)(qf => qf.Id == qfSummonerBond));
                        await HandleHealthShare(eidolon, qf.Owner, shareHP.LoggedAction, SummonerClassEnums.InterceptKind.TARGET);
                    }

                    // Handle tempHP
                    if (qf.Owner.TemporaryHP < eidolon.TemporaryHP) {
                        qf.Owner.GainTemporaryHP(eidolon.TemporaryHP);
                    } else if (qf.Owner.TemporaryHP > eidolon.TemporaryHP) {
                        eidolon.GainTemporaryHP(qf.Owner.TemporaryHP);
                    }

                    // Handle healing
                    if (qf.Owner.HP < eidolon.HP) {
                        qf.Owner.Heal($"{eidolon.HP - qf.Owner.HP}", null);
                    } else if (qf.Owner.HP > eidolon.HP) {
                        eidolon.Heal($"{qf.Owner.HP - eidolon.HP}", null);
                    }
                }),
                EndOfYourTurn = (Func<QEffect, Creature, Task>)(async (qfEndOfTurn, summoner) => {
                    Creature eidolon = GetEidolon(summoner);
                    eidolon.Actions.ForgetAllTurnCounters();
                    summoner.Battle.ActiveCreature = eidolon;
                    List<QEffect> sustainEffects = new List<QEffect>();
                    foreach (Creature creature in qfEndOfTurn.Owner.Battle.AllCreatures) {
                        sustainEffects = sustainEffects.Concat(creature.QEffects.Where(qf => qf.CannotExpireThisTurn == true)).ToList();
                    }
                    await eidolon.Battle.GameLoop.EndOfTurn(eidolon);
                    foreach (QEffect effect in sustainEffects) {
                        effect.CannotExpireThisTurn = true;
                    }
                    summoner.Battle.ActiveCreature = summoner;
                }),
                ProvideMainAction = (Func<QEffect, Possibility>)(qfSummoner => {
                    Creature? eidolon = GetEidolon(qfSummoner.Owner);
                    if (eidolon == null || eidolon.OwningFaction != qfSummoner.Owner.OwningFaction || !eidolon.Actions.CanTakeActions() || qfSummoner.Owner.QEffects.FirstOrDefault(qf => qf.Id == qfActTogether) != null)
                        return (Possibility)null;

                    Possibility output = (Possibility)(ActionPossibility)new CombatAction(qfSummoner.Owner, eidolon.Illustration, "Command your Eidolon", new Trait[] { Trait.Basic, tSummoner }, "Swap to Eidolon.", (Target)Target.Self()) {
                        ShortDescription = "Take control of your Eidolon, using your shared action pool."
                    }
                    .WithEffectOnSelf((Func<Creature, Task>)(async self => {
                        await PartnerActs(summoner, eidolon);
                    }))
                    .WithActionCost(0);

                    //output.WithPossibilityGroup("Summoner");
                    return output;
                }),
                YouAreTargeted = (Func<QEffect, CombatAction, Task>)(async (qfHealOrHarm, action) => {
                    if (action.Name == "Command your Eidolon") {
                        return;
                    }

                    if (GetEidolon(qfHealOrHarm.Owner) == null || GetEidolon(qfHealOrHarm.Owner).Destroyed) {
                        return;
                    }

                    HPShareEffect shareHP = (HPShareEffect)qfHealOrHarm.Owner.QEffects.FirstOrDefault<QEffect>((Func<QEffect, bool>)(qf => qf.Id == qfSummonerBond && qf.Source == GetEidolon(qfHealOrHarm.Owner)));
                    HPShareEffect eidolonShareHP = (HPShareEffect)GetEidolon(qfHealOrHarm.Owner).QEffects.FirstOrDefault<QEffect>((Func<QEffect, bool>)(qf => qf.Id == qfSummonerBond && qf.Source == qfHealOrHarm.Owner));

                    if (action == shareHP.CA || action == eidolonShareHP.CA) {
                        return;
                    }

                    shareHP.LogAction(qfHealOrHarm.Owner, action, action.Owner, SummonerClassEnums.InterceptKind.TARGET);
                    shareHP.Owner.AddQEffect(new QEffect() { Id = qfReactiveStrikeCheck });
                }),
                AfterYouAreTargeted = (Func<QEffect, CombatAction, Task>)(async (qfShareHP, action) => {
                    if (action.Name == "Command your Eidolon") {
                        return;
                    }

                    if (GetEidolon(qfShareHP.Owner) == null || GetEidolon(qfShareHP.Owner).Destroyed) {
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
                    if (GetEidolon(qfPreHazardDamage.Owner) == null || GetEidolon(qfPreHazardDamage.Owner).Destroyed) {
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

                    shareHP.LogAction(qfPreHazardDamage.Owner, damageStuff.Power, attacker, SummonerClassEnums.InterceptKind.DAMAGE);
                    return null;
                }),
                AfterYouTakeDamageOfKind = (async (qfPostHazardDamage, action, kind) => {
                    if (GetEidolon(qfPostHazardDamage.Owner) == null || GetEidolon(qfPostHazardDamage.Owner).Destroyed) {
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

                    if (eidolon == null) {
                        return;
                    }

                    // Focus points
                    if (action.HasTrait(Trait.Focus)) {
                        eidolon.Spellcasting.FocusPoints = summoner.Spellcasting.FocusPoints;
                    }

                    // MAP
                    if (action.Traits.Contains(Trait.Attack)) {
                        eidolon.Actions.AttackedThisManyTimesThisTurn = summoner.Actions.AttackedThisManyTimesThisTurn;
                    }
                })
            })
            .AddQEffect(new QEffect() {
                ProvideActionIntoPossibilitySection = (qf, section) => {
                    if (section.PossibilitySectionId != PossibilitySectionId.OtherManeuvers) {
                        return null;
                    }

                    // Determine options
                    List<Item> itemOptions = new List<Item>();
                    Item? handwraps = StrikeRules.GetBestHandwraps(qf.Owner);
                    if (handwraps != null && handwraps.Runes.Count > 0) {
                        itemOptions.Add(handwraps);
                    }
                    List<Item> weapons = qf.Owner.HeldItems;
                    if (weapons.Count >= 1 && weapons[0].WeaponProperties != null && weapons[0].Runes.Count > 0) {
                        itemOptions.Add(weapons[0]);
                    }
                    if (weapons.Count == 2 && weapons[1].WeaponProperties != null && weapons[1].Runes.Count > 0) {
                        itemOptions.Add(weapons[1]);
                    }
                    if (qf.Owner.FindQEffect(qfInvestedWeapon) != null) {
                        itemOptions.Remove((Item)qf.Owner.FindQEffect(qfInvestedWeapon).Tag);
                    }

                    SubmenuPossibility menu = new SubmenuPossibility(illInvest, "Invest Weapon");
                    menu.Subsections.Add(new PossibilitySection("Invest Weapon"));

                    foreach (Item item in itemOptions) {
                        menu.Subsections[0].AddPossibility((ActionPossibility)new CombatAction(summoner, item.Illustration, $"Invest {item.Name}", new Trait[] { Trait.Manipulate },
                            $"Invest {item.Name}, so your eidolon can benefit from it. This will cause your previously invested weapon to become uninvested.", Target.Self())
                        .WithSoundEffect(SfxName.MagicWeapon)
                        .WithActionCost(1)
                        .WithEffectOnSelf(async self => {
                            Creature eidolon = GetEidolon(self);
                            eidolon.CarriedItems.Clear();

                            QEffect? oldInvestedEffect = qf.Owner.FindQEffect(qfInvestedWeapon);
                            if (oldInvestedEffect != null) {
                                oldInvestedEffect.ExpiresAt = ExpirationCondition.Immediately;
                            }

                            Item eidolonHandwraps = new Item(ItemName.HandwrapsOfMightyBlows, null, item.Name, 2, 0, new Trait[] { Trait.Invested, Trait.Magical, Trait.Transmutation }) {
                                WeaponProperties = new WeaponProperties("1d6", DamageKind.Bludgeoning)
                            }.WithWornAt(Trait.Gloves);
                            foreach (Item rune in item.Runes) {
                                eidolonHandwraps.Runes.Add(rune);
                                rune.RuneProperties.ModifyItem(eidolonHandwraps);
                            }
                            summoner.AddQEffect(new QEffect($"Invested Weapon ({item.Name})",
                                item.ItemName == ItemName.HandwrapsOfMightyBlows ? "Your eidolon also benefits from these handwraps of mighty blows." : "While wielding this weapon, your eidolon benefits from its runestones.") {
                                Tag = item,
                                Id = qfInvestedWeapon,
                                Illustration = item.Illustration
                            });
                            eidolon.CarriedItems.Add(eidolonHandwraps);
                            eidolonHandwraps.IsWorn = true;
                        })
                        );
                    }

                    foreach (ActionPossibility possibility in menu.Subsections[0].Possibilities) {
                        possibility.PossibilitySize = PossibilitySize.Half;
                    }
                    return menu;
                }
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
                        Possibility output = (Possibility)(ActionPossibility)new CombatAction(qfManifestEidolon.Owner, eidolon.Illustration, "Manifest Eidolon", new Trait[] {
                                tSummoner, Trait.Concentrate, Trait.Conjuration, Trait.Manipulate, Trait.Teleportation, spellList
                        },
                            "Your eidolon appears in an open space adjacent to you, and can then take a single action.\n\nThe conduit that allows your eidolon to manifest is also a tether between you. " +
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
                            await PartnerActs(summoner, eidolon, true, null);
                            eidolon.RemoveAllQEffects(effect => effect == actTogether);
                        }))
                        .WithActionCost(3);

                        output.WithPossibilityGroup("Summoner");
                        return output;
                    } else {
                        Possibility output = (Possibility)(ActionPossibility)new CombatAction(qfManifestEidolon.Owner, illDismiss, "Dismiss Eidolon", new Trait[] {
                            tSummoner, Trait.Concentrate, Trait.Conjuration, Trait.Manipulate, Trait.Teleportation, spellList
                        },
                            "Dismiss your eidolon, protecting it and yourself from harm.", Target.RangedFriend(20).WithAdditionalConditionOnTargetCreature((CreatureTargetingRequirement)new EidolonCreatureTargetingRequirement(qfSummonerBond)))
                        .WithEffectOnChosenTargets((Func<Creature, ChosenTargets, Task>)(async (self, targets) => {
                            self.Battle.RemoveCreatureFromGame(eidolon);
                        }))
                        .WithActionCost(3);

                        output.WithPossibilityGroup("Summoner");
                        return output;
                    }
                }),
            })
            ));
        }

        //++combatActionExecution.user.Actions.AttackedThisManyTimesThisTurn

        private static Creature CreateEidolon(FeatName featName, int[] abilityScores, int ac, int dexCap, Creature summoner) {
            Creature eidolon = CreateEidolonBase("Eidolon", summoner, abilityScores, ac, dexCap);

            // Link to summoner
            eidolon.AddQEffect(new HPShareEffect(eidolon) {
                Id = qfSummonerBond,
                Source = summoner
            });
            summoner.AddQEffect(new HPShareEffect(summoner) {
                Id = qfSummonerBond,
                Source = eidolon
            });

            // Add spellcasting
            SpellcastingSource spellSource = eidolon.AddSpellcastingSource(SpellcastingKind.Innate, tSummoner, Ability.Charisma, summoner.PersistentCharacterSheet.Calculated.SpellRepertoires[tSummoner].SpellList);
            eidolon.Spellcasting.FocusPointsMaximum = summoner.Spellcasting.FocusPointsMaximum;
            eidolon.Spellcasting.FocusPoints = summoner.Spellcasting.FocusPointsMaximum;

            // Add skill profs
            List<KeyValuePair<Trait, Proficiency>> skillProfs = summoner.Proficiencies.AllProficiencies.ToList()
                //.Where(t => t.Key != Trait.Spell && t.Key != tSummoner && t.Key != Trait.Simple && t.Key != Trait.Martial && t.Key != Trait.Unarmed && t.Key != Trait.UnarmoredDefense).ToList();
                .Where(t => Skills.TraitToSkill(t.Key) != null).ToList();
            foreach (KeyValuePair<Trait, Proficiency> skill in skillProfs) {
                eidolon.WithProficiency(skill.Key, skill.Value);
            }

            // Generate natural weapon attacks
            Feat pAttack = summoner.PersistentCharacterSheet.Calculated.AllFeats.FirstOrDefault((Func<Feat, bool>)(ft => ft.HasTrait(tPrimaryAttackType)));
            Feat sAttack = summoner.PersistentCharacterSheet.Calculated.AllFeats.FirstOrDefault((Func<Feat, bool>)(ft => ft.HasTrait(tSecondaryAttackType)));
            Feat pStatsFeat = summoner.PersistentCharacterSheet.Calculated.AllFeats.FirstOrDefault((Func<Feat, bool>)(ft => ft.HasTrait(tPrimaryAttackStats)));
            List<Trait> pStats = new List<Trait>() { Trait.Unarmed };
            for (int i = 2; i < pStatsFeat.Traits.Count; i++) {
                pStats.Add(pStatsFeat.Traits[i]);
            }
            List<Trait> sStats = new List<Trait>() { Trait.Unarmed, Trait.Finesse, Trait.Agile };

            DamageKind primaryDamageType;
            if (summoner.PersistentCharacterSheet.Calculated.AllFeats.FirstOrDefault(ft => ft.HasTrait(tEnergyHeartWeapon)) != null &&
                summoner.PersistentCharacterSheet.Calculated.AllFeats.FirstOrDefault(ft => ft.HasTrait(tEnergyHeartWeapon)).Name == "Primary Unarmed Attack") {
                primaryDamageType = TraitToDamage(summoner.PersistentCharacterSheet.Calculated.AllFeats.FirstOrDefault(ft => ft.HasTrait(tEnergyHeartDamage)).Traits[0]);
                pStats.Add(DamageToTrait(primaryDamageType));
            }  else if (new FeatName[] { ftPMace, ftPWing, ftPKick, ftPFist, ftPTendril }.Contains(pAttack.FeatName)) {
                primaryDamageType = DamageKind.Bludgeoning;
            } else if (new FeatName[] { ftPPolearm, ftPHorn, ftPTail }.Contains(pAttack.FeatName)) {
                primaryDamageType = DamageKind.Piercing;
            } else {
                primaryDamageType = DamageKind.Slashing;
            }

            DamageKind secondaryDamageType;
            if (summoner.PersistentCharacterSheet.Calculated.AllFeats.FirstOrDefault(ft => ft.HasTrait(tEnergyHeartWeapon)) != null &&
                summoner.PersistentCharacterSheet.Calculated.AllFeats.FirstOrDefault(ft => ft.HasTrait(tEnergyHeartWeapon)).Name == "Secondary Unarmed Attack") {
                secondaryDamageType = TraitToDamage(summoner.PersistentCharacterSheet.Calculated.AllFeats.FirstOrDefault(ft => ft.HasTrait(tEnergyHeartDamage)).Traits[0]);
                sStats.Add(DamageToTrait(secondaryDamageType));
            } else if (new FeatName[] { ftSWing, ftSKick, ftSFist, ftSTendril }.Contains(sAttack.FeatName)) {
                secondaryDamageType = DamageKind.Bludgeoning;
            } else if (new FeatName[] { ftSHorn, ftSTail }.Contains(sAttack.FeatName)) {
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

            eidolon.WithUnarmedStrike(new Item(pIcon, pAttack.Name.ToLower(), pStats.ToArray()).WithWeaponProperties(new WeaponProperties(damage, primaryDamageType)));
            eidolon.WithAdditionalUnarmedStrike(new Item(sIcon, sAttack.Name.ToLower(), sStats.ToArray()).WithWeaponProperties(new WeaponProperties("1d6", secondaryDamageType)));

            var evoFeats = summoner.PersistentCharacterSheet.Calculated.AllFeats.Where(ft => ft.HasTrait(tEvolution)).ToArray();
            evoFeats = Array.ConvertAll(evoFeats, ft => (EvolutionFeat)ft);

            eidolon.AddQEffect(new QEffect() {
                ProvideMainAction = (effect) => {
                    if (summoner.PersistentCharacterSheet.Calculated.AllFeats.Where(ft => ft.HasTrait(tTandem)).ToList().Count > 0) {
                        SubmenuPossibility tandemActions = new SubmenuPossibility(illActTogether, "Tandem Actions");
                        tandemActions.Subsections.Add(new PossibilitySection("Tandem Actions"));
                        tandemActions.Subsections[0].PossibilitySectionId = psTandemActions;
                        return tandemActions;
                    }

                    return GenerateActTogetherAction(effect.Owner, summoner, summoner);

                },
                ProvideActionIntoPossibilitySection = (qfActTogether, section) => {
                    if (summoner.PersistentCharacterSheet.Calculated.AllFeats.Where(ft => ft.HasTrait(tTandem)).ToList().Count == 0) {
                        return null;
                    } else if (section.PossibilitySectionId == psTandemActions) {
                        return GenerateActTogetherAction(qfActTogether.Owner, summoner, summoner);
                    }
                    return null;
                },
            })
            .AddQEffect(new QEffect() {
                ProvideMainAction = (Func<QEffect, Possibility>)(qfEidolon => {
                    Creature? summoner = GetSummoner(qfEidolon.Owner);
                    if (summoner == null || summoner.OwningFaction != qfEidolon.Owner.OwningFaction || !summoner.Actions.CanTakeActions() || qfEidolon.Owner.QEffects.FirstOrDefault(qf => qf.Id == qfActTogether) != null)
                        return (Possibility)null;
                    Possibility output = (Possibility)(ActionPossibility)new CombatAction(qfEidolon.Owner, summoner.Illustration, "Return Control",
                        new Trait[] { Trait.Basic, tSummoner }, $"Switch back to controlling {summoner.Name}. All unspent actions will be retained.", (Target)Target.Self())
                    .WithActionCost(0)
                    .WithActionId(ActionId.EndTurn)
                    .WithEffectOnSelf((Action<Creature>)(self => {
                        // Remove act together toggle on eidolon
                        self.RemoveAllQEffects(qf => qf.Id == qfActTogetherToggle);
                        // Remove and log actions
                        ActionShareEffect actionShare = (ActionShareEffect)self.QEffects.FirstOrDefault(qf => qf.Id == qfSharedActions);
                        actionShare.LogTurnEnd(self.Actions);
                        self.Actions.UsedQuickenedAction = true;
                        self.Actions.ActionsLeft = 0;
                        self.Actions.WishesToEndTurn = true;
                        Sfxs.Play(SfxName.EndOfTurn, 0.2f);
                    }));

                    //output.WithPossibilityGroup("Summoner");
                    return output;
                })
            });

            // Add subclasses
            if (featName == scAngelicEidolonAvenger || featName == scAngelicEidolonEmmissary) {
                eidolon.AddQEffect(new QEffect("Hallowed Strikes", "Your eidolon's unarmed strikes deal +1 extra good damage.") {
                    AddExtraKindedDamageOnStrike = (action, target) => {
                        return new KindedDamage(DiceFormula.FromText("1", "Hallowed Strikes"), DamageKind.Good);
                    },
                });
                if (eidolon.Level >= 7) {
                    eidolon.UnarmedStrike.Traits.Add(tParry);
                    eidolon.AddQEffect(new QEffect() {
                        ProvideMainAction = (Func<QEffect, Possibility?>)(qfEidolon => {
                            return (Possibility)(ActionPossibility)new CombatAction(eidolon, IllustrationName.GenericCombatManeuver, "Parry", new Trait[] { }, "Your eidolon gains a +1 bonus to AC until the start of your next turn, and can use the Angelic Aegis action.", Target.Self()
                                .WithAdditionalRestriction(self => self.HasEffect(qfParrying) == true ? "Already parrying" : null))
                            .WithActionCost(1)
                            .WithSoundEffect(SfxName.RaiseShield)
                            .WithEffectOnSelf(self => {
                                self.AddQEffect(new QEffect("Parrying", "You have a +1 circumstance bonus to AC.") {
                                    Id = qfParrying,
                                    Illustration = IllustrationName.GenericCombatManeuver,
                                    Source = eidolon,
                                    ExpiresAt = ExpirationCondition.ExpiresAtStartOfSourcesTurn,
                                    BonusToDefenses = (qf, action, defence) => {
                                        if (defence != Defense.AC) {
                                            return (Bonus)null;
                                        }
                                        return new Bonus(1, BonusType.Circumstance, "Parrying");
                                    },
                                });
                            });
                        })
                    });
                    eidolon.AddQEffect(new QEffect() {
                        ProvideMainAction = (Func<QEffect, Possibility?>)(qfEidolon => {
                            if (eidolon.HasEffect(qfParrying) == false) {
                                return null;
                            }
                            return (Possibility)(ActionPossibility)new CombatAction(eidolon, illAngelicAegis, "Angelic Aegis", new Trait[] { },
                                "{b}Frequency{/b} Once per round\n{b}Requirements{/b} Your eidolon is parrying.\n\n" +
                                "Adjacent ally gains a +2 circumstance bonus to their AC and, as a reaction, your eidolon " +
                                "may intercept any attack that deals physical damage to them, reducing the damage taken by an amount equal to your level." +
                                "\n\nThese benefits only apply whilst the target ally is adjacent to your eidolon.", Target.AdjacentFriend()) {
                                ShortDescription = "Adjacent ally gains a +2 circumstance bonus to their AC and, as a reaction, your eidolon " +
                                "may intercept any attack that deals physical damage to them, reducing the damage taken by an amount equal to your level."
                            }
                            .WithActionCost(0)
                            .WithProjectileCone(illAngelicAegis, 5, ProjectileKind.Ray)
                            .WithSoundEffect(SfxName.Abjuration)
                            .WithEffectOnSelf(self => {
                                self.AddQEffect(new QEffect() {
                                    ExpiresAt = ExpirationCondition.ExpiresAtStartOfYourTurn,
                                    PreventTakingAction = (action => action.Name == "Angelic Aegis" ? "Once per round limit." : null)
                                });
                            })
                            .WithEffectOnEachTarget(async (action, self, target, checkResult) => {
                                target.AddQEffect(new QEffect("Angelic Aegis", $"You have a +2 circumstance bonus to AC and can be protected by {eidolon}, so long as you're adjacent to them.") {
                                    Illustration = illAngelicAegis,
                                    Source = eidolon,
                                    ExpiresAt = ExpirationCondition.ExpiresAtStartOfSourcesTurn,
                                    BonusToDefenses = (qf, action, defence) => {
                                        if (defence != Defense.AC || qf.Owner.DistanceTo(qf.Source) > 1) {
                                            return (Bonus)null;
                                        }
                                        return new Bonus(2, BonusType.Circumstance, "Angelic Aegis");
                                    },
                                    YouAreDealtDamage = async (qf, attacker, damage, defender) => {
                                        if (qf.Owner.DistanceTo(qf.Source) > 1) {
                                            return null;
                                        }
                                        if (new DamageKind[] { DamageKind.Bludgeoning, DamageKind.Slashing, DamageKind.Piercing }.Contains(damage.Kind) == false) {
                                            return null;
                                        }
                                        if (await eidolon.Battle.AskToUseReaction(eidolon, "{b}" + attacker + "{/b} uses {b}" + damage.Power.Name + "{/b} for " + damage.Amount + $"damage, which provokes Angelic Aegis Interception.\nUse your reaction reduce the damage by {qf.Source.Level}?")) {
                                            return (DamageModification)new ReduceDamageModification(qf.Source.Level, "Angelic Aegis Interception");
                                        }
                                        return null;
                                    }
                                });
                            });
                        }),
                    });
                }
            } else if (featName == scDraconicEidolonCunning || featName == scDraconicEidolonMarauding) {
                SpellRepertoire repertoire = summoner.PersistentCharacterSheet.Calculated.SpellRepertoires[tSummoner];
                int saveDC = summoner.GetOrCreateSpellcastingSource(SpellcastingKind.Spontaneous, tSummoner, Ability.Charisma, repertoire.SpellList).GetSpellSaveDC();

                Trait damageTrait = summoner.PersistentCharacterSheet.Calculated.AllFeats.FirstOrDefault((Func<Feat, bool>)(ft => ft.HasTrait(tDragonType))).Traits[0];
                FeatName targetFeat = summoner.PersistentCharacterSheet.Calculated.AllFeats.FirstOrDefault((Func<Feat, bool>)(ft => ft.HasTrait(tBreathWeaponArea))).FeatName;
                Target target;
                if (targetFeat == ftBreathWeaponLine) {
                    target = Target.Line(12);
                } else {
                    target = Target.Cone(6);
                }
                DamageKind damageKind = DamageKind.Fire;
                Defense save = Defense.Reflex;
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
                } else if (damageTrait == Trait.Positive) {
                    damageKind = DamageKind.Positive;
                    save = Defense.Reflex;
                    damageName = "positive";
                } else if (damageTrait == Trait.Negative) {
                    damageKind = DamageKind.Negative;
                    save = Defense.Reflex;
                    damageName = "negative";
                }

                Trait[] traits = new Trait[] { };
                if (damageTrait != Trait.VersatileP) {
                    traits = new Trait[] { damageTrait };
                }

                eidolon.AddQEffect(new QEffect() {
                    ProvideMainAction = (Func<QEffect, Possibility>)(qfEidolon => {
                        Creature summoner = GetSummoner(qfEidolon.Owner);
                        int dice = 1 + (summoner.Level + 1) / 2;
                        CombatAction breathWeapon = new CombatAction(qfEidolon.Owner, IllustrationName.BreathWeapon, "Breath Weapon", traits, "{b}Range{/b} " +
                            (targetFeat == ftBreathWeaponCone ? "30-foot cone" : "60-foot line") + "\n{b}Saving Throw{/b} " +
                            (save == Defense.Reflex ? "Reflex" : save == Defense.Fortitude ? "Fortitude" : "Will") +
                            "\n\nYour eidolon exhales a breath of destructive energy. Deal " + dice + "d6 " + damageName +
                            " damage to each creature in the area.\n\n{b}Special.{/b}Your eidolon can't use breath weapon again for 1d4 rounds.\n\nAt 3rd level, and every 2 levels thereafter, damage increases by 1d6." +
                            (damageKind == DamageKind.Negative ? "\n\nTheir ghost killing breath weapon deals force damage to undead creatures." : ""), target) {
                            ShortDescription = "Your eidolon exhales a breath of destructive energy. Deal " + dice + "d6 " + damageName + " damage to each creature in the area."
                        }
                        .WithSavingThrow(new SavingThrow(save, (_ => saveDC)))
                        .WithEffectOnEachTarget(async (action, self, target, checkResult) => {
                            if (target.HasTrait(Trait.Undead) && damageKind == DamageKind.Negative) {
                                await CommonSpellEffects.DealBasicDamage(action, self, target, checkResult, $"{dice}d6", DamageKind.Force);
                            } else {
                                await CommonSpellEffects.DealBasicDamage(action, self, target, checkResult, $"{dice}d6", damageKind);
                            }
                        })
                        .WithEffectOnChosenTargets(async (self, defenders) => {
                            int num = R.Next(1, 5);

                            self.AddQEffect(new QEffect("Recharging Fire Breath", "This creature can't use Fire Breath until the value counts down to zero.", ExpirationCondition.CountsDownAtEndOfYourTurn, self, (Illustration)IllustrationName.Recharging) {
                                Id = QEffectId.Recharging,
                                CountsAsADebuff = true,
                                PreventTakingAction = (Func<CombatAction, string>)(ca => !(ca.Name == "Breath Weapon") ? (string)null : "This ability is recharging."),
                                Value = num
                            });
                        })
                        .WithSoundEffect(SfxName.Fireball)
                        .WithActionCost(2);

                        if (targetFeat == ftBreathWeaponLine) {
                            breathWeapon.WithProjectileCone(IllustrationName.BreathWeapon, 30, ProjectileKind.Cone);
                        } else {
                            breathWeapon.WithProjectileCone(IllustrationName.BreathWeapon, 20, ProjectileKind.Ray);
                        }

                        Possibility output = (Possibility)(ActionPossibility)breathWeapon;
                        if (eidolon.Level >= 7) {
                            output.PossibilitySize = PossibilitySize.Half;
                        }

                        return output;
                    }),
                });

                if (eidolon.Level >= 7) {
                    eidolon.AddQEffect(new QEffect() {
                        ProvideMainAction = (Func<QEffect, Possibility>)(qfEidolon => {
                            Possibility output = (Possibility)(ActionPossibility)new CombatAction(eidolon, illDraconicFrenzy, "Draconic Frenzy", new Trait[] { },
                                "Your eidolon makes one Strike with their primary unarmed attack and two Strikes with their secondary unarmed attack (in any order). " +
                                "If any of these attacks critically hits an enemy, your eidolon instantly recovers the use of their Breath Weapon.",
                                Target.Self().WithAdditionalRestriction((Func<Creature, string>)(self => {
                                    if (!self.CanMakeBasicUnarmedAttack && self.QEffects.All<QEffect>(qf => qf.AdditionalUnarmedStrike == null))
                                        return "You must be able to attack to use Draconic Frenzy.";
                                    foreach (Item obj in self.MeleeWeapons.Where<Item>((Func<Item, bool>)(weapon => weapon.HasTrait(Trait.Unarmed)))) {
                                        if (self.CreateStrike(obj).CanBeginToUse(self).CanBeUsed)
                                            return (string)null;
                                    }
                                    return "There is no nearby enemy or you can't make attacks.";
                                })))
                            .WithActionCost(2)
                            .WithSoundEffect(SfxName.BeastRoar)
                            .WithEffectOnEachTarget(async (action, self, target, result) => {
                                self.AddQEffect(new QEffect() {
                                    Value = 0,
                                    Key = "Draconic Frenzy",
                                    AfterYouDealDamage = async (self2, action2, target) => {
                                        if (action2 == null || !action2.Name.StartsWith("Strike (")) {
                                            return;
                                        }
                                        if (action2.CheckResult == CheckResult.CriticalSuccess) {
                                            if (self2.RemoveAllQEffects(qf => qf.Name == "Recharging Fire Breath") > 0) {
                                                self.Occupies.Overhead("{b}{i}breath weapon recharged{/i}{/b}", Color.White, self?.ToString() + "'s breath weapon has recharged.");
                                            }
                                        }
                                    }
                                });
                                for (int i = 0; i < 3; ++i) {
                                    await self.Battle.GameLoop.StateCheck();
                                    List<Option> options = new List<Option>();
                                    CombatAction strike = self.CreateStrike((i == 0 ? self.UnarmedStrike : self.MeleeWeapons.ToArray()[1]));
                                    strike.WithActionCost(0);
                                    GameLoop.AddDirectUsageOnCreatureOptions(strike, options, true);
                                    if (options.Count > 0) {
                                        Option chosenOption;
                                        if (options.Count >= 2) {
                                            options.Add((Option)new CancelOption(true));
                                            chosenOption = (await self.Battle.SendRequest(new AdvancedRequest(self, "Choose a creature to Strike.", options) {
                                                TopBarText = $"Choose a creature to Strike or right-click to cancel. ({i + 1}/3)",
                                                TopBarIcon = illDraconicFrenzy
                                            })).ChosenOption;
                                        } else
                                            chosenOption = options[0];

                                        if (chosenOption is CancelOption) {
                                            action.RevertRequested = true;
                                            return;
                                        }
                                        int num = await chosenOption.Action() ? 1 : 0;
                                    }
                                }
                                self.RemoveAllQEffects(qf => qf.Key == "Draconic Frenzy");
                            });
                            output.PossibilitySize = PossibilitySize.Half;
                            return output;
                        }),
                    });
                }
            } else if (summoner.HasFeat(scBeastEidolon)) {
                eidolon.AddQEffect(new QEffect() {
                    ProvideMainAction = qfSelf => {
                        SubmenuPossibility output = new SubmenuPossibility(illBeastsCharge, "Beast's Charge");
                        output.Subsections.Add(new PossibilitySection("Charge Option"));

                        output.Subsections[0].AddPossibility((ActionPossibility) new CombatAction(qfSelf.Owner, illBeastsCharge, "Beast's Charge (Line)", new Trait[] { Trait.Move },
                            "Stride up to twice your speed in a direct line, then strike. If you moved at least 20-feet, the strike gains a +1 circumstance bonus." +
                            "\n\nThis movement will not path around hazards or attacks of opportunity.",
                            Target.Self())
                        .WithActionCost(2)
                        .WithSoundEffect(SfxName.Footsteps)
                        .WithEffectOnSelf(async (action, self) => {
                            MovementStyle movementStyle = new MovementStyle() {
                                //MaximumSquares = self.Speed * 2 * 5,
                                MaximumSquares = self.Speed * 2,
                                ShortestPath = false,
                                PermitsStep = false,
                                IgnoresUnevenTerrain = false,
                            };

                            Tile startingTile = self.Occupies;
                            Tile? destTile = await GetChargeTiles(self, movementStyle, 4, "Choose where to Stride with Beast's Charge.", illBeastsCharge);

                            if (destTile == null) {
                                action.RevertRequested = true;
                            } else {
                                movementStyle.Shifting = self.HasEffect(QEffectId.Mobility) && destTile.InIteration.RequiresProvokingAttackOfOpportunity;
                                await self.MoveTo(destTile, action, movementStyle);
                                QEffect? chargeBonus = null;
                                if (self.DistanceTo(startingTile) >= 4) {
                                    self.AddQEffect(chargeBonus = new QEffect("Charge Bonus", "+1 circumstance bonus to your next strike action.") {
                                        BonusToAttackRolls = (qf, action, target) => {
                                            return new Bonus(1, BonusType.Circumstance, "Beast's Charge");
                                        },
                                        Illustration = illBeastsCharge,
                                    });
                                }
                                await CommonCombatActions.StrikeAdjacentCreature(self);
                                if (chargeBonus != null) {
                                    chargeBonus.ExpiresAt = ExpirationCondition.Immediately;
                                }
                            }
                        })
                        );

                        //output.Subsections[0].AddPossibility((ActionPossibility)new CombatAction(qfSelf.Owner, illBeastsCharge, "Beast's Charge (Line)", new Trait[] { Trait.Move },
                        //    "Stride up to twice your speed in a line. If you end your movement within melee reach of at least one enemy, you can make a melee Strike against them. If you moved at least 20-feet, the strike gains a +1 circumstance bonus.",
                        //    Target.Line(qfSelf.Owner.Speed * 2).WithLesserDistanceIsOkay().WithIsBurningJet())
                        //.WithActionCost(2)
                        //.WithSoundEffect(SfxName.Footsteps)
                        //.WithEffectOnChosenTargets(async (action, self, targets) => {
                        //    Tile startingTile = self.Occupies;
                        //    Tile finalTile = LineAreaTarget.DetermineFinalTile(self.Occupies, targets.ChosenTiles);
                        //    if (finalTile == null) {
                        //        return;
                        //    }
                        //    await self.MoveTo(finalTile, action, new MovementStyle() {
                        //        Shifting = false,
                        //        ShortestPath = true,
                        //        PermitsStep = false,
                        //        MaximumSquares = 100
                        //    });
                        //    QEffect? chargeBonus = null;
                        //    if (self.DistanceTo(startingTile) >= 4) {
                        //        self.AddQEffect(chargeBonus = new QEffect("Charge Bonus", "+1 circumstance bonus to your next strike action.") {
                        //            BonusToAttackRolls = (qf, action, target) => {
                        //                return new Bonus(1, BonusType.Circumstance, "Beast's Charge");
                        //            },
                        //            Illustration = illBeastsCharge,
                        //        });
                        //    }
                        //    await CommonCombatActions.StrikeAdjacentCreature(self);
                        //    if (chargeBonus != null) {
                        //        chargeBonus.ExpiresAt = ExpirationCondition.Immediately;
                        //    }
                        //}));

                        output.Subsections[0].AddPossibility((ActionPossibility)new CombatAction(qfSelf.Owner, illBeastsCharge, "Beast's Charge (Mobile)", new Trait[] { Trait.Move },
                        "Stride twice. If you end your movement within melee reach of at least one enemy, you can make a melee Strike against that enemy, but do not gain a charge bonus to the attack roll.", (Target)Target.Self())
                        .WithActionCost(2)
                        .WithSoundEffect(SfxName.Footsteps)
                        .WithEffectOnSelf(async (action, self) => {
                            if (!await self.StrideAsync("Choose where to Stride with Beast's Charge. (1/2)", allowCancel: true)) {
                                action.RevertRequested = true;
                            } else {
                                int num = await self.StrideAsync("Choose where to Stride with Beast's Charge. You should end your movement within melee reach of an enemy. (2/2)", allowPass: true) ? 1 : 0;
                                await CommonCombatActions.StrikeAdjacentCreature(self);
                            }
                        }));

                        if (eidolon.Level >= 7) {
                            output.PossibilitySize = PossibilitySize.Half;
                        }
                        return output;
                    },
                });
                if (eidolon.Level >= 7) {
                    eidolon.AddQEffect(new QEffect() {
                        ProvideMainAction = (Func<QEffect, Possibility?>)(qfEidolon => {
                            Possibility output = (Possibility)(ActionPossibility)new CombatAction(eidolon, illPrimalRoar, "Primal Roar", new Trait[] { },
                                "Your eidolon unleashes a primal roar or other such terrifying noise that fits your eidolon's form. Your eidolon attempts Intimidation " +
                                "checks with a +2 bonus to Demoralize each enemy that can hear the roar; these Demoralize attempts don't take any penalty for not sharing a language.",
                                Target.Emanation(30))
                            .WithActionCost(2)
                            .WithSoundEffect(SfxName.BeastRoar)
                            .WithActiveRollSpecification(new ActiveRollSpecification(Checks.SkillCheck(Skill.Intimidation), Checks.DefenseDC(Defense.Will)))
                            .WithNoSaveFor((action, target) => target.OwningFaction == action.Owner.OwningFaction || target.QEffects.FirstOrDefault(qf => qf.Name == "Immunity to " + ActionId.Demoralize.HumanizeTitleCase2() + " by " + action.Owner.Name) != null)
                            .WithEffectOnEachTarget(async (action, self, target, checkResult) => {
                                if (target.OwningFaction == action.Owner.OwningFaction) {
                                    target.Occupies.Overhead("{b}{i}unaffected{/i}{/b}", Color.White, target?.ToString() + " is an ally.");
                                    return;
                                }

                                if (target.QEffects.FirstOrDefault(qf => qf.Name == "Immunity to " + ActionId.Demoralize.HumanizeTitleCase2() + " by " + self.Name) != null) {
                                    target.Occupies.Overhead("{b}{i}unaffected{/i}{/b}", Color.White, target?.ToString() + " has already been demoralized this combat.");
                                    return;
                                }

                                if (checkResult == CheckResult.CriticalSuccess) {
                                    target.AddQEffect(QEffect.Frightened(2));
                                } else if (checkResult == CheckResult.Success) {
                                    target.AddQEffect(QEffect.Frightened(1));
                                }
                                target.AddQEffect(QEffect.ImmunityToTargeting(ActionId.Demoralize, self));
                            });
                            output.PossibilitySize = PossibilitySize.Half;
                            return output;
                        }),
                        BonusToSkillChecks = (skill, action, target) => {
                            return new Bonus(2, BonusType.Untyped, "Primal Roar");
                        }
                    });
                }
            } else if (summoner.HasFeat(scDevoPhantomEidolon)) {
                eidolon.AddQEffect(new QEffect("Dutiful Retaliation {icon:Reaction}", "Your eidolon makes a strike again an enemy that damaged you. Both your eidolon and your attacker must be within 15ft of you."));
                summoner.AddQEffect(new QEffect() {
                    AfterYouTakeDamage = (async (qf, amount, damagekind, action, critical) => {
                        if (!action.Name.StartsWith("Strike (") || action.Owner == null) {
                            return;
                        }

                        Creature eidolon = GetEidolon(qf.Owner);

                        if (eidolon == null || eidolon.Destroyed || !eidolon.Actions.CanTakeActions()) {
                            return;
                        }

                        if (qf.Owner.DistanceTo(action.Owner) <= 3 && qf.Owner.DistanceTo(eidolon) <= 3) {
                            CombatAction combatAction = eidolon.CreateStrike(eidolon.UnarmedStrike, 0).WithActionCost(0);

                            // Check if eidolon cannot make strikes
                            foreach (QEffect restriction in eidolon.QEffects) {
                                if (restriction.PreventTakingAction != null && restriction.PreventTakingAction(combatAction) != null) {
                                    return;
                                }
                            }

                            // Check if triggering creature cannot be targetted
                            foreach (QEffect condition in action.Owner.QEffects) {
                                if (condition.PreventTargetingBy != null && condition.PreventTargetingBy(combatAction) != null) {
                                    return;
                                }
                            }

                            if (await eidolon.Battle.AskToUseReaction(eidolon, "{b}" + action.Owner.Name + "{/b} uses {b}" + action.Name + "{/b} which provokes Dutiful Retaliation.\nUse your reaction to make an attack of opportunity?")) {
                                int map = eidolon.Actions.AttackedThisManyTimesThisTurn;
                                eidolon.Occupies.Overhead("*dutiful devotion*", Color.White);
                                await eidolon.MakeStrike(action.Owner, eidolon.UnarmedStrike, 0);
                                eidolon.Actions.AttackedThisManyTimesThisTurn = map;
                            }
                        }
                    })
                });
                if (eidolon.Level >= 7) {
                    eidolon.AddQEffect(new QEffect() {
                        ProvideMainAction = (Func<QEffect, Possibility?>)(qfEidolon => {
                            return (Possibility)(ActionPossibility)new CombatAction(eidolon, illDevoStance, "Devotion Stance", new Trait[] { }, "Your eidolon takes on a patient defensive stance, steeling their focus with thoughts of their devotion.\n\nUntil the start of their next turn, they gain a +2 circumstance bonus to AC, and a +4 bonus to damage to attacks made outside their turn.", Target.Self())
                            .WithActionCost(1)
                            .WithSoundEffect(SfxName.RaiseShield)
                            .WithEffectOnSelf(self => {
                                self.AddQEffect(new QEffect() {
                                    ExpiresAt = ExpirationCondition.ExpiresAtStartOfYourTurn,
                                    PreventTakingAction = (action => action.Name == "Devotion Stance" ? "Once per round limit." : null)
                                });
                                self.AddQEffect(new QEffect("Devotion Stance", "You have a +2 circumstance bonus to AC, and deal +4 damage with attacks made outside your turn.") {
                                    Illustration = illDevoStance,
                                    Source = eidolon,
                                    ExpiresAt = ExpirationCondition.ExpiresAtStartOfSourcesTurn,
                                    BonusToDefenses = (qf, action, defence) => {
                                        if (defence != Defense.AC) {
                                            return (Bonus)null;
                                        }
                                        return new Bonus(2, BonusType.Circumstance, "Devotion Stance");
                                    },
                                    BonusToDamage = (qf, action, defender) => {
                                        if (qf.Owner.Battle.ActiveCreature == qf.Owner || qf.Owner.Battle.ActiveCreature == GetSummoner(qf.Owner)) {
                                            return null;
                                        }
                                        return new Bonus(4, BonusType.Untyped, "Devotion Stance");
                                    }
                                });
                            });
                        })
                    });
                }
            } else if (summoner.HasFeat(scAzataEidolon)) {
                eidolon.AddQEffect(new QEffect() {
                    ProvideMainAction = qfCelestialPassion => {
                        return (Possibility)(ActionPossibility)new CombatAction(qfCelestialPassion.Owner, IllustrationName.AngelicHalo, "Celestial Passion", new Trait[] { Trait.Divine, Trait.Concentrate, Trait.Emotion, Trait.Mental, Trait.Auditory }, "", Target.RangedFriend(3))
                        .WithProjectileCone(IllustrationName.AngelicHalo, 10, ProjectileKind.Cone)
                        .WithActionId(acCelestialPassion)
                        .WithActionCost(1)
                        .WithSoundEffect(SfxName.Bless)
                        .WithEffectOnEachTarget(async (action, caster, target, result) => {
                            target.GainTemporaryHP(caster.Level);
                            target.AddQEffect(new QEffect("Celestial Passion", "...") {
                                Illustration = IllustrationName.AngelicHalo,
                                Source = caster,
                                ExpiresAt = ExpirationCondition.ExpiresAtStartOfSourcesTurn,
                                BonusToAttackRolls = (qf, action, defender) => new Bonus(1, BonusType.Status, "Celestial Passion"),
                                BonusToSkillChecks = (skill, action, target) => new Bonus(1, BonusType.Status, "Celestial Passion"),
                            });
                            QEffect.ImmunityToTargeting(acCelestialPassion, caster);
                        })
                        ;
                    }
                });
                if (eidolon.Level >= 7) {
                    AuraAnimation auraAnimation = eidolon.AnimationData.AddAuraAnimation(IllustrationName.BlessCircle, 3f);
                    //auraAnimation.Color = Color.LightSeaGreen;
                    auraAnimation.Color = Color.LawnGreen;
                    //auraAnimation.Color = Color.HotPink;

                    eidolon.AddQEffect(new QEffect("Whimsical Aura", "Your eidolon has a +5ft status bonus to its speed, and grants this benefit to all allies that start their turn within 15ft of it. At the end of your eidolon's turn, all allies within the aura reduce their frightened condition by 1.") {
                        BonusToAllSpeeds = qf => new Bonus(1, BonusType.Status, "Whimsical Aura"),
                        StateCheck = effect => {
                            //qf.Tag = qf.Owner.Battle.AllCreatures.Where(creature => creature.OwningFaction == eidolon.OwningFaction);
                            foreach (Creature creature in effect.Owner.Battle.AllCreatures) {
                                if (creature.HasEffect(qfWhimsicalAura) || creature.QEffects.FirstOrDefault(qf => qf.Name == "Whimsical Aura") != null) {
                                    continue;
                                }
                                creature.AddQEffect(new QEffect() {
                                    Id = qfWhimsicalAura,
                                    Tag = false,
                                    StartOfYourTurn = async (effect, self) => {
                                        effect.Tag = false;
                                        List<Creature> auraHavers = self.Battle.AllCreatures.Where(c => c.OwningFaction == self.OwningFaction && c.QEffects.FirstOrDefault(qf => qf.Name == "Whimsical Aura") != null && c.DistanceTo(self) <= 3).ToList();
                                        if (auraHavers.Count > 0) {
                                            effect.Tag = true;
                                            self.Occupies.Overhead("*+5ft status bonus to speed*", Color.White);
                                        }
                                    },
                                    BonusToAllSpeeds = qf => (bool)qf.Tag == true ? new Bonus(1, BonusType.Status, "Whimsical Aura") : null,
                                });
                            }
                        },
                        EndOfYourTurn = async (qf, self) => {
                            foreach (Creature ally in qf.Owner.Battle.AllCreatures.Where(creature => creature.OwningFaction == eidolon.OwningFaction)) {
                                if (ally.DistanceTo(self) > 3) {
                                    continue;
                                }
                                QEffect? frightened = ally.QEffects.FirstOrDefault(effect => effect.Id == QEffectId.Frightened);
                                if (frightened != null) {
                                    frightened.Value -= 1;
                                    if (frightened.Value == 0) {
                                        frightened.ExpiresAt = ExpirationCondition.Immediately;
                                    }
                                    ally.Occupies.Overhead("*frightened reduced by 1*", Color.White);
                                }
                            }
                        }
                    });
                }
            } else if (summoner.HasFeat(scDevilEidolon)) {
                eidolon.AddQEffect(QEffect.DamageResistance(DamageKind.Fire, Math.Max(1, (eidolon.Level / 2))));
                eidolon.AddQEffect(QEffect.DamageWeakness(DamageKind.Good, Math.Max(1, (eidolon.Level / 2))));
                eidolon.AddQEffect(new QEffect("Hellfire Scourge", "Your eidolon deals +1d4 fire damage to the first frightened creature it strikes each round.") {
                    Tag = false,
                    StartOfYourTurn = async (effect, self) => {
                        effect.Tag = false;
                    },
                    AddExtraKindedDamageOnStrike = (action, target) => {
                        if (target.HasEffect(QEffectId.Frightened) && (bool)action.Owner.QEffects.FirstOrDefault(qf => qf.Name == "Hellfire Scourge").Tag == false) {
                            action.Owner.QEffects.FirstOrDefault(qf => qf.Name == "Hellfire Scourge").Tag = true;
                            return new KindedDamage(DiceFormula.FromText("1d4"), DamageKind.Fire);
                        }
                        return null;
                    }
                });
                if (eidolon.Level >= 7) {
                    eidolon.AddQEffect(new QEffect() {
                        ProvideMainAction = effect => {
                            return new ActionPossibility(new CombatAction(effect.Owner, illDisciplineTheLegion, "Discipline the Legion", new Trait[] {
                                tSummoner, Trait.Linguistic, Trait.Concentrate, Trait.Mental, Trait.Emotion },
                                "Your eidolon shouts a commands at one ally within 30-feet. The next time that ally attacks or makes a skill check before the start of your next turn, your eidolon can " +
                                "use their reaction to make an Intimidation check against an easy DC for their level.\n\n{b}Critical Success{/b} You grant your ally a +2 circumstance bonus to the triggering " +
                                "check. If you’re a master with the check you attempted, the bonus is +3, and if you’re legendary, it’s +4.\n{b}Success{/b} You grant your ally a +1 circumstance bonus to " +
                                "the triggering check.\n{b}Critical Failure{/b} Your ally takes a –1 circumstance penalty to the triggering check.\n\nYour ally also deals extra fire damage equal to half your " +
                                "level, if the action your eidolon was assisting them with was an attack, and double if the attack was a critial success.", (Target)Target.RangedFriend(6).WithAdditionalConditionOnTargetCreature((source, target) => {
                                    if (target.QEffects.FirstOrDefault(qf => qf.Key == "Infernal Command") == null) {
                                        return Usability.Usable;
                                    }
                                    return Usability.NotUsableOnThisCreature("Your eidolon has already disciplined this creature.");
                                }))
                                .WithActionCost(1)
                                .WithEffectOnChosenTargets((Func<Creature, ChosenTargets, Task>)(async (self, targets) => {
                                    targets.ChosenCreature.AddQEffect(new QEffect("Infernal Command", $"This creature has the attention of {self.Name}, and is ready to be spurred into decisive action.") {
                                        Illustration = illDisciplineTheLegion,
                                        Source = self,
                                        Key = "Infernal Command",
                                        ExpiresAt = ExpirationCondition.ExpiresAtStartOfSourcesTurn,
                                        BeforeYourActiveRoll = async (effect, action, innerTarget) => {
                                            if (await eidolon.Battle.AskToUseReaction(eidolon, "{b}" + effect.Owner + "{/b} is about to use {b}" + action.Name + "{/b} against " + innerTarget?.ToString() + ". \nRoll for Discipline the Legion?")) {
                                                CheckResult result = CommonSpellEffects.RollCheck("Discipline the Legion", new ActiveRollSpecification(Checks.SkillCheck(new Skill[] { Skill.Intimidation, Skill.Deception }), Checks.FlatDC(GetDCByLevel(self.Level) - 2)), self, effect.Owner);
                                                int bonus = 0;

                                                if (result == CheckResult.CriticalSuccess) {
                                                    bonus = Math.Max((int)self.Proficiencies.Get(Trait.Intimidation), (int)self.Proficiencies.Get(Trait.Deception));
                                                    bonus = Math.Max(2, bonus / 2);
                                                } else if (result == CheckResult.Success) {
                                                    bonus = 1;
                                                } else if (result == CheckResult.CriticalFailure) {
                                                    bonus = -1;
                                                }

                                                effect.Owner.AddQEffect(new QEffect() {
                                                    BonusToSkillChecks = (skill, action, target) => {
                                                        return new Bonus(bonus, BonusType.Circumstance, "Infernal Command");
                                                    },
                                                    BonusToAttackRolls = (effect, action, target) => {
                                                        return new Bonus(bonus, BonusType.Circumstance, "Infernal Command");
                                                    },
                                                    AfterYouDealDamage = async (self, action, target) => {
                                                        if (effect.UsedThisTurn) {
                                                            return;
                                                        }

                                                        if (target.OwningFaction != self.OwningFaction && target == innerTarget) {
                                                            int damage = self.Level / 2;
                                                            if (action.CheckResult == CheckResult.CriticalSuccess) {
                                                                damage *= 2;
                                                            }
                                                            effect.UsedThisTurn = true;
                                                            await self.DealDirectDamage(null, DiceFormula.FromText($"{damage}"), target, action.CheckResult, DamageKind.Fire);
                                                        }
                                                    },
                                                    AfterYouTakeAction = async (effect, action) => {
                                                        effect.ExpiresAt = ExpirationCondition.Immediately;
                                                    }
                                                });
                                            }
                                        },
                                    });
                                }))
                            );
                        }
                    });
                }
            } else if (summoner.HasFeat(scAngerPhantom)) {
                eidolon.AddQEffect(new QEffect() {
                    ProvideMainAction = effect => {
                        Possibility output = new ActionPossibility(new CombatAction(effect.Owner, illFrenziedAssault, "Frenzied Assault", new Trait[4] {
                            tSummoner, Trait.Basic, Trait.AlwaysHits, Trait.IsHostile },
                          "Make two Strikes against the same target, one with each of your melee natural weapon attacks, each using your current multiple attack penalty." +
                          "\n\nCombine the damage for the purposes of weakness and resistance. This counts as two attacks when calculating your multiple attack penalty.", (Target)Target.Melee())
                            .WithActionCost(2)
                            .WithEffectOnChosenTargets((Func<Creature, ChosenTargets, Task>)(async (self, targets) => {
                                int map = self.Actions.AttackedThisManyTimesThisTurn;

                                Creature enemy = targets.ChosenCreature;

                                await self.MakeStrike(enemy, self.UnarmedStrike, map);
                                await self.MakeStrike(enemy, self.MeleeWeapons.ToArray()[1], map);

                                GetSummoner(eidolon).Actions.AttackedThisManyTimesThisTurn = self.Actions.AttackedThisManyTimesThisTurn;
                            }))
                        .WithTargetingTooltip((Func<CombatAction, Creature, int, string>)((power, target, index) => power.Description)));
                        if (eidolon.Level >= 7) {
                            output.PossibilitySize = PossibilitySize.Half;
                        }
                        return output;
                    }
                });
                if (eidolon.Level >= 7) {
                    eidolon.AddQEffect(new QEffect() {
                        ProvideMainAction = effect => {
                            if (effect.Owner.HasEffect(qfSeethingFrenzy)) {
                                return null;
                            }
                            Possibility output = new ActionPossibility(new CombatAction(effect.Owner, illSeethingFrenzy, "Seething Frenzy", new Trait[] {
                            tSummoner, Trait.Emotion, Trait.Mental, Trait.Concentrate },
                                "Your eidolon enters a seething frenzy, disregarding its own safety to tear your foes apart. It gains temporary HP equal to its level, and a +4 damage bonus to its unarmed strike attacks, " +
                                "but it takes a -2 penalty to AC. The rage lasts until the end of the encounter, and leaves your eidolon fatigued if they leave early.", (Target)Target.Self())
                                .WithActionCost(1)
                                .WithSoundEffect(SfxName.BeastRoar)
                                .WithEffectOnSelf((async (self) => {
                                    self.GainTemporaryHP(self.Level);
                                    self.AddQEffect(new QEffect("Seething Frenzy", "You take a -2 penalty to AC, but your strikes deal +4 bonus damage.") {
                                        Illustration = illSeethingFrenzy,
                                        Id = qfSeethingFrenzy,
                                        BonusToDefenses = (effect, action, defence) => {
                                            if (defence == Defense.AC) {
                                                return new Bonus(-2, BonusType.Untyped, "Seething Frenzy");
                                            }
                                            return null;
                                        },
                                        BonusToDamage = (effect, action, target) => {
                                            if (action.Name.StartsWith("Strike (") && action.HasTrait(Trait.Unarmed)) {
                                                return new Bonus(4, BonusType.Untyped, "Seething Frenzy");
                                            }
                                            return null;
                                        },
                                        WhenExpires = effect => {
                                            effect.Owner.AddQEffect(QEffect.Fatigued());
                                        }
                                    });
                                }))
                            );
                            output.PossibilitySize = PossibilitySize.Half;
                            return output;
                        }
                    });
                }
            }

            foreach (EvolutionFeat feat in evoFeats) {
                if (feat.EffectOnEidolon != null) {
                    feat.EffectOnEidolon.Invoke(eidolon);
                }
            }

            if (eidolon.Level >= 7) {
                eidolon.AddQEffect(new QEffect("Weapon Specialization", "+2 weapon damage.") {
                    BonusToDamage = ((self, action, target) => {
                        if (action.Item == null)
                            return (Bonus)null;
                        Proficiency proficiency = action.Owner.Proficiencies.Get(action.Item.Traits);
                        return proficiency >= Proficiency.Expert ? new Bonus(proficiency == Proficiency.Expert ? 2 : (proficiency == Proficiency.Master ? 3 : (proficiency == Proficiency.Legendary ? 4 : 0)), BonusType.Untyped, "Weapon specialization") : (Bonus)null;
                    })
                });
            }

            //List<Item> wornItems = summoner.CarriedItems.Where(item => item.IsWorn == true && item.HasTrait(Trait.Invested) && item.PermanentQEffectActionWhenWorn != null).ToList<Item>();


            eidolon.PostConstructorInitialization(TBattle.Pseudobattle);
            return eidolon;
        }

        private static Creature CreateEidolonBase(string name, Creature summoner, int[] abilityScores, int ac, int dexCap) {
            int strength = abilityScores[0];
            int dexterity = abilityScores[1];
            int constitution = abilityScores[2];
            int intelligence = abilityScores[3];
            int wisdom = abilityScores[4];
            int charisma = abilityScores[5];
            int level = summoner.Level;
            int trained = 2 + level;
            int expert = trained + 2;
            int master = expert + 2;
            Abilities abilities1 = new Abilities(strength, dexterity, constitution, intelligence, wisdom, charisma);
            Illustration illustration1 = summoner.PersistentCharacterSheet.Calculated.AllFeats.FirstOrDefault(ft => ft.HasTrait(tPortrait)).Illustration;
            string name1 = name;
            List<Trait> alignment = summoner.PersistentCharacterSheet.Calculated.AllFeats.FirstOrDefault((Func<Feat, bool>)(ft => ft.HasTrait(tAlignment))).Traits;
            List<Trait> traits = new List<Trait>();
            traits.Add(tEidolon);
            for (int i = 1; i < alignment.Count; i++) {
                traits.Add(alignment[i]);
            }
            int perception = wisdom + (summoner.Proficiencies.AllProficiencies[Trait.Perception] == Proficiency.Trained ? trained : expert) + level;
            int speed1 = 5;
            Defenses defenses = new Defenses(10 + ac + (dexterity < dexCap ? dexterity : dexCap) + (level >= 11 ? expert : trained), constitution + (level >= 11 ? master : expert), dexterity + (level >= 9 ? expert : trained), wisdom + (level >= 15 ? master : expert));
            int hp = summoner.MaxHP;
            Skills skills = summoner.Skills;
            Abilities abilities2 = abilities1;

            return new Creature(illustration1, name1, (IList<Trait>)traits, level, perception, speed1, defenses, hp, abilities2, skills)
                .WithProficiency(Trait.Unarmed, (level >= 5 ? (level >= 13 ? Proficiency.Master : Proficiency.Expert) : Proficiency.Trained))
                .WithProficiency(Trait.UnarmoredDefense, (level >= 11 ? (level >= 19 ? Proficiency.Master : Proficiency.Expert) : Proficiency.Trained))
                .WithEntersInitiativeOrder(false)
                //.WithSpellProficiencyBasedOnSpellAttack(summoner.ClassOrSpellDC() - 10, abilities1.Strength >= abilities1.Dexterity ? Ability.Strength : Ability.Dexterity)
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
                    PreventTakingAction = action => {
                        if (action.ActionId == ActionId.Delay) {
                            return "Your eidolon cannot take this action.";
                        }
                        return null;
                    },
                    PreventTargetingBy = action => {
                        if (action.SpellId == SpellId.Dominate) {
                            return "Eidolons cannot be dominated.";
                        }
                        return null;
                    },
                    StateCheckWithVisibleChanges = (async qf => {
                        Creature summoner = GetSummoner(qf.Owner);

                        // Handle drained mirror addition
                        QEffect drained = qf.Owner.FindQEffect(QEffectId.Drained);
                        if (drained != null) {
                            summoner.AddQEffect(new QEffect() {
                                Id = qfDrainedMirror,
                                Key = "DrainedMirror",
                                StateCheck = drained.StateCheck,
                                Value = drained.Value,
                            });
                        } else if (qf.Owner.HasEffect(qfDrainedMirror) && summoner.HasEffect(QEffectId.Drained) == false) {
                            qf.Owner.RemoveAllQEffects(effect => effect.Id == qfDrainedMirror);
                        }

                        // PAST THIS POINT, INACTIVE EIDOLON NOT AFFECTED
                        if (qf.Owner.Destroyed == true) {
                            return;
                        }

                        if (summoner.OwningFaction != qf.Owner.OwningFaction) {
                            qf.Owner.OwningFaction = summoner.OwningFaction;
                        }

                        // Reaction
                        if (qf.Owner.Actions.IsReactionUsedUp == true) {
                            summoner.Actions.UseUpReaction();
                        }

                        // Handle handwraps
                        if (summoner.FindQEffect(qfInvestedWeapon) != null) {
                            QEffect investedWeaponQf = summoner.FindQEffect(qfInvestedWeapon);
                            Item investedWeapon = investedWeaponQf.Tag as Item;
                            Item handwraps = qf.Owner.CarriedItems.FirstOrDefault(item => item.ItemName == ItemName.HandwrapsOfMightyBlows && item.Name == investedWeapon.Name);
                            if (investedWeapon.ItemName != ItemName.HandwrapsOfMightyBlows && summoner.HeldItems.FirstOrDefault(item => item == investedWeapon) == null) {
                                handwraps.IsWorn = false;
                            } else {
                                handwraps.IsWorn = true;
                            }
                        }

                        // Handle AoO
                        if (qf.Owner.HasEffect(qfReactiveStrikeCheck)) {
                            qf.Owner.RemoveAllQEffects(qf => qf.Id == qfReactiveStrikeCheck);
                            HPShareEffect shareHP = (HPShareEffect)qf.Owner.QEffects.FirstOrDefault<QEffect>((Func<QEffect, bool>)(qf => qf.Id == qfSummonerBond));
                            await HandleHealthShare(qf.Owner, summoner, shareHP.LoggedAction, SummonerClassEnums.InterceptKind.TARGET);
                        }
                        if (summoner.HasEffect(qfReactiveStrikeCheck)) {
                            summoner.RemoveAllQEffects(qf => qf.Id == qfReactiveStrikeCheck);
                            HPShareEffect shareHP = (HPShareEffect)summoner.QEffects.FirstOrDefault<QEffect>((Func<QEffect, bool>)(qf => qf.Id == qfSummonerBond));
                            await HandleHealthShare(summoner, qf.Owner, shareHP.LoggedAction, SummonerClassEnums.InterceptKind.TARGET);
                        }

                        // Handle tempHP
                        if (qf.Owner.TemporaryHP < summoner.TemporaryHP) {
                            qf.Owner.GainTemporaryHP(summoner.TemporaryHP);
                        } else if (qf.Owner.TemporaryHP > summoner.TemporaryHP) {
                            summoner.GainTemporaryHP(qf.Owner.TemporaryHP);
                        }

                        if (qf.Owner.HP < summoner.HP) {
                            qf.Owner.Heal($"{summoner.HP - qf.Owner.HP}", null);
                        } else if (qf.Owner.HP > summoner.HP) {
                            summoner.Heal($"{qf.Owner.HP - summoner.HP}", null);
                        }
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

                        shareHP.LogAction(qfHealOrHarm.Owner, action, action.Owner, SummonerClassEnums.InterceptKind.TARGET);
                        shareHP.Owner.AddQEffect(new QEffect() { Id = qfReactiveStrikeCheck });
                    }),
                    BonusToSpellSaveDCs = qf => {
                        int sDC = GetSummoner(qf.Owner).ClassOrSpellDC();
                        int eDC = qf.Owner.ClassOrSpellDC();
                        return new Bonus(eDC >= sDC ? sDC - eDC : eDC - sDC, BonusType.Untyped, "Summoner Spellcasting DC");
                    },
                    BonusToAttackRolls = (qf, action, target) => {
                        if (action.HasTrait(Trait.Spell)) {
                            int sDC = GetSummoner(qf.Owner).ClassOrSpellDC();
                            int eDC = qf.Owner.ClassOrSpellDC();
                            return new Bonus(eDC >= sDC ? sDC - eDC : eDC - sDC, BonusType.Untyped, "Summoner Spellcasting Attack Bonus");
                        }
                        return null;
                    },
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

                        shareHP.LogAction(qfPreHazardDamage.Owner, damageStuff.Power, attacker, SummonerClassEnums.InterceptKind.DAMAGE);
                        return null;

                        //if (attacker == qfPreHazardDamage.Owner.Battle.Pseudocreature) {
                        //    qfPreHazardDamage.Owner.Battle.Log("{b}HAZARD DAMAGE LOGGED{/b}");
                        //}
                        //return null;
                    }),
                    AfterYouTakeDamageOfKind = (async (qfPostHazardDamage, action, kind) => {
                        Creature summoner = GetSummoner(qfPostHazardDamage.Owner);
                        Creature eidolon = qfPostHazardDamage.Owner;

                        await HandleHealthShare(eidolon, summoner, action, SummonerClassEnums.InterceptKind.DAMAGE);
                    }),
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

                        Item? handwraps = StrikeRules.GetBestHandwraps(action.Owner);
                        if (handwraps != null) {
                            return new Bonus(handwraps.WeaponProperties.ItemBonus, BonusType.Item, handwraps.Name);
                        }
                        return null;
                    }),
                    AfterYouTakeAction = (Func<QEffect, CombatAction, Task>)(async (qf, action) => {
                        Creature eidolon = qf.Owner;

                        // Focus points
                        if (action.HasTrait(Trait.Focus)) {
                            summoner.Spellcasting.FocusPoints = eidolon.Spellcasting.FocusPoints;
                        }

                        // MAP
                        if (summoner != null && action.Traits.Contains(Trait.Attack)) {
                            summoner.Actions.AttackedThisManyTimesThisTurn = eidolon.Actions.AttackedThisManyTimesThisTurn;
                        }
                    })
                });
        }

        private async static Task PartnerActs(Creature self, Creature partner) {
            await PartnerActs(self, partner, false, null);
        }

        private async static Task PartnerActs(Creature self, Creature partner, bool tandem, Func<CombatAction, string?>? limitations) {
            QEffect limitationEffect = null;
            if (limitations != null) {
                limitationEffect = new QEffect() {
                    PreventTakingAction = limitations
                };

                partner.AddQEffect(limitationEffect);
            }

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
                Sfxs.Play(SfxName.StartOfTurn, 0.2f);
            await partner.Battle.GameLoop.StateCheck();
            // Process partner's turn
            // Handle tandem attack
            Creature? tandemAttackTarget = partner.QEffects.FirstOrDefault(qf => qf.Id == qfActTogether) != null ? (Creature) partner.QEffects.FirstOrDefault(qf => qf.Id == qfActTogether).Tag : null;
            if (tandemAttackTarget != null) {
                List<Option> options = new List<Option>();
                List<Item> weapons = partner.HeldItems.Where(item => item.WeaponProperties != null).ToList();
                List<QEffect> additionalAttackEffects = partner.QEffects.Where(qf => qf.AdditionalUnarmedStrike != null).ToList();
                List<Item> additionalAttacks = new List<Item>();
                foreach (QEffect qf in additionalAttackEffects) {
                    additionalAttacks.Add(qf.AdditionalUnarmedStrike);
                }
                List<Item> strikes = new List<Item>().Concat(additionalAttacks).Concat(weapons).ToList();
                strikes.Add(partner.UnarmedStrike);
                foreach (Item obj in strikes) {
                    CombatAction strike = partner.CreateStrike(obj, partner.Actions.AttackedThisManyTimesThisTurn - 1);
                    strike.WithActionCost(0);
                    CreatureTarget targeting = (CreatureTarget) strike.Target;
                    if ((bool)targeting.IsLegalTarget(partner, tandemAttackTarget)) {
                        Option option = Option.ChooseCreature(strike.Name, tandemAttackTarget, async delegate {
                            await partner.Battle.GameLoop.FullCast(strike, new ChosenTargets {
                                ChosenCreature = tandemAttackTarget,
                                ChosenCreatures = { tandemAttackTarget }
                            });
                        }, -2.14748365E+09f).WithIllustration(strike.Illustration);
                        string text = strike.TooltipCreator?.Invoke(strike, tandemAttackTarget, 0);
                        if (text != null) {
                            option.WithTooltip(text);
                        } else if (strike.ActiveRollSpecification != null) {
                            option.WithTooltip(CombatActionExecution.BreakdownAttack(strike, tandemAttackTarget).TooltipDescription);
                        } else if (strike.SavingThrow != null && (strike.ExcludeTargetFromSavingThrow == null || !strike.ExcludeTargetFromSavingThrow(strike, tandemAttackTarget))) {
                            option.WithTooltip(CombatActionExecution.BreakdownSavingThrow(strike, tandemAttackTarget, strike.SavingThrow).TooltipDescription);
                        } else {
                            option.WithTooltip(strike.Description);
                        }
                        option.NoConfirmation = true;
                        options.Add(option);
                    }
                }
                if (options.Count > 0) {
                    Option chosenOption;
                    if (options.Count >= 2) {
                        options.Add((Option)new CancelOption(true));
                        chosenOption = (await partner.Battle.SendRequest(new AdvancedRequest(partner, "Choose a creature to Strike.", options) {
                            TopBarText = $"Choose a creature to Strike or right-click to cancel.",
                            TopBarIcon = illTandemStrike
                        })).ChosenOption;
                    } else
                        chosenOption = options[0];

                    if (chosenOption is CancelOption) {
                        return;
                    }
                    await chosenOption.Action();
                }
            } else {
                // Run regular turn
                await (Task)partner.Battle.GameLoop.GetType().GetMethod("SubActionPhase", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                    .Invoke(partner.Battle.GameLoop, new object[] { partner });
            }
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
            if (limitations != null) {
                partner.RemoveAllQEffects(qf => qf == limitationEffect);
            }
            oldActiveCreature = (Creature)null;
        }

        private async static Task HandleHealthShare(Creature self, Creature partner, CombatAction action, SummonerClassEnums.InterceptKind interceptKind) {
            HPShareEffect selfShareHP = (HPShareEffect)self.QEffects.FirstOrDefault<QEffect>((Func<QEffect, bool>)(qf => qf.Id == qfSummonerBond && qf.Source == partner));
            HPShareEffect partnerShareHP = (HPShareEffect)partner.QEffects.FirstOrDefault<QEffect>((Func<QEffect, bool>)(qf => qf.Id == qfSummonerBond && qf.Source == self));

            // Remove target check
            if (interceptKind == SummonerClassEnums.InterceptKind.TARGET) {
                self.RemoveAllQEffects(qf => qf.Id == qfReactiveStrikeCheck);
            }

            if (selfShareHP.Type != interceptKind) {
                return;
            }

            if (action == selfShareHP.CA || action == partnerShareHP.CA) {
                return;
            }

            int totalHPSelf = self.HP + self.TemporaryHP;
            int totalHPPartner = partner.HP + partner.TemporaryHP;

            if (partnerShareHP.CompareEffects(selfShareHP)) {
                // Same effect
                if (selfShareHP.HealOrHarm(self) == SummonerClassEnums.EffectKind.HARM) {
                    if (totalHPPartner < totalHPSelf) {
                        int damage = totalHPSelf - totalHPPartner;
                        await partner.DealDirectDamage(partnerShareHP.CA, DiceFormula.FromText($"{damage}"), self, CheckResult.Success, DamageKind.Untyped);
                    } else if (totalHPPartner > totalHPSelf) {
                        int damage = totalHPPartner - totalHPSelf;
                        await self.DealDirectDamage(selfShareHP.CA, DiceFormula.FromText($"{damage}"), partner, CheckResult.Success, DamageKind.Untyped);
                    }
                } else if (selfShareHP.HealOrHarm(self) == SummonerClassEnums.EffectKind.HEAL) {
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
                if (selfShareHP.HealOrHarm(self) == SummonerClassEnums.EffectKind.HARM) {
                    int damage = totalHPPartner - totalHPSelf;
                    await self.DealDirectDamage(selfShareHP.CA, DiceFormula.FromText($"{damage}"), partner, CheckResult.Success, DamageKind.Untyped);
                } else if (selfShareHP.HealOrHarm(self) == SummonerClassEnums.EffectKind.HEAL) {
                    int healing = self.HP - partner.HP;
                    partner.Heal($"{healing}", selfShareHP.CA);
                }
            }
        }

        private static Possibility? GenerateTandemStrikeAction(Creature self, Creature partner, Creature summoner) {
            if (partner == null || !partner.Actions.CanTakeActions() || self.QEffects.FirstOrDefault(qf => qf.Id == qfActTogether) != null)
                return (Possibility)null;
            if (self.QEffects.FirstOrDefault(qf => qf.Id == qfActTogetherToggle) != null) {
                Possibility output = (Possibility)(ActionPossibility)new CombatAction(self, illTandemStrike, "Cancel Tandem Strike",
                new Trait[] { tSummoner, tTandem }, $"Cancel tandem strike toggle.", (Target)Target.Self())
                .WithActionCost(0).WithEffectOnSelf((Action<Creature>)(self => {
                    // Remove toggle from self
                    self.RemoveAllQEffects(qf => qf.Id == qfActTogetherToggle);
                }));

                //output.WithPossibilityGroup("Tandem Actions");
                return output;
            } else {
                Possibility tandemStrike = (Possibility)(ActionPossibility)new CombatAction(self, illTandemStrike, "Enable Tandem Strike",
                new Trait[] { tSummoner, tTandem },
                "{b}Frequency: {/b} once per round\n\n" + (self == summoner ? "Your" : "Your eidolon's") + " next strike action grants " + (self == summoner ? "your eidolon" : "you") + " an immediate bonus tandem turn, where " + (self == summoner ? "they" : "you") + " they can make a single strike action.",
                (Target)Target.Self()) {
                    ShortDescription = (self == summoner ? "Your" : "Your eidolon's") + " next strike action grants " + (self == summoner ? "your eidolon" : "you") + " an immediate bonus tandem turn, where " + (self == summoner ? "they" : "you") + " they can make a single strike action."
                }
                    .WithActionCost(0)
                    .WithEffectOnSelf((Action<Creature>)(self => {
                        // Give toggle qf to self
                        self.AddQEffect(new QEffect("Tandem Strike Toggled", "Your next strike action cost will also grant a free strike action to your bonded partner.") {
                            Id = qfActTogetherToggle,
                            ExpiresAt = ExpirationCondition.ExpiresAtEndOfYourTurn,
                            Illustration = illTandemStrike,
                            PreventTakingAction = (a => {
                                if (!a.Name.StartsWith("Strike (") && a.Name != "Cancel Tandem Strike") {
                                    return "Tandem strike can only be activated by the strike action.";
                                }
                                return null;
                            }),
                            AfterYouTakeAction = (Func<QEffect, CombatAction, Task>)(async (qf, action) => {
                                if (action.Name.StartsWith("Strike (")) {
                                    self.RemoveAllQEffects(qf => qf.Id == qfActTogetherToggle);
                                    self.AddQEffect(new QEffect {
                                        PreventTakingAction = action => action.Name == "Enable Tandem Strike" ? "Tandem strike already used this round" : null,
                                        ExpiresAt = ExpirationCondition.ExpiresAtStartOfYourTurn
                                    });
                                    partner.AddQEffect(new QEffect {
                                        PreventTakingAction = action => action.Name == "Enable Tandem Strike" ? "Tandem strike already used this round" : null,
                                        ExpiresAt = ExpirationCondition.ExpiresAtStartOfYourTurn
                                    });
                                    QEffect actTogether = new QEffect("Tandem Strike", "Immediately take a single strike action.") {
                                        Illustration = IllustrationName.Haste,
                                        Id = qfActTogether,
                                    };
                                    partner.AddQEffect(actTogether);
                                    actTogether.Tag = action.ChosenTargets.ChosenCreature;
                                    await PartnerActs(self, partner, true, (a => {
                                        if (!a.Name.StartsWith("Strike (")) {
                                            return "Only the strike action is allowed during a tandem strike turn.";
                                        }
                                        return null;
                                    }));
                                    partner.RemoveAllQEffects(effect => effect == actTogether);
                                }
                            })
                        });
                    }));
                //tandemStrike.WithPossibilityGroup("Tandem Actions");
                return tandemStrike;
            }
        }

        private static Possibility? GenerateTandemMovementAction(Creature self, Creature partner, Creature summoner) {
            if (partner == null || !partner.Actions.CanTakeActions() || self.QEffects.FirstOrDefault(qf => qf.Id == qfActTogether) != null)
                return (Possibility)null;
            if (self.QEffects.FirstOrDefault(qf => qf.Id == qfActTogetherToggle) != null) {
                Possibility output = (Possibility)(ActionPossibility)new CombatAction(self, illTandemMovement, "Cancel Tandem Movement",
                new Trait[] { tSummoner, tTandem }, $"Cancel tandem movement toggle.", (Target)Target.Self())
                    .WithActionCost(0).WithEffectOnSelf((Action<Creature>)(self => {
                    // Remove toggle from self
                    self.RemoveAllQEffects(qf => qf.Id == qfActTogetherToggle);
                }));

                //output.WithPossibilityGroup("Tandem Actions");
                return output;
            } else {
                Possibility tandemMove = (Possibility)(ActionPossibility)new CombatAction(self, illTandemMovement, "Enable Tandem Movement",
                new Trait[] { tSummoner, tTandem },
                "{b}Frequency: {/b} once per round\n\n" + (self == summoner ? "Your" : "Your eidolon's") + " next stride action grants " + (self == summoner ? "your eidolon" : "you") + " an immediate bonus tandem turn, where " + (self == summoner ? "they" : "you") + " they can make a single stride action.",
                (Target)Target.Self()) {
                    ShortDescription = (self == summoner ? "Your" : "Your eidolon's") + " next stride action grants " + (self == summoner ? "your eidolon" : "you") + " an immediate bonus tandem turn, where " + (self == summoner ? "they" : "you") + " they can make a single stride action."
                }
                    .WithActionCost(0)
                    .WithEffectOnSelf((Action<Creature>)(self => {
                        // Give toggle qf to self
                        self.AddQEffect(new QEffect("Tandem Movement Toggled", "Your next stride action cost will also grant a free stride action to your bonded partner.") {
                            Id = qfActTogetherToggle,
                            ExpiresAt = ExpirationCondition.ExpiresAtEndOfYourTurn,
                            Illustration = illTandemMovement,
                            PreventTakingAction = (a => {
                                if (a.ActionId != ActionId.Stride && a.Name != "Cancel Tandem Movement") {
                                    return "Tandem movement can only be activated by the stride action.";
                                }
                                return null;
                            }),
                            AfterYouTakeAction = (Func<QEffect, CombatAction, Task>)(async (qf, action) => {
                                if (action.ActionId == ActionId.Stride) {
                                    self.RemoveAllQEffects(qf => qf.Id == qfActTogetherToggle);
                                    self.AddQEffect(new QEffect {
                                        PreventTakingAction = action => action.Name == "Enable Tandem Movement" ? "Tandem movement already used this round" : null,
                                        ExpiresAt = ExpirationCondition.ExpiresAtStartOfYourTurn
                                    });
                                    partner.AddQEffect(new QEffect {
                                        PreventTakingAction = action => action.Name == "Enable Tandem Movement" ? "Tandem movement already used this round" : null,
                                        ExpiresAt = ExpirationCondition.ExpiresAtStartOfYourTurn
                                    });
                                    QEffect actTogether = new QEffect("Tandem Movement", "Immediately take a single stride action.") {
                                        Illustration = IllustrationName.Haste,
                                        Id = qfActTogether,
                                    };
                                    partner.AddQEffect(actTogether);
                                    await PartnerActs(self, partner, true, (a => {
                                        if (a.ActionId != ActionId.Stride) {
                                            return "Only the stride action is allowed during a tandem movement turn.";
                                        }
                                        return null;
                                    }));
                                    partner.RemoveAllQEffects(effect => effect == actTogether);
                                }
                            })
                        });
                    }));
                    //tandemMove.WithPossibilityGroup("Tandem Actions");
                    return tandemMove;
            }
        }

        private static Possibility? GenerateActTogetherAction(Creature self, Creature partner, Creature summoner) {
            if (partner == null || !partner.Actions.CanTakeActions() || self.QEffects.FirstOrDefault(qf => qf.Id == qfActTogether) != null)
                return (Possibility)null;
            if (self.QEffects.FirstOrDefault(qf => qf.Id == qfActTogetherToggle) != null) {
                Possibility output = (Possibility)(ActionPossibility)new CombatAction(self, illActTogether, "Cancel Act Together",
                new Trait[] { tSummoner, tTandem }, $"Cancel act together toggle.", (Target)Target.Self())
                .WithActionCost(0).WithEffectOnSelf((Action<Creature>)(self => {
                    // Remove toggle from self
                    self.RemoveAllQEffects(qf => qf.Id == qfActTogetherToggle);
                }));

                //output.WithPossibilityGroup("Tandem Actions");
                return output;
            } else {
                Possibility actTogether = (Possibility)(ActionPossibility)new CombatAction(self, illActTogether, "Enable Act Together",
                new Trait[] { tSummoner, tTandem },
                "{b}Frequency: {/b} once per round\n\n" + (self == summoner ? "Your" : "Your eidolon's") + " next action grants " + (self == summoner ? "your eidolon" : "you") + " an immediate bonus tandem turn, where " + (self == summoner ? "they" : "you") + " they can make a single action.",
                (Target)Target.Self()) {
                    ShortDescription = (self == summoner ? "Your" : "Your eidolon's") + " next action grants " + (self == summoner ? "your eidolon" : "you") + " an immediate bonus tandem turn, where " + (self == summoner ? "they" : "you") + " they can make a single action."
                }
                    .WithActionCost(0)
                    .WithEffectOnSelf((Action<Creature>)(self => {
                    // Give toggle qf to self
                    self.AddQEffect(new QEffect("Act Together Toggled", "Your next action of 1+ cost will also grant a single quickened action to your bonded partner.") {
                        Id = qfActTogetherToggle,
                        ExpiresAt = ExpirationCondition.ExpiresAtEndOfYourTurn,
                        Illustration = illActTogetherStatus,
                        AfterYouTakeAction = (Func<QEffect, CombatAction, Task>)(async (qf, action) => {
                            if (action.ActionCost != 0) {
                                self.RemoveAllQEffects(qf => qf.Id == qfActTogetherToggle);
                                self.AddQEffect(new QEffect {
                                    PreventTakingAction = action => action.Name == "Enable Act Together" ? "Act together already used this round" : null,
                                    ExpiresAt = ExpirationCondition.ExpiresAtStartOfYourTurn
                                });
                                partner.AddQEffect(new QEffect {
                                    PreventTakingAction = action => action.Name == "Enable Act Together" ? "Act together already used this round" : null,
                                    ExpiresAt = ExpirationCondition.ExpiresAtStartOfYourTurn
                                });
                                QEffect actTogether = new QEffect("Act Together", "Immediately take a single 1 cost action.") {
                                    Illustration = IllustrationName.Haste,
                                    Id = qfActTogether,
                                };
                                partner.AddQEffect(actTogether);

                                await PartnerActs(self, partner, true, null);
                                partner.RemoveAllQEffects(effect => effect == actTogether);
                            }
                        })
                    });
                }));
                //actTogether.WithPossibilityGroup("Tandem Actions");
                return actTogether;
            }
        }

        
        private static string DirToFeatName(string text, out Trait category) {
            category = Trait.None;
            
            // Trim directions
            if (text.Contains("SummonerAssets/EidolonPortraits/ConvertedBaseGameAssets/"))
                text = text.Substring(32 + 24);
            else
                text = text.Substring(32);

            // Trim category
            if (text.StartsWith("Beast")) {
                category = Trait.Beast;
                text = text.Substring("Beast".Length + 1);
            } else if (text.StartsWith("Construct")) {
                category = Trait.Construct;
                text = text.Substring("Construct".Length + 1);
            } else if (text.StartsWith("Dragon")) {
                category = Trait.Dragon;
                text = text.Substring("Dragon".Length + 1);
            } else if (text.StartsWith("Elemental")) {
                category = Trait.Elemental;
                text = text.Substring("Elemental".Length + 1);
            } else if (text.StartsWith("Humanoid")) {
                category = Trait.Humanoid;
                text = text.Substring("Humanoid".Length + 1);
            } else if (text.StartsWith("Outsider")) {
                category = tOutsider;
                text = text.Substring("Outsider".Length + 1);
            } else if (text.StartsWith("Undead")) {
                category = Trait.Undead;
                text = text.Substring("Undead".Length + 1);
            }

            // From Github User Binary Worrier: https://stackoverflow.com/a/272929
            if (string.IsNullOrWhiteSpace(text))
                return "";
            StringBuilder newText = new StringBuilder(text.Length * 2);
            newText.Append(text[0]);
            for (int i = 1; i < text.Length; i++) {
                if (char.IsUpper(text[i]) && text[i - 1] != ' ')
                    newText.Append(' ');
                newText.Append(text[i]);
            }
            string output = newText.ToString();
            // This part was added on by me
            if (output.EndsWith(".png")) {
                output = output.Substring(0, output.Length - 4);
            }
            if (output.EndsWith("256")) {
                output = output.Substring(0, output.Length - 3);
            }
            return output;
        }

        internal static async Task<Tile> GetChargeTiles(Creature self, MovementStyle movementStyle, int minimumDistance, string msg, Illustration img) {
            Tile startingPos = self.Occupies;
            Vector2 pos = self.Occupies.ToCenterVector();
            List<Option> options = new List<Option>();
            Dictionary<string, Tile> pairs = new Dictionary<string, Tile>();

            PathfindingDescription pathfindingDescription = new PathfindingDescription() {
                Squares = movementStyle.MaximumSquares,
                Style = movementStyle
            };

            IList<Tile>? tiles = (IList<Tile>?)(typeof(ModManager).Assembly.GetType("Dawnsbury.Core.Intelligence.Pathfinding").GetMethod("Floodfill", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static)
                    .Invoke(null, [self, self.Battle, pathfindingDescription]));

            if (tiles == null) {
                self.Occupies.Overhead("cannot move", Color.White);
                return null;
            }

            movementStyle.MaximumSquares *= 5;
            movementStyle.ShortestPath = true;

            // Compile destination tiles
            foreach (Tile tile in tiles) {
                // Check if valid tile
                if (!tile.IsFree) {
                    continue;
                }

                // Handle minimum distance
                if (self.DistanceTo(tile) < minimumDistance) {
                    continue;
                }

                // Handle LoS
                if (new CoverKind[] { CoverKind.Greater, CoverKind.Blocked, CoverKind.Standard }.Contains(self.HasLineOfEffectTo(tile))) {
                    continue;
                }

                // Add tile as option
                // TODO: Add AI
                CombatAction movement = new CombatAction(self, illBeastsCharge, "Beast's Charge", new Trait[] { Trait.Move }, "", Target.Tile((cr, t) => t.LooksFreeTo(cr), (cr, t) => (float)int.MinValue)
                    .WithPathfindingGuidelines((cr => pathfindingDescription))
                )
                .WithActionCost(0)
                //.WithEffectOnChosenTargets(async (action, self2, targets) => await self2.MoveToUsingEarlierFloodfill(targets.ChosenTile, action, movementStyle))
                //.WithEffectOnChosenTargets(async (action, self2, targets) => await self2.MoveTo(targets.ChosenTile, action, movementStyle))
                ;
                options.Add(movement.CreateUseOptionOn(tile));
                pairs.Add(options.Last().ToString(), tile);
            }

            // Adds a Cancel Option
            options.Add(new CancelOption(true));

            // Prompts the user for their desired tile and returns it or null
            Option selectedOption = (await self.Battle.SendRequest(new AdvancedRequest(self, msg, options) {
                IsMainTurn = false,
                IsStandardMovementRequest = true,
                TopBarIcon = img,
                TopBarText = msg
            })).ChosenOption;

            if (selectedOption != null) {
                if (selectedOption is CancelOption cancel) {
                    return null;
                }

                return pairs[selectedOption.ToString()];
            }

            return null;
        }

        private static AreaSelection? DetermineTilesCopy(Creature caster, BurstAreaTarget burstAreaTarget, Vector2 burstOrigin, bool ignoreBurstOriginLoS = false) {
            Vector2 vector2 = burstOrigin;
            Microsoft.Xna.Framework.Point point = new Microsoft.Xna.Framework.Point((int) caster.Occupies.X, (int)caster.Occupies.X);
            Coverlines coverlines = caster.Battle.Map.Coverlines;
            bool flag1 = true;
            for (int targetCorner = 0; targetCorner < 4; ++targetCorner) {
                Point corner = Coverlines.CreateCorner(point.X, point.Y, targetCorner);
                if (!coverlines.GetCorner(corner.X, corner.Y, (int)burstOrigin.X, (int)burstOrigin.Y)) {
                    flag1 = false;
                    break;
                }
            }
            if (flag1 & ignoreBurstOriginLoS)
                return (AreaSelection)null;
            AreaSelection tiles = new AreaSelection();
            foreach (Tile allTile in caster.Battle.Map.AllTiles) {
                Vector2 centerVector = allTile.ToCenterVector();
                if ((double)DistanceBetweenCenters(vector2, centerVector) <= (double)burstAreaTarget.Radius) {
                    bool flag2 = false;
                    for (int targetCorner = 0; targetCorner < 4; ++targetCorner) {
                        Microsoft.Xna.Framework.Point corner = Coverlines.CreateCorner(allTile.X, allTile.Y, targetCorner);
                        if (!coverlines.GetCorner((int)burstOrigin.X, (int)burstOrigin.Y, corner.X, corner.Y)) {
                            if (!allTile.AlwaysBlocksLineOfEffect) {
                                flag2 = true;
                                break;
                            }
                            break;
                        }
                    }
                    if (flag2)
                        tiles.TargetedTiles.Add(allTile);
                    else
                        tiles.ExcludedTiles.Add(allTile);
                }
            }
            return tiles;
        }

        private static float DistanceBetweenCenters(Vector2 pointOne, Vector2 pointTwo) {
            float num = Math.Abs(pointOne.X - pointTwo.X);
            float num2 = Math.Abs(pointOne.Y - pointTwo.Y);
            if (num >= num2) {
                return num + num2 / 2f;
            }

            return num2 + num / 2f;
        }
    }
}

//Yes, indeed.  To prevent an action from showing up on the stat block, add the trait Trait.Basic to its list of trait. Not too intuitive, sorry.