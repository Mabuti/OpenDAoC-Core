using System;
using DOL.AI.Brain;
using DOL.Events;
using DOL.GS;
using DOL.GS.Spells;
using DOL.Logging;

namespace DOL.GS.Mimic
{
    public class MimicBrain : FollowOwnerBrain
    {
        private const int OWNER_THREAT_DURATION = 8000;

        private static readonly Logger log = LoggerManager.Create(typeof(MimicBrain));

        private readonly MimicNPC _mimic;
        private readonly DOLEventHandler _ownerAttackedHandler;
        private bool _preventCombat;
        private bool _pvpMode;
        private GameLiving? _guardTarget;
        private GameLiving? _activeTarget;
        private long _ownerThreatExpires;
        private string? _lastInstruction;

        public MimicBrain(GameLiving owner, MimicNPC mimic) : base(owner)
        {
            _mimic = mimic;
            _ownerAttackedHandler = new DOLEventHandler(OnOwnerAttacked);
            GameEventMgr.AddHandler(owner, GameLivingEvent.AttackedByEnemy, _ownerAttackedHandler);

            AggressionState = eAggressionState.Defensive;
        }

        public override void Think()
        {
            UpdateCombatOrder();
            base.Think();
            HandleGuardAndFollow();
        }

        public override void FollowOwner()
        {
            // Default implementation disengages whenever the mimic is attacking which prevents
            // them from fighting. Guard and follow logic is handled in HandleGuardAndFollow().
        }

        public void SetPreventCombat(bool value)
        {
            _preventCombat = value;

            if (_preventCombat)
            {
                AggressionState = eAggressionState.Passive;

                ClearCombatOrders(forceDisengage: true);
                LogInstruction("PreventCombat enabled; standing down and disengaging.", force: true);
            }
            else
            {
                AggressionState = eAggressionState.Defensive;
                LogInstruction("PreventCombat disabled; returning to Defensive aggression.", force: true);
            }
        }

        public void SetPvPMode(bool value)
        {
            _pvpMode = value;
            LogInstruction($"PvP mode {(value ? "enabled" : "disabled")}; adjusting engagement rules.", force: true);
        }

        public void SetGuardTarget(GameLiving? target)
        {
            _guardTarget = target;
            if (target != null)
                LogInstruction($"Guarding {target.Name}; prioritizing their safety.", force: true);
            else
                LogInstruction("Guard target cleared; following owner normally.", force: true);
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
                    {
                        _mimic.Follow(_guardTarget, 120, 320);
                        LogInstruction($"Moving to guard {_guardTarget.Name}.");
                    }

                    return;
                }

                _guardTarget = null;
                LogInstruction("Guard target lost; resuming owner follow.");
            }

            if (_mimic.IsAttacking)
                return;

            if (Owner != null && !_mimic.IsWithinRadius(Owner, 350))
            {
                _mimic.Follow(Owner, 150, 350);
                LogInstruction($"Following owner {Owner.Name}.");
            }
        }

        private void UpdateCombatOrder()
        {
            if (!IsActive)
                return;

            if (_preventCombat)
            {
                ClearCombatOrders(forceDisengage: true);
                LogInstruction("Combat prevented; no attack orders will be issued.");
                return;
            }

            _activeTarget = ValidateTarget(_activeTarget);

            if (_activeTarget != null)
            {
                OrderedAttackTarget = _activeTarget;
                LogInstruction($"Maintaining engagement on {_activeTarget.Name}.");
                return;
            }

            if (Owner is not GameLiving owner)
            {
                ClearCombatOrders(forceDisengage: false);
                LogInstruction("Owner missing; clearing combat orders.");
                return;
            }

            GameLiving? ownerTarget = ValidateTarget(owner.TargetObject as GameLiving);

            if (ownerTarget == null)
            {
                ClearCombatOrders(forceDisengage: false);
                LogInstruction("Owner has no valid target; holding position.");
                return;
            }

            if (_pvpMode)
            {
                if (!IsOwnerUnderThreat() && !_mimic.IsAttacking && !HasAggro)
                {
                    ClearCombatOrders(forceDisengage: false);
                    LogInstruction("PvP mode idle; waiting for threat before engaging.");
                    return;
                }
            }
            else if (!_mimic.IsAttacking && !HasAggro && !OwnerIsAggressive(owner))
            {
                ClearCombatOrders(forceDisengage: false);
                LogInstruction("Owner not aggressive; waiting for engagement signal.");
                return;
            }

            EngageTarget(ownerTarget);
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
            _ownerThreatExpires = GameLoop.GameLoopTime + OWNER_THREAT_DURATION;

            if (_preventCombat || !IsActive)
                return;

            if (args is AttackedByEnemyEventArgs { AttackData.Attacker: GameLiving attacker })
            {
                GameLiving? target = ValidateTarget(attacker);

                if (target != null)
                {
                    EngageTarget(target);
                    LogInstruction($"Owner under attack by {target.Name}; retaliating.");
                }
            }
        }

        private void ClearCombatOrders(bool forceDisengage)
        {
            GameLiving? previousTarget = _activeTarget;
            _activeTarget = null;
            OrderedAttackTarget = null;

            if (previousTarget != null)
            {
                RemoveFromAggroList(previousTarget);
                LogInstruction($"Clearing aggro on {previousTarget.Name}.");
            }

            if (forceDisengage)
            {
                if (_mimic.IsAttacking || HasAggro)
                {
                    Disengage();
                    LogInstruction("Disengaging from combat.");
                }
            }
            else if (_mimic.IsAttacking && !HasAggro)
            {
                Disengage();
                LogInstruction("No aggro remaining; disengaging.");
            }
        }

        private void EngageTarget(GameLiving target)
        {
            if (AggressionState == eAggressionState.Passive)
                AggressionState = eAggressionState.Defensive;

            if (_activeTarget == target)
            {
                LogInstruction($"Already engaging {target.Name}; maintaining attack.");
                return;
            }

            if (_activeTarget != null)
                RemoveFromAggroList(_activeTarget);

            _activeTarget = target;
            OrderedAttackTarget = target;
            AddToAggroList(target, 1);
            LogInstruction($"Engaging {target.Name} at owner's direction.");
        }

        private void LogInstruction(string instruction, bool force = false)
        {
            if (!log.IsInfoEnabled)
                return;

            string message = $"[{_mimic.Name}] {instruction}";

            if (!force && string.Equals(_lastInstruction, message, StringComparison.Ordinal))
                return;

            _lastInstruction = message;
            log.Info(message);
        }
    }
}
