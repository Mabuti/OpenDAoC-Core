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
        private const int CAMP_SCAN_INTERVAL = 2000;

        private static readonly Logger log = LoggerManager.Create(typeof(MimicBrain));

        private readonly MimicNPC _mimic;
        private readonly DOLEventHandler _ownerAttackedHandler;
        private bool _preventCombat;
        private bool _pvpMode;
        private GameLiving? _guardTarget;
        private GameLiving? _activeTarget;
        private GameLiving? _campTarget;
        private long _ownerThreatExpires;
        private long _nextCampScan;
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
            HandleGuardCampAndFollow();
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

        private void HandleGuardCampAndFollow()
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

            if (_mimic.GroupState.Camp is MimicCampSettings camp)
            {
                Point3D mimicPosition = new Point3D(_mimic.X, _mimic.Y, _mimic.Z);

                if (!mimicPosition.IsWithinRadius(camp.Location, 150))
                {
                    WalkState = eWalkState.Stay;
                    _mimic.StopFollowing();
                    _mimic.WalkTo(camp.Location, _mimic.MaxSpeed);
                    LogInstruction("Moving to camp location.");
                }
                else if (_mimic.IsMoving)
                {
                    _mimic.StopMoving();
                }

                return;
            }

            if (Owner != null && !_mimic.IsWithinRadius(Owner, 350))
            {
                WalkState = eWalkState.Follow;
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
            _campTarget = ValidateTarget(_campTarget);

            if (_activeTarget != null)
            {
                OrderedAttackTarget = _activeTarget;
                LogInstruction($"Maintaining engagement on {_activeTarget.Name}.");
                return;
            }

            GameLiving? campTarget = EvaluateCampTarget();

            if (campTarget != null)
            {
                EngageTarget(campTarget);
                LogInstruction($"Engaging camp target {campTarget.Name}.");
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

            if (!ShouldAssistOwner(owner, ownerTarget))
            {
                if (!HasAggro)
                {
                    ClearCombatOrders(forceDisengage: false);
                    LogInstruction("Owner not aggressive; waiting for engagement signal.");
                }

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
                    LogInstruction($"Owner under attack by {target.Name}; retaliating.", force: true);
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
                OrderedAttackTarget = target;
                return;
            }

            if (_activeTarget != null)
                RemoveFromAggroList(_activeTarget);

            _activeTarget = target;
            OrderedAttackTarget = target;
            AddToAggroList(target, 1);
            LogInstruction($"Engaging {target.Name}.");
        }

        private GameLiving? EvaluateCampTarget()
        {
            MimicCampSettings? camp = _mimic.GroupState.Camp;

            if (camp == null)
            {
                _campTarget = null;
                return null;
            }

            _campTarget = ValidateTarget(_campTarget);

            if (_campTarget != null)
                return _campTarget;

            if (GameLoop.GameLoopTime < _nextCampScan)
                return null;

            _nextCampScan = GameLoop.GameLoopTime + CAMP_SCAN_INTERVAL;

            if (_mimic.CurrentRegion is not Region region)
                return null;

            int radius = Math.Clamp(camp.AggroRange, 1, 6000);
            bool ownerThreatened = IsOwnerUnderThreat();

            foreach (GamePlayer player in region.GetPlayersInRadius(camp.Location, (ushort)radius))
            {
                if (!player.IsAlive)
                    continue;

                if (!GameServer.ServerRules.IsAllowedToAttack(_mimic, player, true))
                    continue;

                _campTarget = player;
                return _campTarget;
            }

            if (_pvpMode && !ownerThreatened)
                return null;

            foreach (GameNPC npc in region.GetNPCsInRadius(camp.Location, (ushort)radius))
            {
                if (npc == _mimic || !npc.IsAlive)
                    continue;

                if (!GameServer.ServerRules.IsAllowedToAttack(_mimic, npc, true))
                    continue;

                if (camp.HasFilter)
                {
                    ConColor color = ConLevels.GetConColor(_mimic.GetConLevel(npc));

                    if (color < camp.MinimumCon)
                        continue;
                }

                _campTarget = npc;
                return _campTarget;
            }

            return null;
        }

        private bool ShouldAssistOwner(GameLiving owner, GameLiving target)
        {
            if (_pvpMode)
            {
                if (target is GamePlayer)
                    return true;

                return IsOwnerUnderThreat() || HasAggro;
            }

            if (OwnerIsAggressive(owner))
                return true;

            return HasAggro;
        }

        private void LogInstruction(string instruction, bool force = false)
        {
            string message = $"[{_mimic.Name}] {instruction}";

            if (!force && string.Equals(_lastInstruction, message, StringComparison.Ordinal))
                return;

            _lastInstruction = message;

            if (log.IsInfoEnabled)
                log.Info(message);

            Console.WriteLine(message);
        }
    }
}
