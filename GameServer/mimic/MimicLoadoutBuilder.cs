using System;
using System.Collections.Generic;
using DOL.Database;
using DOL.GS;
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
        private const int EntrapmentSpellId = 950030;

        private static readonly Dictionary<string, Spell> _baseSpells = new();

        private static readonly HashSet<eCharacterClass> SupportClasses = new()
        {
            eCharacterClass.Cleric,
            eCharacterClass.Druid,
            eCharacterClass.Healer,
            eCharacterClass.Friar,
            eCharacterClass.Shaman,
            eCharacterClass.Bard,
            eCharacterClass.Warden,
            eCharacterClass.Valkyrie,
            eCharacterClass.Heretic,
            eCharacterClass.Mentalist,
            eCharacterClass.Minstrel,
            eCharacterClass.Paladin,
            eCharacterClass.Skald,
            eCharacterClass.MaulerAlb,
            eCharacterClass.MaulerMid,
            eCharacterClass.MaulerHib
        };

        private static readonly HashSet<eCharacterClass> PrimaryHealerClasses = new()
        {
            eCharacterClass.Cleric,
            eCharacterClass.Druid,
            eCharacterClass.Healer,
            eCharacterClass.Shaman,
            eCharacterClass.Friar,
            eCharacterClass.Bard,
            eCharacterClass.Warden,
            eCharacterClass.Valkyrie,
            eCharacterClass.Mentalist,
            eCharacterClass.Heretic
        };

        private static readonly HashSet<eCharacterClass> CasterClasses = new()
        {
            eCharacterClass.Sorcerer,
            eCharacterClass.Wizard,
            eCharacterClass.Cabalist,
            eCharacterClass.Theurgist,
            eCharacterClass.Necromancer,
            eCharacterClass.Spiritmaster,
            eCharacterClass.Runemaster,
            eCharacterClass.Bonedancer,
            eCharacterClass.Warlock,
            eCharacterClass.Animist,
            eCharacterClass.Bainshee,
            eCharacterClass.Eldritch,
            eCharacterClass.Enchanter
        };

        private static readonly HashSet<eCharacterClass> ArcherClasses = new()
        {
            eCharacterClass.Scout,
            eCharacterClass.Hunter,
            eCharacterClass.Ranger
        };

        private static readonly HashSet<eCharacterClass> StealthClasses = new()
        {
            eCharacterClass.Infiltrator,
            eCharacterClass.Shadowblade,
            eCharacterClass.Nightshade
        };

        static MimicLoadoutBuilder()
        {
            ClothingMgr.LoadTemplates();
        }

        public static void Configure(MimicNPC mimic, MimicRole role)
        {
            eCharacterClass characterClass = mimic.Template.CharacterClass;
            SupportArchetype archetype = GetArchetype(mimic.Template.Realm);

            if (SupportClasses.Contains(characterClass) || role.HasFlag(MimicRole.Healer))
            {
                bool includeHeals = PrimaryHealerClasses.Contains(characterClass) || role.HasFlag(MimicRole.Healer);
                ApplySupportLoadout(mimic, archetype, role, includeHeals);
                return;
            }

            if (CasterClasses.Contains(characterClass) || (role.HasFlag(MimicRole.CrowdControl) && !role.HasFlag(MimicRole.Tank)))
            {
                ApplyCasterLoadout(mimic, archetype, role);
                return;
            }

            if (ArcherClasses.Contains(characterClass))
            {
                ApplyArcherLoadout(mimic);
                return;
            }

            if (StealthClasses.Contains(characterClass))
            {
                ApplyStealthLoadout(mimic);
                return;
            }

            ApplyMeleeLoadout(mimic);
        }

        private static void ApplyMeleeLoadout(MimicNPC mimic)
        {
            mimic.Inventory = mimic.Template.Realm switch
            {
                eRealm.Albion => ClothingMgr.Albion_Fighter.CloneTemplate(),
                eRealm.Midgard => ClothingMgr.Midgard_Fighter.CloneTemplate(),
                eRealm.Hibernia => ClothingMgr.Hibernia_Fighter.CloneTemplate(),
                _ => ClothingMgr.Albion_Fighter.CloneTemplate()
            };
            mimic.SwitchWeapon(eActiveWeaponSlot.Standard);
        }

        private static void ApplySupportLoadout(MimicNPC mimic, SupportArchetype archetype, MimicRole role, bool includeHeals)
        {
            mimic.Inventory = archetype switch
            {
                SupportArchetype.Albion => ClothingMgr.Albion_Healer.CloneTemplate(),
                SupportArchetype.Midgard => ClothingMgr.Midgard_Healer.CloneTemplate(),
                SupportArchetype.Hibernia => ClothingMgr.Hibernia_Healer.CloneTemplate(),
                _ => ClothingMgr.Albion_Healer.CloneTemplate()
            };

            mimic.SwitchWeapon(eActiveWeaponSlot.Standard);

            List<Spell> spells = new();

            if (includeHeals)
            {
                spells.Add(CreateHealSpell(mimic, 6.0));
                spells.Add(CreateEmergencyHealSpell(mimic, 4.5));
                spells.Add(CreateHealOverTimeSpell(mimic));
            }

            if (role.HasFlag(MimicRole.Support) || includeHeals)
            {
                spells.Add(CreateBuffSpell(mimic, eSpellType.StrengthConstitutionBuff, 420, 421));
                spells.Add(CreateBuffSpell(mimic, eSpellType.PowerRegenBuff, 949, 950));
            }

            spells.Add(CreateDamageSpell(mimic, archetype));

            if (role.HasFlag(MimicRole.CrowdControl))
                spells.Add(CreateCrowdControlSpell(mimic, archetype));

            mimic.Spells = spells;
        }

        private static void ApplyCasterLoadout(MimicNPC mimic, SupportArchetype archetype, MimicRole role)
        {
            mimic.Inventory = mimic.Template.Realm switch
            {
                eRealm.Albion => ClothingMgr.Albion_Caster.CloneTemplate(),
                eRealm.Midgard => ClothingMgr.Midgard_Caster.CloneTemplate(),
                eRealm.Hibernia => ClothingMgr.Hibernia_Caster.CloneTemplate(),
                _ => ClothingMgr.Albion_Caster.CloneTemplate()
            };

            mimic.SwitchWeapon(eActiveWeaponSlot.Standard);

            List<Spell> spells = new()
            {
                CreateDamageSpell(mimic, archetype)
            };

            if (role.HasFlag(MimicRole.CrowdControl))
                spells.Add(CreateCrowdControlSpell(mimic, archetype));

            if (role.HasFlag(MimicRole.Support))
            {
                spells.Add(CreateBuffSpell(mimic, eSpellType.StrengthConstitutionBuff, 420, 421));
                spells.Add(CreateBuffSpell(mimic, eSpellType.PowerRegenBuff, 949, 950));
            }

            mimic.Spells = spells;
        }

        private static void ApplyArcherLoadout(MimicNPC mimic)
        {
            mimic.Inventory = mimic.Template.Realm switch
            {
                eRealm.Albion => ClothingMgr.Albion_Archer.CloneTemplate(),
                eRealm.Midgard => ClothingMgr.Midgard_Archer.CloneTemplate(),
                eRealm.Hibernia => ClothingMgr.Hibernia_Archer.CloneTemplate(),
                _ => ClothingMgr.Albion_Archer.CloneTemplate()
            };

            mimic.SwitchWeapon(eActiveWeaponSlot.Distance);
        }

        private static void ApplyStealthLoadout(MimicNPC mimic)
        {
            mimic.Inventory = mimic.Template.Realm switch
            {
                eRealm.Albion => ClothingMgr.Albion_Stealther.CloneTemplate(),
                eRealm.Midgard => ClothingMgr.Midgard_Stealther.CloneTemplate(),
                eRealm.Hibernia => ClothingMgr.Hibernia_Stealther.CloneTemplate(),
                _ => ClothingMgr.Albion_Stealther.CloneTemplate()
            };

            mimic.SwitchWeapon(eActiveWeaponSlot.Standard);
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

        private static Spell CreateCrowdControlSpell(MimicNPC mimic, SupportArchetype archetype)
        {
            Spell spell = CloneBaseSpell($"mimic_support_root_{archetype}", () =>
            {
                DbSpell db = CreateBaseSpell("Mimic Entrapment", EntrapmentSpellId + (int)archetype, eSpellType.Root, eSpellTarget.ENEMY, 1500, 12, 2.8, 15, 716);
                db.Description = "A binding spell that roots an enemy in place to protect the group.";
                db.SpellGroup = 9155 + (int)archetype;
                db.EffectGroup = db.SpellGroup;
                db.Duration = 20;
                return new Spell(db, 50);
            });

            spell.Level = (byte)Math.Clamp(mimic.Level, 1, byte.MaxValue);
            spell.Duration = (int)Math.Clamp(10 + mimic.Level * 0.4, 12, 30);
            return spell;
        }

        private static SupportArchetype GetArchetype(eRealm realm)
        {
            return realm switch
            {
                eRealm.Albion => SupportArchetype.Albion,
                eRealm.Midgard => SupportArchetype.Midgard,
                eRealm.Hibernia => SupportArchetype.Hibernia,
                _ => SupportArchetype.Albion
            };
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
