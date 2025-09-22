using System;
using DOL.GS.Mimic;
using DOL.GS.PacketHandler;

namespace DOL.GS.Commands
{
    [CmdAttribute(
        "&mpvp",
        ePrivLevel.Player,
        "Toggle mimic PvP mode",
        "/mpvp <true|false>")]
    public sealed class MimicPvpCommand : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            if (client.Player == null)
                return;

            if (args.Length < 2 || !bool.TryParse(args[1], out bool value))
            {
                DisplaySyntax(client);
                return;
            }

            if (client.Player.TargetObject is MimicNPC mimic)
            {
                MimicManager.SetPvPMode(mimic, value);
                DisplayMessage(client, $"{mimic.Name} PvP mode set to {value}.");
            }
            else
            {
                MimicManager.SetPvPMode(client.Player, value);
                DisplayMessage(client, $"Set PvP mode for all grouped mimics to {value}.");
            }
        }
    }
}
