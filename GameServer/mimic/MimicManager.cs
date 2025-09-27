using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using DOL.GS;
using DOL.GS.PacketHandler;
using DOL.GS.Utils;

namespace DOL.GS.Mimic
{
    public static class MimicManager
    {
        private static readonly List<MimicTemplate> _templates = new()
        {
            // Albion
            CreateTemplate("alb_armsman", "Albion Armsman", eRealm.Albion, eCharacterClass.Armsman, MimicRole.Tank | MimicRole.DamageDealer),
            CreateTemplate("alb_paladin", "Albion Paladin", eRealm.Albion, eCharacterClass.Paladin, MimicRole.Tank | MimicRole.Support),
            CreateTemplate("alb_scout", "Albion Scout", eRealm.Albion, eCharacterClass.Scout, MimicRole.DamageDealer | MimicRole.Scout),
            CreateTemplate("alb_infiltrator", "Albion Infiltrator", eRealm.Albion, eCharacterClass.Infiltrator, MimicRole.DamageDealer | MimicRole.Scout),
            CreateTemplate("alb_mercenary", "Albion Mercenary", eRealm.Albion, eCharacterClass.Mercenary, MimicRole.DamageDealer | MimicRole.Assist),
            CreateTemplate("alb_reaver", "Albion Reaver", eRealm.Albion, eCharacterClass.Reaver, MimicRole.Tank | MimicRole.DamageDealer),
            CreateTemplate("alb_friar", "Albion Friar", eRealm.Albion, eCharacterClass.Friar, MimicRole.Healer | MimicRole.Support | MimicRole.DamageDealer),
            CreateTemplate("alb_cleric", "Albion Cleric", eRealm.Albion, eCharacterClass.Cleric, MimicRole.Healer | MimicRole.Support),
            CreateTemplate("alb_heretic", "Albion Heretic", eRealm.Albion, eCharacterClass.Heretic, MimicRole.Support | MimicRole.DamageDealer),
            CreateTemplate("alb_minstrel", "Albion Minstrel", eRealm.Albion, eCharacterClass.Minstrel, MimicRole.Support | MimicRole.CrowdControl | MimicRole.Scout | MimicRole.DamageDealer),
            CreateTemplate("alb_sorcerer", "Albion Sorcerer", eRealm.Albion, eCharacterClass.Sorcerer, MimicRole.CrowdControl | MimicRole.DamageDealer),
            CreateTemplate("alb_wizard", "Albion Wizard", eRealm.Albion, eCharacterClass.Wizard, MimicRole.DamageDealer),
            CreateTemplate("alb_cabalist", "Albion Cabalist", eRealm.Albion, eCharacterClass.Cabalist, MimicRole.CrowdControl | MimicRole.DamageDealer),
            CreateTemplate("alb_theurgist", "Albion Theurgist", eRealm.Albion, eCharacterClass.Theurgist, MimicRole.CrowdControl | MimicRole.Support),
            CreateTemplate("alb_necromancer", "Albion Necromancer", eRealm.Albion, eCharacterClass.Necromancer, MimicRole.CrowdControl | MimicRole.DamageDealer),
            CreateTemplate("alb_mauler", "Albion Mauler", eRealm.Albion, eCharacterClass.MaulerAlb, MimicRole.Tank | MimicRole.Support),

            // Midgard
            CreateTemplate("mid_warrior", "Midgard Warrior", eRealm.Midgard, eCharacterClass.Warrior, MimicRole.Tank | MimicRole.DamageDealer),
            CreateTemplate("mid_berserker", "Midgard Berserker", eRealm.Midgard, eCharacterClass.Berserker, MimicRole.DamageDealer),
            CreateTemplate("mid_savage", "Midgard Savage", eRealm.Midgard, eCharacterClass.Savage, MimicRole.DamageDealer),
            CreateTemplate("mid_thane", "Midgard Thane", eRealm.Midgard, eCharacterClass.Thane, MimicRole.Tank | MimicRole.Support),
            CreateTemplate("mid_valkyrie", "Midgard Valkyrie", eRealm.Midgard, eCharacterClass.Valkyrie, MimicRole.Tank | MimicRole.Healer | MimicRole.Support),
            CreateTemplate("mid_skald", "Midgard Skald", eRealm.Midgard, eCharacterClass.Skald, MimicRole.Support | MimicRole.DamageDealer | MimicRole.Scout),
            CreateTemplate("mid_hunter", "Midgard Hunter", eRealm.Midgard, eCharacterClass.Hunter, MimicRole.DamageDealer | MimicRole.Scout),
            CreateTemplate("mid_shadowblade", "Midgard Shadowblade", eRealm.Midgard, eCharacterClass.Shadowblade, MimicRole.DamageDealer | MimicRole.Scout),
            CreateTemplate("mid_spiritmaster", "Midgard Spiritmaster", eRealm.Midgard, eCharacterClass.Spiritmaster, MimicRole.CrowdControl | MimicRole.DamageDealer),
            CreateTemplate("mid_runemaster", "Midgard Runemaster", eRealm.Midgard, eCharacterClass.Runemaster, MimicRole.CrowdControl | MimicRole.DamageDealer),
            CreateTemplate("mid_bonedancer", "Midgard Bonedancer", eRealm.Midgard, eCharacterClass.Bonedancer, MimicRole.DamageDealer | MimicRole.Support),
            CreateTemplate("mid_warlock", "Midgard Warlock", eRealm.Midgard, eCharacterClass.Warlock, MimicRole.CrowdControl | MimicRole.DamageDealer),
            CreateTemplate("mid_shaman", "Midgard Shaman", eRealm.Midgard, eCharacterClass.Shaman, MimicRole.Healer | MimicRole.Support),
            CreateTemplate("mid_healer", "Midgard Healer", eRealm.Midgard, eCharacterClass.Healer, MimicRole.Healer | MimicRole.Support | MimicRole.CrowdControl),
            CreateTemplate("mid_mauler", "Midgard Mauler", eRealm.Midgard, eCharacterClass.MaulerMid, MimicRole.Tank | MimicRole.Support),

            // Hibernia
            CreateTemplate("hib_hero", "Hibernia Hero", eRealm.Hibernia, eCharacterClass.Hero, MimicRole.Tank | MimicRole.DamageDealer),
            CreateTemplate("hib_champion", "Hibernia Champion", eRealm.Hibernia, eCharacterClass.Champion, MimicRole.Tank | MimicRole.Support),
            CreateTemplate("hib_blademaster", "Hibernia Blademaster", eRealm.Hibernia, eCharacterClass.Blademaster, MimicRole.DamageDealer),
            CreateTemplate("hib_ranger", "Hibernia Ranger", eRealm.Hibernia, eCharacterClass.Ranger, MimicRole.DamageDealer | MimicRole.Scout),
            CreateTemplate("hib_nightshade", "Hibernia Nightshade", eRealm.Hibernia, eCharacterClass.Nightshade, MimicRole.DamageDealer | MimicRole.Scout),
            CreateTemplate("hib_valewalker", "Hibernia Valewalker", eRealm.Hibernia, eCharacterClass.Valewalker, MimicRole.DamageDealer | MimicRole.Support),
            CreateTemplate("hib_vampiir", "Hibernia Vampiir", eRealm.Hibernia, eCharacterClass.Vampiir, MimicRole.DamageDealer),
            CreateTemplate("hib_druid", "Hibernia Druid", eRealm.Hibernia, eCharacterClass.Druid, MimicRole.Healer | MimicRole.Support),
            CreateTemplate("hib_bard", "Hibernia Bard", eRealm.Hibernia, eCharacterClass.Bard, MimicRole.Support | MimicRole.CrowdControl | MimicRole.Scout),
            CreateTemplate("hib_warden", "Hibernia Warden", eRealm.Hibernia, eCharacterClass.Warden, MimicRole.Tank | MimicRole.Support),
            CreateTemplate("hib_mentalist", "Hibernia Mentalist", eRealm.Hibernia, eCharacterClass.Mentalist, MimicRole.Healer | MimicRole.Support | MimicRole.CrowdControl),
            CreateTemplate("hib_eldritch", "Hibernia Eldritch", eRealm.Hibernia, eCharacterClass.Eldritch, MimicRole.CrowdControl | MimicRole.DamageDealer),
            CreateTemplate("hib_enchanter", "Hibernia Enchanter", eRealm.Hibernia, eCharacterClass.Enchanter, MimicRole.DamageDealer | MimicRole.Support),
            CreateTemplate("hib_animist", "Hibernia Animist", eRealm.Hibernia, eCharacterClass.Animist, MimicRole.DamageDealer | MimicRole.Support),
            CreateTemplate("hib_bainshee", "Hibernia Bainshee", eRealm.Hibernia, eCharacterClass.Bainshee, MimicRole.CrowdControl | MimicRole.DamageDealer),
            CreateTemplate("hib_mauler", "Hibernia Mauler", eRealm.Hibernia, eCharacterClass.MaulerHib, MimicRole.Tank | MimicRole.Support)
        };

