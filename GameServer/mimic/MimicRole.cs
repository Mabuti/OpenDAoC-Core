using System;
using System.Collections.Generic;
using System.Linq;

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
        private static readonly (string Alias, MimicRole Role)[] _aliases =
        {
            ("leader", MimicRole.Leader),
            ("puller", MimicRole.Puller),
            ("tank", MimicRole.Tank),
            ("cc", MimicRole.CrowdControl),
            ("crowdcontrol", MimicRole.CrowdControl),
            ("assist", MimicRole.Assist),
            ("healer", MimicRole.Healer),
            ("heal", MimicRole.Healer),
            ("support", MimicRole.Support),
            ("dps", MimicRole.DamageDealer),
            ("damage", MimicRole.DamageDealer),
            ("damagedealer", MimicRole.DamageDealer),
            ("scout", MimicRole.Scout)
        };

        private static readonly MimicRole[] _flagOrder =
        {
            MimicRole.Leader,
            MimicRole.Puller,
            MimicRole.Tank,
            MimicRole.CrowdControl,
            MimicRole.Assist,
            MimicRole.Healer,
            MimicRole.Support,
            MimicRole.DamageDealer,
            MimicRole.Scout
        };

        public static bool TryParse(string value, out MimicRole role)
        {
            role = MimicRole.None;

            if (string.IsNullOrWhiteSpace(value))
                return false;

            string[] tokens = value.Split(new[] { ',', '+', '|', '/', '\\', ' ' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string token in tokens)
            {
                string normalized = token.Trim().ToLowerInvariant();
                MimicRole? match = null;

                foreach ((string Alias, MimicRole Role) entry in _aliases)
                {
                    if (entry.Alias.Equals(normalized, StringComparison.Ordinal))
                    {
                        match = entry.Role;
                        break;
                    }
                }

                if (match == null)
                {
                    if (Enum.TryParse(normalized, true, out MimicRole parsed) && parsed != MimicRole.None)
                        match = parsed;
                }

                if (match == null)
                {
                    role = MimicRole.None;
                    return false;
                }

                role |= match.Value;
            }

            return role != MimicRole.None;
        }

        public static string GetSyntax()
        {
            return string.Join('|', _flagOrder
                .Where(flag => flag != MimicRole.None)
                .Select(ToCommandName));
        }

        public static string ToDisplayString(MimicRole role)
        {
            if (role == MimicRole.None)
                return "None";

            List<string> parts = new();

            foreach (MimicRole flag in _flagOrder)
            {
                if (flag == MimicRole.None)
                    continue;

                if (role.HasFlag(flag))
                    parts.Add(ToDisplayName(flag));
            }

            return parts.Count > 0 ? string.Join(", ", parts) : "None";
        }

        private static string ToDisplayName(MimicRole role)
        {
            return role switch
            {
                MimicRole.Leader => "Leader",
                MimicRole.Puller => "Puller",
                MimicRole.Tank => "Tank",
                MimicRole.CrowdControl => "Crowd Control",
                MimicRole.Assist => "Assist",
                MimicRole.Healer => "Healer",
                MimicRole.Support => "Support",
                MimicRole.DamageDealer => "Damage Dealer",
                MimicRole.Scout => "Scout",
                _ => role.ToString()
            };
        }

        private static string ToCommandName(MimicRole role)
        {
            return role switch
            {
                MimicRole.Leader => "leader",
                MimicRole.Puller => "puller",
                MimicRole.Tank => "tank",
                MimicRole.CrowdControl => "cc",
                MimicRole.Assist => "assist",
                MimicRole.Healer => "healer",
                MimicRole.Support => "support",
                MimicRole.DamageDealer => "dps",
                MimicRole.Scout => "scout",
                _ => role.ToString().ToLowerInvariant()
            };
        }
    }
}
