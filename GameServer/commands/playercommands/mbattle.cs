using DOL.GS.Mimic;
using DOL.GS.PacketHandler;

namespace DOL.GS.Commands
{
    [CmdAttribute(
        "&mbattle",
        ePrivLevel.Player,
        "Control mimic battleground events",
        "/mbattle thid start",
        "/mbattle thid stop",
        "/mbattle thid clear")]
    public sealed class MimicBattleCommand : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            if (client.Player == null)
                return;

            if (args.Length < 3)
            {
                DisplaySyntax(client);
                return;
            }

            string region = args[1].ToLowerInvariant();
            if (region != "thid")
            {
                DisplayMessage(client, "Only the Thidranki region is supported.");
                return;
            }

            string action = args[2].ToLowerInvariant();
            switch (action)
            {
                case "start":
                    MimicManager.SetBattleState(region, true);
                    DisplayMessage(client, "Thidranki mimic battle started.");
                    break;
                case "stop":
                    MimicManager.SetBattleState(region, false);
                    DisplayMessage(client, "Thidranki mimic battle stopped.");
                    break;
                case "clear":
                    MimicManager.SetBattleState(region, false);
                    MimicManager.ClearAllMimics();
                    DisplayMessage(client, "Thidranki mimics cleared.");
                    break;
                default:
                    DisplaySyntax(client);
                    break;
            }
        }
    }
}
