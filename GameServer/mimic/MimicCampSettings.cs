using DOL.GS;

namespace DOL.GS.Mimic
{
    public class MimicCampSettings
    {
        public Point3D Location { get; }
        public int AggroRange { get; }
        public ConColor MinimumCon { get; }
        public bool HasFilter => MinimumCon != ConColor.UNKNOWN;

        public MimicCampSettings(Point3D location, int aggroRange, ConColor minimumCon)
        {
            Location = location;
            AggroRange = aggroRange;
            MinimumCon = minimumCon;
        }
    }
}
