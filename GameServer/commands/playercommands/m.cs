using System;
using DOL.GS.Mimic;
using DOL.GS.PacketHandler;

namespace DOL.GS.Commands
{
    [CmdAttribute(
        "&m",
        ePrivLevel.Player,
        "Summon a mimic by class",
        "/m <classname> [level]")]
    public sealed class MimicSummonSingleCommand : AbstractCommandHandler, ICommandHandler
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

            string className = args[1];
            int level = client.Player.Level;
            if (args.Length >= 3 && !int.TryParse(args[2], out level))
            {
                DisplayMessage(client, "Invalid level supplied.");
                return;
            }

            MimicTemplate? template = MimicManager.FindTemplateByClass(className);
            if (template == null)
            {
                DisplayMessage(client, "No mimic template matches that class.");
                return;
            }

            try
            {
                MimicNPC mimic = MimicManager.CreateMimic(client.Player, template, level);
                DisplayMessage(client, $"Summoned {mimic.Name} at level {mimic.Level}.");
            }
            catch (Exception ex)
            {
                DisplayMessage(client, $"Failed to summon mimic: {ex.Message}");
            }
        }
    }
}
