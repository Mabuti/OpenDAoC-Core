using System;
using DOL.GS.Mimic;
using DOL.GS.PacketHandler;

namespace DOL.GS.Commands
{
    [CmdAttribute(
        "&mrole",
        ePrivLevel.Player,
        "Assign roles to mimics",
        "/mrole <leader|puller|tank|cc|assist>")]
    public sealed class MimicRoleCommand : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            if (client.Player == null)
                return;

            if (args.Length < 2)
            {
                DisplaySyntax(client);
                return;
            }

            MimicRole role = ParseRole(args[1]);
            if (role == MimicRole.None)
            {
                DisplayMessage(client, "Unknown role.");
                return;
            }

            MimicNPC? mimic = GetTargetMimic(client.Player);
            if (mimic == null)
            {
                DisplayMessage(client, "Target a mimic in your group first.");
                return;
            }

            MimicManager.AssignRole(mimic, role);
            DisplayMessage(client, $"{mimic.Name} role set to {role}.");
        }

        private static MimicNPC? GetTargetMimic(GamePlayer player)
        {
            if (player.TargetObject is MimicNPC mimic)
                return mimic;

            return null;
        }

        private static MimicRole ParseRole(string value)
        {
            return value.ToLowerInvariant() switch
            {
                "leader" => MimicRole.Leader,
                "puller" => MimicRole.Puller,
                "tank" => MimicRole.Tank,
                "cc" => MimicRole.CrowdControl,
                "assist" => MimicRole.Assist,
                _ => MimicRole.None
            };
        }
    }
}
