using System;
using System.Collections.Generic;
using DOL.Database;
using DOL.GS.Keeps;
using DOL.GS.Spells;

namespace DOL.GS.Mimic
{
    internal static class MimicLoadoutBuilder
    {
        private enum SupportArchetype
        {
            Albion,
            Midgard,
            Hibernia
        }

        private const int RestorationSpellId = 950010;
        private const int ResurgenceSpellId = 950011;
        private const int RenewalSpellId = 950012;
        private const int BulwarkSpellId = 950013;
        private const int SerenitySpellId = 950014;
        private const int SmiteSpellId = 950020;

        private static readonly Dictionary<string, Spell> _baseSpells = new();

        static MimicLoadoutBuilder()
        {
            ClothingMgr.LoadTemplates();
        }

        public static void Configure(MimicNPC mimic)
        {
            switch (mimic.Template.CharacterClass)
            {
                case eCharacterClass.Cleric:
                    ApplySupportLoadout(mimic, SupportArchetype.Albion);
                    break;
                case eCharacterClass.Healer:
                    ApplySupportLoadout(mimic, SupportArchetype.Midgard);
                    break;
                case eCharacterClass.Druid:
                    ApplySupportLoadout(mimic, SupportArchetype.Hibernia);
                    break;
                case eCharacterClass.Armsman:
                    ApplyMeleeLoadout(mimic, ClothingMgr.Albion_Fighter.CloneTemplate());
                    break;
                case eCharacterClass.Warrior:
                    ApplyMeleeLoadout(mimic, ClothingMgr.Midgard_Fighter.CloneTemplate());
                    break;
                case eCharacterClass.Hero:
                    ApplyMeleeLoadout(mimic, ClothingMgr.Hibernia_Fighter.CloneTemplate());
                    break;
                default:
                    // Default to a generic look if we do not have a tailored loadout yet.
                    ApplyMeleeLoadout(mimic, ClothingMgr.Albion_Fighter.CloneTemplate());
                    break;
            }
        }

        private static void ApplyMeleeLoadout(MimicNPC mimic, GameNpcInventoryTemplate template)
        {
            mimic.Inventory = template;
            mimic.SwitchWeapon(eActiveWeaponSlot.Standard);
        }

        private static void ApplySupportLoadout(MimicNPC mimic, SupportArchetype archetype)
        {
            mimic.Inventory = archetype switch
            {
                SupportArchetype.Albion => ClothingMgr.Albion_Healer.CloneTemplate(),
                SupportArchetype.Midgard => ClothingMgr.Midgard_Healer.CloneTemplate(),
                SupportArchetype.Hibernia => ClothingMgr.Hibernia_Healer.CloneTemplate(),
                _ => ClothingMgr.Albion_Healer.CloneTemplate()
            };

            mimic.SwitchWeapon(eActiveWeaponSlot.Standard);

            List<Spell> spells = new()
            {
                CreateHealSpell(mimic, 6.0),
                CreateEmergencyHealSpell(mimic, 4.5),
                CreateHealOverTimeSpell(mimic),
                CreateBuffSpell(mimic, eSpellType.StrengthConstitutionBuff, 420, 421),
                CreateBuffSpell(mimic, eSpellType.PowerRegenBuff, 949, 950),
                CreateDamageSpell(mimic, archetype)
            };

            mimic.Spells = spells;
        }

        private static Spell CreateHealSpell(MimicNPC mimic, double scaling)
        {
            Spell spell = CloneBaseSpell("mimic_support_heal", () =>
            {
                DbSpell db = CreateBaseSpell("Mimic Restoration", RestorationSpellId, eSpellType.Heal, eSpellTarget.REALM, 2000, 12, 2.6, 0, 1357);
                db.Description = "A focused heal that restores a moderate amount of health.";
                db.SpellGroup = 9101;
                db.EffectGroup = 9101;
                db.Value = 380;
                return new Spell(db, 50);
            });

            spell.Level = (byte)Math.Clamp(mimic.Level, 1, byte.MaxValue);
            spell.Value = Math.Round(80 + mimic.Level * scaling, 1);
            return spell;
        }

        private static Spell CreateEmergencyHealSpell(MimicNPC mimic, double scaling)
        {
            Spell spell = CloneBaseSpell("mimic_support_quick_heal", () =>
            {
                DbSpell db = CreateBaseSpell("Mimic Resurgence", ResurgenceSpellId, eSpellType.Heal, eSpellTarget.REALM, 1500, 8, 0, 10, 1358);
                db.Description = "A fast emergency heal with a short recharge.";
                db.SpellGroup = 9102;
                db.EffectGroup = 9102;
                db.Value = 300;
                return new Spell(db, 50);
            });

            spell.Level = (byte)Math.Clamp(mimic.Level, 1, byte.MaxValue);
            spell.Value = Math.Round(60 + mimic.Level * scaling, 1);
            return spell;
        }

