using System;
using DOL.GS;

namespace DOL.GS.Mimic.Controllers
{
    internal interface IMimicController : IDisposable
    {
        void OnRoleChanged(MimicRole role);
        void OnPreventCombatChanged(bool value);
        void OnPvPModeChanged(bool value);
        void OnGuardTargetChanged(GameLiving? target);
        void Think();
        bool TryHandleRoleBehaviors();
        bool TryUpdateCombatOrder();
    }
}
