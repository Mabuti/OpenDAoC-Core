using System;
using DOL.GS.Mimic;
using DOL.GS.PacketHandler;

namespace DOL.GS.Commands
{
    [CmdAttribute(
        "&mpc",
        ePrivLevel.Player,
        "Toggle mimic prevent combat",
        "/mpc <true|false>")]
    public sealed class MimicPreventCombatCommand : AbstractCommandHandler, ICommandHandler
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
                MimicManager.SetPreventCombat(mimic, value);
                DisplayMessage(client, $"{mimic.Name} prevent combat set to {value}.");
            }
            else
            {
                MimicManager.SetPreventCombat(client.Player, value);
                DisplayMessage(client, $"Set prevent combat for all grouped mimics to {value}.");
            }
        }
    }
}
