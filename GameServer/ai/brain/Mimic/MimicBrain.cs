using System;
using DOL.AI.Brain;
using DOL.Events;
using DOL.GS;
using DOL.GS.Spells;

namespace DOL.GS.Mimic
{
    public class MimicBrain : FollowOwnerBrain
    {
        private const int OWNER_THREAT_DURATION = 8000;

        private readonly MimicNPC _mimic;
        private readonly DOLEventHandler _ownerAttackedHandler;
        private bool _preventCombat;
        private bool _pvpMode;
        private GameLiving? _guardTarget;
        private GameLiving? _activeTarget;
        private long _ownerThreatExpires;

        public MimicBrain(GameLiving owner, MimicNPC mimic) : base(owner)
        {
            _mimic = mimic;
            _ownerAttackedHandler = new DOLEventHandler(OnOwnerAttacked);
            GameEventMgr.AddHandler(owner, GameLivingEvent.AttackedByEnemy, _ownerAttackedHandler);
        }

        public override void Think()
        {
            base.Think();
            HandleGuardAndFollow();
        }

        public override void FollowOwner()
        {
            // Default implementation disengages whenever the mimic is attacking which prevents
            // them from fighting. Guard and follow logic is handled in HandleGuardAndFollow().
        }

        public override void AttackMostWanted()
        {
            if (!IsActive)
                return;

            if (_preventCombat)
            {
                if (_mimic.IsAttacking)
                    Disengage();

                if (HasAggro)
                    ClearAggroList();

                ClearActiveTarget();
                return;
            }

            GameLiving? target = SelectTarget();

            if (target != null)
            {
                if (_mimic.TargetObject != target)
                    _mimic.TargetObject = target;

                if (!_mimic.IsAttacking)
                    _mimic.StartAttack(target);

                return;
            }

            if (_mimic.IsAttacking)
                Disengage();

            ClearActiveTarget();
            base.AttackMostWanted();
        }

        public void SetPreventCombat(bool value)
        {
            _preventCombat = value;

            if (_preventCombat && _mimic.IsAttacking)
                Disengage();
        }

        public void SetPvPMode(bool value)
        {
            _pvpMode = value;
        }

        public void SetGuardTarget(GameLiving? target)
        {
            _guardTarget = target;
        }

        public void Dispose()
        {
            GameEventMgr.RemoveHandler(Owner, GameLivingEvent.AttackedByEnemy, _ownerAttackedHandler);
        }

        private void HandleGuardAndFollow()
        {
            if (_guardTarget != null)
            {
                if (_guardTarget.ObjectState == GameObject.eObjectState.Active && _guardTarget.IsAlive)
                {
                    if (!_mimic.IsWithinRadius(_guardTarget, 150))
                        _mimic.Follow(_guardTarget, 120, 320);

                    return;
                }

                _guardTarget = null;
            }

            if (_mimic.IsAttacking)
                return;

            if (Owner != null && !_mimic.IsWithinRadius(Owner, 350))
                _mimic.Follow(Owner, 150, 350);
        }

        private GameLiving? SelectTarget()
        {
            GameLiving? previousTarget = _activeTarget;
            _activeTarget = ValidateTarget(_activeTarget);

            if (_activeTarget == null && previousTarget != null)
                RemoveFromAggroList(previousTarget);

            if (_activeTarget != null)
                return _activeTarget;

            if (Owner is not GameLiving owner)
                return null;

            GameLiving? ownerTarget = ValidateTarget(owner.TargetObject as GameLiving);

            if (ownerTarget == null)
                return null;

            if (_pvpMode)
            {
                if (!IsOwnerUnderThreat() && !_mimic.IsAttacking)
                    return null;
            }
            else if (!_mimic.IsAttacking && !OwnerIsAggressive(owner))
            {
                return null;
            }

            EngageTarget(ownerTarget);
            return _activeTarget;
        }

        private GameLiving? ValidateTarget(GameLiving? target)
        {
            if (target == null)
                return null;

            if (!target.IsAlive || target.ObjectState != GameObject.eObjectState.Active)
                return null;

            return GameServer.ServerRules.IsAllowedToAttack(_mimic, target, true) ? target : null;
        }

        private static bool OwnerIsAggressive(GameLiving owner)
        {
            if (owner.IsAttacking)
                return true;

            ISpellHandler? spellHandler = owner.CurrentSpellHandler;
            return spellHandler != null && spellHandler.Spell.Target == eSpellTarget.ENEMY;
        }

        private bool IsOwnerUnderThreat()
        {
            return _ownerThreatExpires > GameLoop.GameLoopTime;
        }

        private void OnOwnerAttacked(DOLEvent e, object sender, EventArgs args)
        {
            if (args is AttackedByEnemyEventArgs { AttackData.Attacker: GameLiving attacker })
            {
                GameLiving? target = ValidateTarget(attacker);

                if (target != null)
                    EngageTarget(target);
            }

            _ownerThreatExpires = GameLoop.GameLoopTime + OWNER_THREAT_DURATION;
        }

        private void ClearActiveTarget()
        {
            if (_activeTarget != null)
                RemoveFromAggroList(_activeTarget);

            _activeTarget = null;
        }

        private void EngageTarget(GameLiving target)
        {
            if (_activeTarget == target)
                return;

            _activeTarget = target;
            AddToAggroList(target, 1);
        }
    }
}
