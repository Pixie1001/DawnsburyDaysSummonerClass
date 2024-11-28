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
using Dawnsbury.Modding;
using Dawnsbury.Core.Mechanics;
using Microsoft.Xna.Framework.Graphics;
using static System.Collections.Specialized.BitVector32;
using System.Reflection.Metadata.Ecma335;
using static System.Runtime.InteropServices.JavaScript.JSType;
using Microsoft.VisualBasic;
using static System.Net.Mime.MediaTypeNames;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Data;

// TODO: Setup to remove handwrap transfer entirely, in case a future patch breaks them, and instead just add runes based on invested weapon

namespace Dawnsbury.Mods.Classes.Summoner {
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    internal static class UtilityFuncs {

        public static Skill? Trait2Skill(Trait skill) {
            Skill? skill1;
            switch (skill) {
                case Trait.Acrobatics:
                    skill1 = new Skill?(Skill.Acrobatics);
                    break;
                case Trait.Arcana:
                    skill1 = new Skill?(Skill.Arcana);
                    break;
                case Trait.Athletics:
                    skill1 = new Skill?(Skill.Athletics);
                    break;
                case Trait.Crafting:
                    skill1 = new Skill?(Skill.Crafting);
                    break;
                case Trait.Deception:
                    skill1 = new Skill?(Skill.Deception);
                    break;
                case Trait.Diplomacy:
                    skill1 = new Skill?(Skill.Diplomacy);
                    break;
                case Trait.Intimidation:
                    skill1 = new Skill?(Skill.Intimidation);
                    break;
                case Trait.Medicine:
                    skill1 = new Skill?(Skill.Medicine);
                    break;
                case Trait.Nature:
                    skill1 = new Skill?(Skill.Nature);
                    break;
                case Trait.Occultism:
                    skill1 = new Skill?(Skill.Occultism);
                    break;
                case Trait.Performance:
                    skill1 = new Skill?(Skill.Performance);
                    break;
                case Trait.Religion:
                    skill1 = new Skill?(Skill.Religion);
                    break;
                case Trait.Society:
                    skill1 = new Skill?(Skill.Society);
                    break;
                case Trait.Stealth:
                    skill1 = new Skill?(Skill.Stealth);
                    break;
                case Trait.Survival:
                    skill1 = new Skill?(Skill.Survival);
                    break;
                case Trait.Thievery:
                    skill1 = new Skill?(Skill.Thievery);
                    break;
                default:
                    skill1 = new Skill?();
                    break;
            }
            return skill1;
        }
    }
}

