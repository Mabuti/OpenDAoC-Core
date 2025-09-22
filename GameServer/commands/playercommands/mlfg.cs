using System;
using DOL.GS.Mimic;
using DOL.GS.PacketHandler;

namespace DOL.GS.Commands
{
    [CmdAttribute(
        "&mlfg",
        ePrivLevel.Player,
        "Display available mimic recruits or invite one by index",
        "/mlfg",
        "/mlfg <index>")]
    public sealed class MimicLookingForGroupCommand : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            if (client.Player == null)
                return;

            if (args.Length <= 1)
            {
                ShowTemplates(client);
                return;
            }

            if (!int.TryParse(args[1], out int index))
            {
                DisplayMessage(client, "Invalid index.");
                return;
            }

            var templates = MimicManager.GetTemplatesForPlayer(client.Player);
            if (index < 0 || index >= templates.Count)
            {
                DisplayMessage(client, "Index out of range.");
                return;
            }

            MimicTemplate template = templates[index];

            try
            {
                MimicNPC mimic = MimicManager.CreateMimic(client.Player, template, client.Player.Level);
                DisplayMessage(client, $"{mimic.Name} has joined your group.");
            }
            catch (Exception ex)
            {
                DisplayMessage(client, $"Unable to recruit mimic: {ex.Message}");
            }
        }

        private static void ShowTemplates(GameClient client)
        {
            var templates = MimicManager.GetTemplatesForPlayer(client.Player);

            if (templates.Count == 0)
            {
                client.Out.SendMessage("No mimics are currently available.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            for (int i = 0; i < templates.Count; i++)
            {
                MimicTemplate template = templates[i];
                client.Out.SendMessage($"[{i}] {template.DisplayName} (Level {template.MinimumLevel}-{template.MaximumLevel})", eChatType.CT_System, eChatLoc.CL_SystemWindow);
            }
        }
    }
}
