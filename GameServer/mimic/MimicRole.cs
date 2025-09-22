using System;

namespace DOL.GS.Mimic
{
    [Flags]
    public enum MimicRole
    {
        None = 0,
        Leader = 1 << 0,
        Puller = 1 << 1,
        Tank = 1 << 2,
        CrowdControl = 1 << 3,
        Assist = 1 << 4
    }
}
