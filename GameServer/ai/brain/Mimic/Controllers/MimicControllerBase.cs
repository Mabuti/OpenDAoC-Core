using DOL.AI.Brain;
using DOL.GS;
using DOL.GS.Spells;

namespace DOL.GS.Mimic.Controllers
{
    internal abstract class MimicControllerBase : IMimicController
    {
        protected readonly MimicBrain _brain;
        protected readonly MimicNPC _mimic;
        private bool _disposed;
        private bool _enabled;

        protected MimicControllerBase(MimicBrain brain, MimicNPC mimic)
        {
            _brain = brain;
            _mimic = mimic;
        }

        protected bool IsDisposed => _disposed;
        protected bool IsEnabled => _enabled;
        protected bool CanOperate => _enabled && !_disposed;

        public virtual void Dispose()
        {
            _disposed = true;
        }

        public virtual void OnRoleChanged(MimicRole role)
        {
            if (_disposed)
                return;

            _enabled = role != MimicRole.None;
        }

        public virtual void OnPreventCombatChanged(bool value) { }

        public virtual void OnPvPModeChanged(bool value) { }

        public virtual void OnGuardTargetChanged(GameLiving? target) { }

        public abstract void Think();

        public virtual bool TryHandleRoleBehaviors()
        {
            return false;
        }

        public virtual bool TryUpdateCombatOrder()
        {
            return false;
        }

        protected static bool OwnerShowsAggression(GameLiving? owner)
        {
            if (owner == null)
                return false;

            if (owner is GamePlayer player && player.IsAttacking)
                return true;

            if (owner.IsAttacking)
                return true;

            ISpellHandler? handler = owner.CurrentSpellHandler;
            return handler != null && handler.Spell.Target == eSpellTarget.ENEMY;
        }
    }
}
