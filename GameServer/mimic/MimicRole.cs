using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DOL.GS.Mimic
{
    [Flags]
    public enum MimicRole
    {
        None = 0,
        Leader = 1 << 0,
        Puller = 1 << 1,
        Tank = 1 << 2,
        CrowdControl = 1 << 3,
        Assist = 1 << 4,
        Healer = 1 << 5,
        Support = 1 << 6,
        DamageDealer = 1 << 7,
        Scout = 1 << 8
    }

    public static class MimicRoleInfo
    {
        private sealed record RoleDefinition(MimicRole Role, string Command, string Display, string Description, string[] Aliases)
        {
            public IEnumerable<string> GetAliases()
            {
                yield return Command;
                yield return Display;
                yield return Display.Replace(" ", string.Empty, StringComparison.Ordinal);

                foreach (string alias in Aliases)
                    yield return alias;
            }
        }

        private static readonly RoleDefinition[] _definitions =
        {
            new(
                MimicRole.Leader,
                "leader",
                "Leader",
                "Keeps mimics in formation and initiates combat on camp threats when no orders are given.",
                new[] { "lead", "captain", "commander" }),
            new(
                MimicRole.Puller,
                "puller",
                "Puller",
                "Ranges ahead to tag nearby enemies for the group when it is safe to do so.",
                new[] { "pull", "scoutpull", "pullbot" }),
            new(
                MimicRole.Tank,
                "tank",
                "Tank",
                "Guards the owner, soaks aggro, and always rushes to defend allies under attack.",
                new[] { "mt", "guard", "protector" }),
            new(
                MimicRole.CrowdControl,
                "cc",
                "Crowd Control",
                "Uses disabling spells to lock down extra enemies and will engage camp targets to control them.",
                new[] { "crowdcontrol", "crowd", "mez", "root", "control" }),
            new(
                MimicRole.Assist,
                "assist",
                "Assist",
                "Follows the owner's target calls for focused damage alongside other attackers.",
                new[] { "assisttrain", "ma", "assistdps" }),
            new(
                MimicRole.Healer,
                "healer",
                "Healer",
                "Prioritizes restoring health and only commits to combat when the group is threatened.",
                new[] { "heal", "heals", "mainhealer", "supportheals" }),
            new(
                MimicRole.Support,
                "support",
                "Support",
                "Provides buffs and utility while reacting to danger with defensive actions.",
                new[] { "buffer", "utility", "supportbot" }),
            new(
                MimicRole.DamageDealer,
                "dps",
                "Damage Dealer",
                "Commits to aggressive damage against the owner's targets.",
                new[] { "damage", "damagedealer", "dealer", "dd" }),
            new(
                MimicRole.Scout,
                "scout",
                "Scout",
                "Patrols for incoming enemies and reports threats while assisting with pulls when needed.",
                new[] { "spotter", "recon", "lookout" })
        };

        private static readonly Dictionary<string, MimicRole> _aliasMap = BuildAliasMap();
        private static readonly char[] _roleSeparators = { ',', '+', '|', '/', '\\', ' ' };

        public static bool TryParse(string value, out MimicRole role)
        {
            role = MimicRole.None;

            if (string.IsNullOrWhiteSpace(value))
                return false;

            string[] tokens = value.Split(_roleSeparators, StringSplitOptions.RemoveEmptyEntries);

            foreach (string token in tokens)
            {
                string normalized = NormalizeToken(token);

                if (string.IsNullOrEmpty(normalized))
                    continue;

                if (_aliasMap.TryGetValue(normalized, out MimicRole match))
                {
                    role |= match;
                    continue;
                }

                if (Enum.TryParse(token, true, out MimicRole parsed) && parsed != MimicRole.None)
                {
                    role |= parsed;
                    continue;
                }

                if (Enum.TryParse(normalized, true, out parsed) && parsed != MimicRole.None)
                {
                    role |= parsed;
                    continue;
                }

                role = MimicRole.None;
                return false;
            }

            return role != MimicRole.None;
        }

        public static string GetSyntax()
        {
            return string.Join('|', _definitions.Select(def => def.Command));
        }

        public static string ToDisplayString(MimicRole role)
        {
            if (role == MimicRole.None)
                return "None";

            List<string> parts = new();

            foreach (RoleDefinition definition in _definitions)
            {
                if (role.HasFlag(definition.Role))
                    parts.Add(definition.Display);
            }

            return parts.Count > 0 ? string.Join(", ", parts) : "None";
        }

        public static IEnumerable<string> GetRoleSummaries()
        {
            foreach (RoleDefinition definition in _definitions)
            {
                yield return $"{definition.Display} ({definition.Command}): {definition.Description}";
            }
        }

        private static Dictionary<string, MimicRole> BuildAliasMap()
        {
            Dictionary<string, MimicRole> map = new(StringComparer.OrdinalIgnoreCase);

            foreach (RoleDefinition definition in _definitions)
            {
                foreach (string alias in definition.GetAliases())
                {
                    string normalized = NormalizeToken(alias);

                    if (string.IsNullOrEmpty(normalized))
                        continue;

                    map[normalized] = definition.Role;
                }

                foreach (string alias in definition.Aliases)
                {
                    string collapsed = alias.Replace(" ", string.Empty, StringComparison.Ordinal);
                    string normalized = NormalizeToken(collapsed);

                    if (string.IsNullOrEmpty(normalized))
                        continue;

                    map[normalized] = definition.Role;
                }
            }

            return map;
        }

        private static string NormalizeToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return string.Empty;

            StringBuilder builder = new(token.Length);

            foreach (char c in token)
            {
                if (char.IsLetterOrDigit(c))
                    builder.Append(char.ToLowerInvariant(c));
            }

            return builder.ToString();
        }
    }
}
