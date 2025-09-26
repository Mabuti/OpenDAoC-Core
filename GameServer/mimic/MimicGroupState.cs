using System.Collections.Generic;
using DOL.GS;

namespace DOL.GS.Mimic
{
    public class MimicGroupState
    {
        private readonly List<MimicNPC> _members = new();

        public GamePlayer Owner { get; }
        public MimicCampSettings? Camp { get; private set; }
        public ConColor CampFilter { get; private set; } = ConColor.UNKNOWN;
        public int CampAggroRange { get; private set; }

        public IReadOnlyList<MimicNPC> Members => _members;

        public MimicGroupState(GamePlayer owner)
        {
            Owner = owner;
            CampAggroRange = owner.CurrentZone?.IsDungeon ?? false ? 250 : 550;
        }

        public void AddMember(MimicNPC mimic)
        {
            if (!_members.Contains(mimic))
                _members.Add(mimic);
        }

        public void RemoveMember(MimicNPC mimic)
        {
            _members.Remove(mimic);
        }

        public void SetCamp(MimicCampSettings camp)
        {
            Camp = camp;
            CampAggroRange = camp.AggroRange;
            CampFilter = camp.MinimumCon;
        }

        public void ClearCamp()
        {
            Camp = null;
        }

        public void SetAggroRange(int range)
        {
            CampAggroRange = range;
        }

        public void SetFilter(ConColor color)
        {
            CampFilter = color;
        }

        public MimicNPC? GetLeader()
        {
            foreach (MimicNPC member in _members)
            {
                if (member.Role.HasFlag(MimicRole.Leader) && member.IsAlive)
                    return member;
            }

            return null;
        }
    }
}
