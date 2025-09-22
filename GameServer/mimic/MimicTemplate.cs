using DOL.GS.PlayerClass;
using DOL.GS;

namespace DOL.GS.Mimic
{
    public sealed class MimicTemplate
    {
        public string Id { get; }
        public string DisplayName { get; }
        public eRealm Realm { get; }
        public eCharacterClass CharacterClass { get; }
        public ushort ModelId { get; }
        public byte MinimumLevel { get; }
        public byte MaximumLevel { get; }

        public MimicTemplate(string id, string displayName, eRealm realm, eCharacterClass characterClass, ushort modelId, byte minLevel, byte maxLevel)
        {
            Id = id;
            DisplayName = displayName;
            Realm = realm;
            CharacterClass = characterClass;
            ModelId = modelId;
            MinimumLevel = minLevel;
            MaximumLevel = maxLevel;
        }

        public bool SupportsLevel(int level)
        {
            return level >= MinimumLevel && level <= MaximumLevel;
        }
    }
}