        private const byte DefaultMinimumLevel = 5;
        private const byte DefaultMaximumLevel = 50;

        private static MimicTemplate CreateTemplate(string id, string displayName, eRealm realm, eCharacterClass characterClass, MimicRole role)
        {
            return new MimicTemplate(id, displayName, realm, characterClass, GetModelId(realm, role), role, DefaultMinimumLevel, DefaultMaximumLevel);
        }

        private static ushort GetModelId(eRealm realm, MimicRole role)
        {
            bool prefersRobes = role.HasFlag(MimicRole.Healer) || role.HasFlag(MimicRole.CrowdControl) || (role.HasFlag(MimicRole.Support) && !role.HasFlag(MimicRole.Tank));

            return realm switch
            {
                eRealm.Albion => prefersRobes ? (ushort)183 : (ushort)49,
                eRealm.Midgard => prefersRobes ? (ushort)179 : (ushort)144,
                eRealm.Hibernia => prefersRobes ? (ushort)188 : (ushort)190,
                _ => (ushort)49
            };
        }

        private static readonly ConcurrentDictionary<GamePlayer, MimicGroupState> _groupStates = new();
        private static readonly ConcurrentDictionary<string, MimicNPC> _activeMimics = new();
        private static readonly ConcurrentDictionary<string, bool> _activeBattles = new(StringComparer.OrdinalIgnoreCase);

