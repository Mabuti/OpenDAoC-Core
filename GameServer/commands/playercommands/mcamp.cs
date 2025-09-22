using System;
using DOL.GS.Mimic;
using DOL.GS.PacketHandler;

namespace DOL.GS.Commands
{
    [CmdAttribute(
        "&mcamp",
        ePrivLevel.Player,
        "Manage mimic camp behavior",
        "/mcamp set",
        "/mcamp remove",
        "/mcamp aggrorange <1-6000>",
        "/mcamp filter <color>")]
    public sealed class MimicCampCommand : AbstractCommandHandler, ICommandHandler
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

            string action = args[1].ToLowerInvariant();

            switch (action)
            {
                case "set":
                    SetCamp(client);
                    break;
                case "remove":
                    MimicManager.ClearCamp(client.Player);
                    DisplayMessage(client, "Mimic camp cleared. Returning to follow behavior.");
                    break;
                case "aggrorange":
                    SetAggroRange(client, args);
                    break;
                case "filter":
                    SetFilter(client, args);
                    break;
                default:
                    DisplaySyntax(client);
                    break;
            }
        }

        private static void SetCamp(GameClient client)
        {
            Point3D ground = client.Player.GroundTarget;
            if (ground == null || (ground.X == 0 && ground.Y == 0 && ground.Z == 0))
            {
                client.Out.SendMessage("Set a ground target first.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            MimicGroupState state = MimicManager.GetOrCreateGroupState(client.Player);
            MimicManager.SetCamp(client.Player, new Point3D(ground), state.CampAggroRange, state.CampFilter);
            client.Out.SendMessage($"Mimic camp set to {ground} with aggro range {state.CampAggroRange}.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
        }

        private static void SetAggroRange(GameClient client, string[] args)
        {
            if (args.Length < 3 || !int.TryParse(args[2], out int range))
            {
                client.Out.SendMessage("Provide a numeric aggro range between 1 and 6000.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            range = Math.Clamp(range, 1, 6000);
            MimicManager.SetAggroRange(client.Player, range);
            client.Out.SendMessage($"Mimic camp aggro range set to {range}.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
        }

        private static void SetFilter(GameClient client, string[] args)
        {
            if (args.Length < 3)
            {
                client.Out.SendMessage("Provide a con color filter.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            if (!MimicManager.TryParseConColor(args[2], out ConColor color))
            {
                client.Out.SendMessage("Unknown con color. Use grey, green, blue, yellow, orange, red, or purple.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            MimicManager.SetFilter(client.Player, color);
            client.Out.SendMessage($"Mimic camp filter set to {color}.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
        }
    }
}
