using System;
using DOL.GS.Mimic;
using DOL.GS.PacketHandler;
using DOL.GS;

namespace DOL.GS.Commands
{
    [CmdAttribute(
        "&mguard",
        ePrivLevel.Player,
        "Order a mimic to guard a friendly target",
        "/mguard",
        "/mguard <name>")]
    public sealed class MimicGuardCommand : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            if (client.Player == null)
                return;

            MimicNPC? mimic = client.Player.TargetObject as MimicNPC;
            if (mimic == null)
            {
                DisplayMessage(client, "Target a mimic to assign guard duty.");
                return;
            }

            GameLiving target = client.Player;
            if (args.Length > 1)
            {
                string name = string.Join(' ', args, 1, args.Length - 1);
                target = FindGuardTarget(client.Player, name) ?? target;
            }

            MimicManager.GuardTarget(mimic, target);
            DisplayMessage(client, $"{mimic.Name} now guards {target.Name}.");
        }

        private static GameLiving? FindGuardTarget(GamePlayer player, string name)
        {
            if (player.Group != null)
            {
                foreach (GameLiving member in player.Group.GetMembersInTheGroup())
                {
                    if (member.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                        return member;
                }
            }

            return null;
        }
    }
}
