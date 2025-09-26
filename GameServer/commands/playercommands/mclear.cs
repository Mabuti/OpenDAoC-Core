using DOL.GS.Mimic;
using DOL.GS.PacketHandler;

namespace DOL.GS.Commands
{
    [CmdAttribute(
        "&mclear",
        ePrivLevel.Player,
        "Remove all active mimics from the world",
        "/mclear")]
    public sealed class MimicClearCommand : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            if (client.Player == null)
                return;

            int removed = MimicManager.ClearAllMimics();

            if (removed == 0)
            {
                DisplayMessage(client, "There are no active mimics to remove.");
            }
            else
            {
                DisplayMessage(client, $"Removed {removed} mimic{(removed == 1 ? string.Empty : "s")} from the world.");
            }
        }
    }
}
