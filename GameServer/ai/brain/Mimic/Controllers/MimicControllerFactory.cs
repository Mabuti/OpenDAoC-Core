using DOL.GS.PlayerClass;

namespace DOL.GS.Mimic.Controllers
{
    internal static class MimicControllerFactory
    {
        public static IMimicController? Create(MimicBrain brain, MimicNPC mimic)
        {
            return mimic.Template.CharacterClass switch
            {
                eCharacterClass.Warrior => new WarriorMimicController(brain, mimic),
                _ => null
            };
        }
    }
}
