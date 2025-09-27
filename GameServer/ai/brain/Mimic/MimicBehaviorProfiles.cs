using System;
using System.Collections.Generic;

namespace DOL.GS.Mimic
{
    [Flags]
    internal enum MimicBehaviorCapability
    {
        None = 0,
        Heals = 1 << 0,
        Buffs = 1 << 1,
        CrowdControl = 1 << 2,
        Interrupts = 1 << 3,
        Disease = 1 << 4,
        Nearsight = 1 << 5,
        Pets = 1 << 6,
        SpeedSongs = 1 << 7,
        Endurance = 1 << 8,
        PowerSong = 1 << 9,
        PulsingBladeturn = 1 << 10,
        Scout = 1 << 11,
        Damage = 1 << 12
    }

    internal sealed class MimicBehaviorProfile
    {
        public MimicBehaviorProfile(MimicBehaviorCapability capabilities, bool prefersRanged = false, bool prefersMelee = false, bool primaryHealer = false)
        {
            Capabilities = capabilities;
            PrefersRanged = prefersRanged;
            PrefersMelee = prefersMelee;
            PrimaryHealer = primaryHealer;
        }

        public MimicBehaviorCapability Capabilities { get; }
        public bool PrefersRanged { get; }
        public bool PrefersMelee { get; }
        public bool PrimaryHealer { get; }

        public bool HasCapability(MimicBehaviorCapability capability) => (Capabilities & capability) != 0;
    }

