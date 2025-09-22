using System;
using DOL.GS.Mimic;
using DOL.GS.PacketHandler;

namespace DOL.GS.Commands
{
    [CmdAttribute(
        "&mgroup",
        ePrivLevel.Player,
        "Summon a group of mimics",
        "/mgroup [realm] [amount] [level]")]
    public sealed class MimicGroupCommand : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            if (client.Player == null)
                return;

            eRealm realm = client.Player.Realm;
            int count = 8;
            int level = client.Player.Level;

            if (args.Length >= 2 && !Enum.TryParse(args[1], true, out realm))
            {
                DisplayMessage(client, "Unknown realm. Use Albion, Midgard, or Hibernia.");
                return;
            }

            if (args.Length >= 3 && (!int.TryParse(args[2], out count) || count <= 0))
            {
                DisplayMessage(client, "Specify a positive amount.");
                return;
            }

            if (args.Length >= 4 && !int.TryParse(args[3], out level))
            {
                DisplayMessage(client, "Invalid level specified.");
                return;
            }

            var templates = MimicManager.GetTemplatesForRealm(realm);
            if (templates.Count == 0)
            {
                DisplayMessage(client, "No mimic templates available for that realm.");
                return;
            }

            int created = 0;
            for (int i = 0; i < count; i++)
            {
                MimicTemplate template = templates[i % templates.Count];
                try
                {
                    MimicManager.CreateMimic(client.Player, template, level);
                    created++;
                }
                catch (Exception ex)
                {
                    DisplayMessage(client, $"Failed to summon {template.DisplayName}: {ex.Message}");
                    break;
                }
            }

            DisplayMessage(client, $"Summoned {created} mimics.");
        }
    }
}
