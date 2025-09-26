using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using DOL.GS.PlayerClass;
using DOL.GS.Utils;
using DOL.GS;

namespace DOL.GS.Mimic
{
    public static class MimicManager
    {
        private static readonly List<MimicTemplate> _templates = new()
        {
            new MimicTemplate("alb_armsman", "Albion Armsman", eRealm.Albion, eCharacterClass.Armsman, 49, 5, 50),
            new MimicTemplate("alb_cleric", "Albion Cleric", eRealm.Albion, eCharacterClass.Cleric, 183, 5, 50),
            new MimicTemplate("mid_warrior", "Midgard Warrior", eRealm.Midgard, eCharacterClass.Warrior, 144, 5, 50),
            new MimicTemplate("mid_healer", "Midgard Healer", eRealm.Midgard, eCharacterClass.Healer, 179, 5, 50),
            new MimicTemplate("hib_hero", "Hibernia Hero", eRealm.Hibernia, eCharacterClass.Hero, 190, 5, 50),
            new MimicTemplate("hib_druid", "Hibernia Druid", eRealm.Hibernia, eCharacterClass.Druid, 188, 5, 50)
        };

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

            if (!mimic.AddToWorld())
                throw new InvalidOperationException("Unable to add mimic to world.");

            EnsureGroupMembership(player, mimic);
            groupState.AddMember(mimic);
            _activeMimics[mimic.InternalID] = mimic;
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
        }

        public static void GuardTarget(MimicNPC mimic, GameLiving? target)
        {
            mimic.SetGuardTarget(target);
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