        public static IReadOnlyList<MimicTemplate> GetTemplatesForPlayer(GamePlayer player)
        {
            return _templates
                .Where(t => t.Realm == player.Realm && t.SupportsLevel(player.Level))
                .OrderBy(t => t.DisplayName)
                .ToList();
        }

        public static IReadOnlyList<MimicTemplate> GetTemplatesForRealm(eRealm realm)
        {
            return _templates
                .Where(t => t.Realm == realm)
                .OrderBy(t => t.DisplayName)
                .ToList();
        }

        public static MimicTemplate? FindTemplateByClass(string className)
        {
            return _templates.FirstOrDefault(t => string.Equals(t.CharacterClass.ToString(), className, StringComparison.OrdinalIgnoreCase)
                                                  || string.Equals(t.DisplayName, className, StringComparison.OrdinalIgnoreCase)
                                                  || string.Equals(t.Id, className, StringComparison.OrdinalIgnoreCase));
        }

        public static MimicNPC? FindMimic(GamePlayer controller, string name)
        {
            return GetOrCreateGroupState(controller).Members.FirstOrDefault(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        public static IReadOnlyList<MimicNPC> GetMimics(GamePlayer player)
        {
            return GetOrCreateGroupState(player).Members.ToList();
        }

        public static MimicNPC CreateMimic(GamePlayer player, MimicTemplate template, int level)
        {
            if (player.CurrentRegion == null)
                throw new InvalidOperationException("Player is not in a valid region.");

            MimicGroupState groupState = GetOrCreateGroupState(player);
            MimicNPC mimic = new(template, player, groupState, level)
            {
                CurrentRegion = player.CurrentRegion,
                X = player.X + Util.Random(-200, 200),
                Y = player.Y + Util.Random(-200, 200),
                Z = player.Z,
                Heading = player.Heading
            };

            mimic.AssignRole(template.DefaultRole);

            if (!mimic.AddToWorld())
                throw new InvalidOperationException("Unable to add mimic to world.");

            mimic.BroadcastLivingEquipmentUpdate();

            EnsureGroupMembership(player, mimic);
            groupState.AddMember(mimic);
            _activeMimics[mimic.InternalID] = mimic;

            player.Out.SendMessage($"{mimic.Name} has been summoned at level {mimic.Level}. Role: {MimicRoleInfo.ToDisplayString(mimic.Role)}.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
            return mimic;
        }

        public static void RemoveMimic(MimicNPC mimic)
        {
            if (mimic.Group != null)
            {
                mimic.Group.RemoveMember(mimic);
            }

            mimic.RemoveFromWorld();

            if (_activeMimics.TryRemove(mimic.InternalID, out _))
            {
                mimic.GroupState.RemoveMember(mimic);
            }
        }

        public static int ClearAllMimics()
        {
            int removed = 0;

            foreach (MimicNPC mimic in _activeMimics.Values.ToList())
            {
                RemoveMimic(mimic);
                removed++;
            }

            PruneEmptyGroupStates();

            return removed;
        }

        public static int ClearUngroupedMimics()
        {
            int removed = 0;

            foreach (MimicNPC mimic in _activeMimics.Values.ToList())
            {
                if (mimic.Group != null)
                    continue;

                RemoveMimic(mimic);
                removed++;
            }

            if (removed > 0)
                PruneEmptyGroupStates();

            return removed;
        }

        public static void SummonGroup(GamePlayer player)
        {
            foreach (MimicNPC mimic in GetMimics(player))
            {
                mimic.TeleportTo(player);
            }
        }

        public static void SetPreventCombat(GamePlayer player, bool value)
        {
            foreach (MimicNPC mimic in GetMimics(player))
            {
                mimic.SetPreventCombat(value);
            }
        }

        public static void SetPreventCombat(MimicNPC mimic, bool value)
        {
            mimic.SetPreventCombat(value);
        }

        public static void SetPvPMode(GamePlayer player, bool value)
        {
            foreach (MimicNPC mimic in GetMimics(player))
            {
                mimic.SetPvPMode(value);
            }
        }

        public static void SetPvPMode(MimicNPC mimic, bool value)
        {
            mimic.SetPvPMode(value);
        }

        public static void AssignRole(MimicNPC mimic, MimicRole role)
        {
            mimic.AssignRole(role);
            AnnounceRoleChange(mimic);
        }

        public static void GuardTarget(MimicNPC mimic, GameLiving? target)
        {
            mimic.SetGuardTarget(target);
        }

        private static void AnnounceRoleChange(MimicNPC mimic)
        {
            GamePlayer owner = mimic.Owner;
            owner.Out.SendMessage($"{mimic.Name} role set to {MimicRoleInfo.ToDisplayString(mimic.Role)}.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
        }

        public static void SetCamp(GamePlayer player, Point3D location, int aggroRange, ConColor filter)
        {
            MimicGroupState state = GetOrCreateGroupState(player);
            state.SetCamp(new MimicCampSettings(location, aggroRange, filter));
        }

        public static void ClearCamp(GamePlayer player)
        {
            GetOrCreateGroupState(player).ClearCamp();
        }

        public static void SetAggroRange(GamePlayer player, int range)
        {
            GetOrCreateGroupState(player).SetAggroRange(range);
        }

        public static void SetFilter(GamePlayer player, ConColor color)
        {
            GetOrCreateGroupState(player).SetFilter(color);
        }

        public static MimicGroupState GetOrCreateGroupState(GamePlayer player)
        {
            return _groupStates.GetOrAdd(player, p => new MimicGroupState(p));
        }

        private static void PruneEmptyGroupStates()
        {
            foreach (var entry in _groupStates.ToList())
            {
                if (entry.Value.Members.Count == 0)
                    _groupStates.TryRemove(entry.Key, out _);
            }
        }

        public static bool TryParseConColor(string text, out ConColor color)
        {
            color = text.ToLowerInvariant() switch
            {
                "grey" => ConColor.GREY,
                "gray" => ConColor.GREY,
                "green" => ConColor.GREEN,
                "blue" => ConColor.BLUE,
                "yellow" => ConColor.YELLOW,
                "orange" => ConColor.ORANGE,
                "red" => ConColor.RED,
                "purple" => ConColor.PURPLE,
                _ => ConColor.UNKNOWN
            };

            return color != ConColor.UNKNOWN;
        }

        public static void SetBattleState(string regionKey, bool active)
        {
            if (active)
                _activeBattles[regionKey] = true;
            else
                _activeBattles.TryRemove(regionKey, out _);
        }

        public static bool IsBattleActive(string regionKey)
        {
            return _activeBattles.ContainsKey(regionKey);
        }

        private static void EnsureGroupMembership(GamePlayer player, MimicNPC mimic)
        {
            Group? group = player.Group;
            if (group == null)
            {
                group = new Group(player);
                GroupMgr.AddGroup(group);
                group.AddMember(player);
            }

            if (group.GetMembersInTheGroup().Contains(mimic))
                return;

            group.AddMember(mimic);
        }
    }
}
