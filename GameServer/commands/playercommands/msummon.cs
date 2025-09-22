using DOL.GS.Mimic;
using DOL.GS.PacketHandler;

namespace DOL.GS.Commands
{
    [CmdAttribute(
        "&msummon",
        ePrivLevel.Player,
        "Summon mimic party members to your location")]
    public sealed class MimicSummonCommand : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            if (client.Player == null)
                return;

            MimicManager.SummonGroup(client.Player);
            DisplayMessage(client, "Mimics summoned to your location.");
        }
    }
}