        private static Spell CreateHealOverTimeSpell(MimicNPC mimic)
        {
            Spell spell = CloneBaseSpell("mimic_support_hot", () =>
            {
                DbSpell db = CreateBaseSpell("Mimic Renewal", RenewalSpellId, eSpellType.HealOverTime, eSpellTarget.REALM, 2000, 6, 2.5, 0, 1360);
                db.Description = "A heal-over-time effect that tops off allies between larger heals.";
                db.SpellGroup = 9103;
                db.EffectGroup = 9103;
                db.Duration = 12;
                db.Frequency = 3;
                db.Value = 75;
                return new Spell(db, 50);
            });

            spell.Level = (byte)Math.Clamp(mimic.Level, 1, byte.MaxValue);
            spell.Value = Math.Round(12 + mimic.Level * 1.4, 1);
            return spell;
        }

        private static Spell CreateBuffSpell(MimicNPC mimic, eSpellType type, int icon, int clientEffect)
        {
            Spell spell = CloneBaseSpell($"mimic_support_buff_{type}", () =>
            {
                DbSpell db = CreateBaseSpell(type == eSpellType.PowerRegenBuff ? "Mimic Serenity" : "Mimic Bulwark", type == eSpellType.PowerRegenBuff ? SerenitySpellId : BulwarkSpellId, type, eSpellTarget.GROUP, 2000, 0, 3.0, 5, icon);
                db.ClientEffect = clientEffect;
                db.Description = "A beneficial enchantment shared with the entire group.";
                db.SpellGroup = 9104 + (int)type;
                db.EffectGroup = db.SpellGroup;
                db.Duration = 600;
                db.Value = type == eSpellType.PowerRegenBuff ? 15 : 150;
                return new Spell(db, 50);
            });

            spell.Level = (byte)Math.Clamp(mimic.Level, 1, byte.MaxValue);
            spell.Value = type == eSpellType.PowerRegenBuff
                ? Math.Round(3 + mimic.Level * 0.25, 1)
                : Math.Round(12 + mimic.Level * 1.1, 1);
            return spell;
        }

        private static Spell CreateDamageSpell(MimicNPC mimic, SupportArchetype archetype)
        {
            Spell spell = CloneBaseSpell("mimic_support_smite", () =>
            {
                DbSpell db = CreateBaseSpell("Mimic Smite", SmiteSpellId, eSpellType.DirectDamage, eSpellTarget.ENEMY, 1500, 12, 2.8, 0, 717);
                db.Description = "A modest ranged smite used to assist the group without diving into melee.";
                db.SpellGroup = 9150;
                db.EffectGroup = 9150;
                db.Damage = 150;
                db.DamageType = (int)eDamageType.Energy;
                return new Spell(db, 50);
            });

            spell.Level = (byte)Math.Clamp(mimic.Level, 1, byte.MaxValue);
            spell.Damage = Math.Round(50 + mimic.Level * 2.2, 1);
            spell.DamageType = archetype switch
            {
                SupportArchetype.Albion => (int)eDamageType.Energy,
                SupportArchetype.Midgard => (int)eDamageType.Spirit,
                SupportArchetype.Hibernia => (int)eDamageType.Heat,
                _ => (int)eDamageType.Energy
            };

            return spell;
        }

        private static DbSpell CreateBaseSpell(string name, int spellId, eSpellType type, eSpellTarget target, int range, int power, double castTime, int recastDelay, int icon)
        {
            DbSpell spell = new()
            {
                AllowAdd = false,
                SpellID = spellId,
                Name = name,
                Icon = icon,
                ClientEffect = icon,
                Target = target.ToString(),
                Range = range,
                Power = power,
                CastTime = castTime,
                RecastDelay = recastDelay,
                Duration = 0,
                Radius = 0,
                Pulse = 0,
                Frequency = 0,
                PulsePower = 0,
                Type = type.ToString(),
                DamageType = (int)eDamageType.Spirit,
                Description = name
            };

            return spell;
        }

        private static Spell CloneBaseSpell(string key, Func<Spell> factory)
        {
            lock (_baseSpells)
            {
                if (!_baseSpells.TryGetValue(key, out Spell? baseSpell))
                {
                    baseSpell = factory();
                    SkillBase.AddScriptedSpell(GlobalSpellsLines.Mob_Spells, baseSpell);
                    _baseSpells[key] = baseSpell;
                }

                return (Spell)baseSpell.Clone();
            }
        }
    }
}