    internal static class MimicBehaviorProfiles
    {
        private static readonly Dictionary<eCharacterClass, MimicBehaviorProfile> _profiles = new()
        {
            // Albion
            { eCharacterClass.Armsman, new MimicBehaviorProfile(MimicBehaviorCapability.Damage | MimicBehaviorCapability.CrowdControl, prefersMelee: true) },
            { eCharacterClass.Paladin, new MimicBehaviorProfile(MimicBehaviorCapability.Buffs | MimicBehaviorCapability.Heals | MimicBehaviorCapability.Damage, prefersMelee: true) },
            { eCharacterClass.Scout, new MimicBehaviorProfile(MimicBehaviorCapability.Damage | MimicBehaviorCapability.Scout, prefersRanged: true) },
            { eCharacterClass.Infiltrator, new MimicBehaviorProfile(MimicBehaviorCapability.Damage, prefersMelee: true) },
            { eCharacterClass.Mercenary, new MimicBehaviorProfile(MimicBehaviorCapability.Damage, prefersMelee: true) },
            { eCharacterClass.Reaver, new MimicBehaviorProfile(MimicBehaviorCapability.Damage | MimicBehaviorCapability.CrowdControl, prefersMelee: true) },
            { eCharacterClass.Friar, new MimicBehaviorProfile(MimicBehaviorCapability.Heals | MimicBehaviorCapability.Buffs | MimicBehaviorCapability.Damage, prefersMelee: true, primaryHealer: true) },
            { eCharacterClass.Cleric, new MimicBehaviorProfile(MimicBehaviorCapability.Heals | MimicBehaviorCapability.Buffs | MimicBehaviorCapability.CrowdControl | MimicBehaviorCapability.Interrupts, primaryHealer: true) },
            { eCharacterClass.Heretic, new MimicBehaviorProfile(MimicBehaviorCapability.Heals | MimicBehaviorCapability.Damage | MimicBehaviorCapability.Interrupts) },
            { eCharacterClass.Minstrel, new MimicBehaviorProfile(MimicBehaviorCapability.CrowdControl | MimicBehaviorCapability.Interrupts | MimicBehaviorCapability.SpeedSongs | MimicBehaviorCapability.Damage) },
            { eCharacterClass.Sorcerer, new MimicBehaviorProfile(MimicBehaviorCapability.CrowdControl | MimicBehaviorCapability.Interrupts | MimicBehaviorCapability.Nearsight | MimicBehaviorCapability.Damage) },
            { eCharacterClass.Wizard, new MimicBehaviorProfile(MimicBehaviorCapability.Damage | MimicBehaviorCapability.Interrupts, prefersRanged: true) },
            { eCharacterClass.Cabalist, new MimicBehaviorProfile(MimicBehaviorCapability.Damage | MimicBehaviorCapability.Interrupts | MimicBehaviorCapability.Nearsight | MimicBehaviorCapability.Disease, prefersRanged: true) },
            { eCharacterClass.Theurgist, new MimicBehaviorProfile(MimicBehaviorCapability.CrowdControl | MimicBehaviorCapability.Interrupts | MimicBehaviorCapability.Pets | MimicBehaviorCapability.Damage, prefersRanged: true) },
            { eCharacterClass.Necromancer, new MimicBehaviorProfile(MimicBehaviorCapability.Damage | MimicBehaviorCapability.Pets | MimicBehaviorCapability.Interrupts, prefersRanged: true) },
            { eCharacterClass.MaulerAlb, new MimicBehaviorProfile(MimicBehaviorCapability.Damage | MimicBehaviorCapability.Interrupts | MimicBehaviorCapability.Buffs, prefersMelee: true) },

            // Midgard
            { eCharacterClass.Warrior, new MimicBehaviorProfile(MimicBehaviorCapability.Damage | MimicBehaviorCapability.CrowdControl, prefersMelee: true) },
            { eCharacterClass.Berserker, new MimicBehaviorProfile(MimicBehaviorCapability.Damage, prefersMelee: true) },
            { eCharacterClass.Savage, new MimicBehaviorProfile(MimicBehaviorCapability.Damage, prefersMelee: true) },
            { eCharacterClass.Thane, new MimicBehaviorProfile(MimicBehaviorCapability.Damage | MimicBehaviorCapability.Interrupts | MimicBehaviorCapability.Buffs, prefersMelee: true) },
            { eCharacterClass.Valkyrie, new MimicBehaviorProfile(MimicBehaviorCapability.Heals | MimicBehaviorCapability.Buffs | MimicBehaviorCapability.Damage | MimicBehaviorCapability.CrowdControl, prefersMelee: true) },
            { eCharacterClass.Skald, new MimicBehaviorProfile(MimicBehaviorCapability.CrowdControl | MimicBehaviorCapability.Interrupts | MimicBehaviorCapability.SpeedSongs | MimicBehaviorCapability.Damage, prefersMelee: true) },
            { eCharacterClass.Hunter, new MimicBehaviorProfile(MimicBehaviorCapability.Damage | MimicBehaviorCapability.Scout | MimicBehaviorCapability.Pets, prefersRanged: true) },
            { eCharacterClass.Shadowblade, new MimicBehaviorProfile(MimicBehaviorCapability.Damage, prefersMelee: true) },
            { eCharacterClass.Spiritmaster, new MimicBehaviorProfile(MimicBehaviorCapability.CrowdControl | MimicBehaviorCapability.Interrupts | MimicBehaviorCapability.Nearsight | MimicBehaviorCapability.Pets | MimicBehaviorCapability.Damage, prefersRanged: true) },
            { eCharacterClass.Runemaster, new MimicBehaviorProfile(MimicBehaviorCapability.CrowdControl | MimicBehaviorCapability.Interrupts | MimicBehaviorCapability.Nearsight | MimicBehaviorCapability.Damage, prefersRanged: true) },
            { eCharacterClass.Bonedancer, new MimicBehaviorProfile(MimicBehaviorCapability.Damage | MimicBehaviorCapability.Pets | MimicBehaviorCapability.Interrupts, prefersRanged: true) },
            { eCharacterClass.Warlock, new MimicBehaviorProfile(MimicBehaviorCapability.Damage | MimicBehaviorCapability.Interrupts | MimicBehaviorCapability.CrowdControl, prefersRanged: true) },
            { eCharacterClass.Shaman, new MimicBehaviorProfile(MimicBehaviorCapability.Heals | MimicBehaviorCapability.Buffs | MimicBehaviorCapability.CrowdControl | MimicBehaviorCapability.Disease | MimicBehaviorCapability.Interrupts, primaryHealer: true) },
            { eCharacterClass.Healer, new MimicBehaviorProfile(MimicBehaviorCapability.Heals | MimicBehaviorCapability.Buffs | MimicBehaviorCapability.CrowdControl | MimicBehaviorCapability.Interrupts, primaryHealer: true) },
            { eCharacterClass.MaulerMid, new MimicBehaviorProfile(MimicBehaviorCapability.Damage | MimicBehaviorCapability.Interrupts | MimicBehaviorCapability.Buffs, prefersMelee: true) },

            // Hibernia
            { eCharacterClass.Hero, new MimicBehaviorProfile(MimicBehaviorCapability.Damage | MimicBehaviorCapability.CrowdControl, prefersMelee: true) },
            { eCharacterClass.Champion, new MimicBehaviorProfile(MimicBehaviorCapability.Damage | MimicBehaviorCapability.Interrupts | MimicBehaviorCapability.Buffs, prefersMelee: true) },
            { eCharacterClass.Blademaster, new MimicBehaviorProfile(MimicBehaviorCapability.Damage, prefersMelee: true) },
            { eCharacterClass.Ranger, new MimicBehaviorProfile(MimicBehaviorCapability.Damage | MimicBehaviorCapability.Scout | MimicBehaviorCapability.Pets, prefersRanged: true) },
            { eCharacterClass.Nightshade, new MimicBehaviorProfile(MimicBehaviorCapability.Damage, prefersMelee: true) },
            { eCharacterClass.Valewalker, new MimicBehaviorProfile(MimicBehaviorCapability.Damage | MimicBehaviorCapability.Interrupts, prefersMelee: true) },
            { eCharacterClass.Vampiir, new MimicBehaviorProfile(MimicBehaviorCapability.Damage | MimicBehaviorCapability.Interrupts, prefersMelee: true) },
            { eCharacterClass.Druid, new MimicBehaviorProfile(MimicBehaviorCapability.Heals | MimicBehaviorCapability.Buffs | MimicBehaviorCapability.CrowdControl, primaryHealer: true) },
            { eCharacterClass.Bard, new MimicBehaviorProfile(MimicBehaviorCapability.Heals | MimicBehaviorCapability.Buffs | MimicBehaviorCapability.CrowdControl | MimicBehaviorCapability.SpeedSongs | MimicBehaviorCapability.Interrupts, primaryHealer: true) },
            { eCharacterClass.Warden, new MimicBehaviorProfile(MimicBehaviorCapability.Heals | MimicBehaviorCapability.Buffs | MimicBehaviorCapability.CrowdControl | MimicBehaviorCapability.PulsingBladeturn | MimicBehaviorCapability.Damage, primaryHealer: true) },
            { eCharacterClass.Mentalist, new MimicBehaviorProfile(MimicBehaviorCapability.Heals | MimicBehaviorCapability.CrowdControl | MimicBehaviorCapability.Interrupts | MimicBehaviorCapability.Nearsight | MimicBehaviorCapability.Damage, primaryHealer: true) },
            { eCharacterClass.Eldritch, new MimicBehaviorProfile(MimicBehaviorCapability.CrowdControl | MimicBehaviorCapability.Interrupts | MimicBehaviorCapability.Nearsight | MimicBehaviorCapability.Damage, prefersRanged: true) },
            { eCharacterClass.Enchanter, new MimicBehaviorProfile(MimicBehaviorCapability.Damage | MimicBehaviorCapability.Pets | MimicBehaviorCapability.Interrupts, prefersRanged: true) },
            { eCharacterClass.Animist, new MimicBehaviorProfile(MimicBehaviorCapability.Damage | MimicBehaviorCapability.Pets | MimicBehaviorCapability.Interrupts, prefersRanged: true) },
            { eCharacterClass.Bainshee, new MimicBehaviorProfile(MimicBehaviorCapability.Damage | MimicBehaviorCapability.Interrupts, prefersRanged: true) },
            { eCharacterClass.MaulerHib, new MimicBehaviorProfile(MimicBehaviorCapability.Damage | MimicBehaviorCapability.Interrupts | MimicBehaviorCapability.Buffs, prefersMelee: true) }
        };

        public static bool TryGetProfile(eCharacterClass characterClass, out MimicBehaviorProfile? profile)
        {
            return _profiles.TryGetValue(characterClass, out profile);
        }
    }
}

