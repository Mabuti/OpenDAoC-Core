using System;
using System.Linq;
using DOL.GS.Mimic;
using DOL.GS.PacketHandler;

namespace DOL.GS.Commands
{
    [CmdAttribute(
        "&mrole",
        ePrivLevel.Player,
        "Assign roles to mimics",
        "/mrole <role[,role...]>")]
    public sealed class MimicRoleCommand : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            if (client.Player == null)
                return;

            if (args.Length < 2)
            {
                DisplaySyntax(client);
                DisplayMessage(client, $"Available roles: {MimicRoleInfo.GetSyntax()}.");
                return;
            }

            string requested = string.Join(' ', args.Skip(1));
            if (!MimicRoleInfo.TryParse(requested, out MimicRole role))
            {
                DisplayMessage(client, $"Unknown role. Available roles: {MimicRoleInfo.GetSyntax()}.");
                return;
            }

            MimicNPC? mimic = GetTargetMimic(client.Player);
            if (mimic == null)
            {
                DisplayMessage(client, "Target a mimic in your group first.");
                return;
            }

            MimicManager.AssignRole(mimic, role);
            DisplayMessage(client, $"{mimic.Name} role updated to {MimicRoleInfo.ToDisplayString(role)}.");
        }

        private static MimicNPC? GetTargetMimic(GamePlayer player)
        {
            if (player.TargetObject is MimicNPC mimic)
                return mimic;

            return null;
        }

    }
}
