using System;
using System.Collections.Generic;
using DOL.AI.Brain;
using DOL.Events;
using DOL.GS;
using DOL.GS.PacketHandler;
using DOL.GS.Spells;
using DOL.GS.Mimic.Controllers;
using DOL.Logging;

namespace DOL.GS.Mimic
{
    public class MimicBrain : FollowOwnerBrain
    {
        private const int OWNER_THREAT_DURATION = 8000;
        private const int CAMP_SCAN_INTERVAL = 2000;

        private static readonly Logger log = LoggerManager.Create(typeof(MimicBrain));
        private static readonly SpellLine s_mobSpellLine = SkillBase.GetSpellLine(GlobalSpellsLines.Mob_Spells) ?? throw new InvalidOperationException("Missing Mob spell line.");
        internal static SpellLine BehaviorSpellLine => s_mobSpellLine;

        private readonly MimicNPC _mimic;
        private readonly IMimicController? _controller;
        private readonly DOLEventHandler _ownerAttackedHandler;
        private bool _preventCombat;
        private bool _pvpMode;
        private GameLiving? _guardTarget;
        private GameLiving? _activeTarget;
        private GameLiving? _campTarget;
        private long _ownerThreatExpires;
        private long _nextCampScan;
        private string? _lastInstruction;
        private bool _isLeader;
        private bool _isPuller;
        private bool _isTank;
        private bool _isCrowdControl;
        private bool _isAssist;
        private bool _isHealer;
        private bool _isSupport;
        private bool _isDamageDealer;
        private bool _isScout;
        private long _suppressCombatUntil;
        private long _nextCrowdControlCheck;
        private long _nextScoutPulse;
        private string? _lastScoutReport;
        private bool _groupInCombat;
        private GameLiving? _groupAssistTarget;

        public MimicBrain(GameLiving owner, MimicNPC mimic) : base(owner)
        {
            _mimic = mimic;
            _ownerAttackedHandler = new DOLEventHandler(OnOwnerAttacked);
            GameEventMgr.AddHandler(owner, GameLivingEvent.AttackedByEnemy, _ownerAttackedHandler);

            AggressionState = eAggressionState.Defensive;
            _controller = MimicControllerFactory.Create(this, mimic);
        }

        public override void Think()
        {
            EvaluateGroupCombatState();
            _controller?.Think();
            HandleRoleBehaviors();
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

            _controller?.OnPreventCombatChanged(value);
        }

        public void SetPvPMode(bool value)
        {
            _pvpMode = value;
            LogInstruction($"PvP mode {(value ? "enabled" : "disabled")}; adjusting engagement rules.", force: true);
            _controller?.OnPvPModeChanged(value);
        }

        public void SetGuardTarget(GameLiving? target)
        {
            _guardTarget = target;
            if (target != null)
                LogInstruction($"Guarding {target.Name}; prioritizing their safety.", force: true);
            else
                LogInstruction("Guard target cleared; following owner normally.", force: true);

            _controller?.OnGuardTargetChanged(target);
        }

        public void Dispose()
        {
            GameEventMgr.RemoveHandler(Owner, GameLivingEvent.AttackedByEnemy, _ownerAttackedHandler);
            _controller?.Dispose();
        }

        public void OnRoleChanged(MimicRole role)
        {
            _isLeader = role.HasFlag(MimicRole.Leader);
            _isPuller = role.HasFlag(MimicRole.Puller);
            _isTank = role.HasFlag(MimicRole.Tank);
            _isCrowdControl = role.HasFlag(MimicRole.CrowdControl);
            _isAssist = role.HasFlag(MimicRole.Assist);
            _isHealer = role.HasFlag(MimicRole.Healer);
            _isSupport = role.HasFlag(MimicRole.Support);
            _isDamageDealer = role.HasFlag(MimicRole.DamageDealer) || _isAssist;
            _isScout = role.HasFlag(MimicRole.Scout);

            if (_isTank && Owner != null)
            {
                _mimic.SetGuardTarget(Owner);
            }
            else if (!_isTank && _guardTarget == Owner)
            {
                _mimic.SetGuardTarget(null);
            }

            _suppressCombatUntil = 0;
            _nextCrowdControlCheck = 0;
            _nextScoutPulse = 0;
            _lastScoutReport = null;

            LogInstruction($"Role updated to {MimicRoleInfo.ToDisplayString(role)}.", force: true);
            _controller?.OnRoleChanged(role);
        }

        private void HandleRoleBehaviors()
        {
            if (!IsActive)
                return;

            if (_controller != null && _controller.TryHandleRoleBehaviors())
                return;

            bool performedSupportAction = false;

            if ((_isHealer || _isSupport) && CheckSpells(StandardMobBrain.eCheckSpellType.Defensive))
            {
                _suppressCombatUntil = Math.Max(_suppressCombatUntil, GameLoop.GameLoopTime + 1500);
                performedSupportAction = true;
            }

            if (_isCrowdControl && TryCrowdControlAction())
            {
                _suppressCombatUntil = Math.Max(_suppressCombatUntil, GameLoop.GameLoopTime + 2000);
                performedSupportAction = true;
            }

            if (_isScout)
                PerformScoutSweep();

            if (performedSupportAction && _preventCombat)
                _suppressCombatUntil = Math.Max(_suppressCombatUntil, GameLoop.GameLoopTime + 500);
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

            if (_groupInCombat)
                return;

            MimicCampSettings? camp = _mimic.GroupState.Camp;

            if (camp != null)
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

            if (camp == null)
            {
                MimicNPC? leader = _mimic.GroupState.GetLeader();

                if (!_isLeader && leader != null && leader != _mimic && leader.IsAlive)
                {
                    EnsureFollowTarget(leader, 150, 280, $"Maintaining formation with leader {leader.Name}.");
                    return;
                }
            }

            if (Owner != null)
                EnsureFollowTarget(Owner, 150, 350, $"Following owner {Owner.Name}.");
        }

        private void UpdateCombatOrder()
        {
            if (!IsActive)
                return;

            if (_suppressCombatUntil > GameLoop.GameLoopTime)
            {
                if (!HasAggro)
                    ClearCombatOrders(forceDisengage: false);

                return;
            }

            if (_preventCombat)
            {
                ClearCombatOrders(forceDisengage: true);
                LogInstruction("Combat prevented; no attack orders will be issued.");
                return;
            }

            _activeTarget = ValidateTarget(_activeTarget);
            _campTarget = ValidateTarget(_campTarget);
            _groupAssistTarget = ValidateTarget(_groupAssistTarget);

            if (_controller != null && _controller.TryUpdateCombatOrder())
                return;

            if (_activeTarget != null)
            {
                OrderedAttackTarget = _activeTarget;
                LogInstruction($"Maintaining engagement on {_activeTarget.Name}.");
                return;
            }

            GameLiving? campTarget = (_isPuller || _isTank || _isLeader || _isCrowdControl || _isScout) ? EvaluateCampTarget() : null;

            if (campTarget != null)
            {
                if (ShouldEngageCampTarget())
                {
                    EngageTarget(campTarget);
                    LogInstruction($"Engaging camp target {campTarget.Name}.");
                    return;
                }

                if (_isScout)
                {
                    AnnounceThreat($"{_mimic.Name} spots {campTarget.Name} near the camp.");
                    LogInstruction($"Scout identified camp threat {campTarget.Name}.");
                }
            }

            if (Owner is not GameLiving owner)
            {
                ClearCombatOrders(forceDisengage: false);
                LogInstruction("Owner missing; clearing combat orders.");
                return;
            }

            GameLiving? ownerTarget = ValidateTarget(owner.TargetObject as GameLiving);

            if (ownerTarget != null)
            {
                if (!ShouldAssistOwner(owner, ownerTarget))
                {
                    if (_groupAssistTarget != null)
                    {
                        EngageTarget(_groupAssistTarget);
                        LogInstruction($"Assisting group on {_groupAssistTarget.Name}.");
                    }
                    else if (!_groupInCombat && !HasAggro)
                    {
                        ClearCombatOrders(forceDisengage: false);
                        LogInstruction("Owner not aggressive; waiting for engagement signal.");
                    }

                    return;
                }

                EngageTarget(ownerTarget);
                return;
            }

            if (_groupAssistTarget != null)
            {
                EngageTarget(_groupAssistTarget);
                LogInstruction($"Assisting group on {_groupAssistTarget.Name}.");
                return;
            }

            if (_groupInCombat)
            {
                GameLiving? aggroTarget = HasAggro ? GetHighestAggroTarget() : null;

                if (aggroTarget != null)
                {
                    EngageTarget(aggroTarget);
                    LogInstruction($"Re-engaging {aggroTarget.Name} from aggro list.");
                }

                return;
            }

            ClearCombatOrders(forceDisengage: false);
            LogInstruction("Owner has no valid target; holding position.");
        }

        private void EnsureFollowTarget(GameLiving target, ushort minDistance, ushort maxDistance, string instruction)
        {
            if (WalkState != eWalkState.Follow || _mimic.FollowTarget != target || !_mimic.IsWithinRadius(target, maxDistance))
            {
                WalkState = eWalkState.Follow;
                _mimic.Follow(target, minDistance, maxDistance);
                LogInstruction(instruction);
            }
        }

        private void EvaluateGroupCombatState()
        {
            _groupAssistTarget = null;
            _groupInCombat = TryGetGroupCombatState(out GameLiving? assistTarget);
            _groupAssistTarget = assistTarget;
        }

        private bool TryGetGroupCombatState(out GameLiving? assistTarget)
        {
            assistTarget = null;

            GamePlayer ownerPlayer = _mimic.Owner;
            Group? group = ownerPlayer.Group;

            if (group == null)
            {
                bool engaged = IsGroupMemberEngaged(ownerPlayer);

                if (engaged)
                    assistTarget = GetGroupMemberTarget(ownerPlayer);

                return engaged;
            }

            GameLiving? fallbackTarget = null;
            bool inCombat = false;
            List<GameLiving> members = group.GetMembersInTheGroup();

            try
            {
                foreach (GameLiving member in members)
                {
                    if (member == null || !member.IsAlive)
                        continue;

                    if (!IsGroupMemberEngaged(member))
                        continue;

                    inCombat = true;

                    GameLiving? memberTarget = GetGroupMemberTarget(member);

                    if (member == ownerPlayer && memberTarget != null)
                    {
                        assistTarget = memberTarget;
                        return true;
                    }

                    if (member != _mimic && memberTarget != null && fallbackTarget == null)
                        fallbackTarget = memberTarget;
                }
            }
            finally
            {
                members.Clear();
            }

            assistTarget = fallbackTarget;
            return inCombat;
        }

        private static bool IsGroupMemberEngaged(GameLiving member)
        {
            if (!member.IsAlive)
                return false;

            if (member.IsAttacking)
                return true;

            if (member.attackComponent?.AttackState == true)
                return true;

            if (member.InCombat)
                return true;

            ISpellHandler? spell = member.CurrentSpellHandler;

            return spell != null && spell.IsInCastingPhase && spell.Spell.Target == eSpellTarget.ENEMY;
        }

        private GameLiving? GetGroupMemberTarget(GameLiving member)
        {
            GameLiving? target = ValidateTarget(member.TargetObject as GameLiving);

            if (target != null)
                return target;

            AttackAction? attackAction = member.attackComponent?.attackAction;

            if (attackAction?.LastAttackData?.Target is GameLiving lastTarget)
            {
                target = ValidateTarget(lastTarget);

                if (target != null)
                    return target;
            }

            ISpellHandler? spell = member.CurrentSpellHandler;

            if (spell != null && spell.Spell.Target == eSpellTarget.ENEMY)
                return ValidateTarget(spell.Target);

            return null;
        }

        private GameLiving? GetHighestAggroTarget()
        {
            foreach (StandardMobBrain.OrderedAggroListElement entry in GetOrderedAggroList())
            {
                GameLiving? target = ValidateTarget(entry.Living);

                if (target != null)
                    return target;
            }

            return null;
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
            if (owner is GamePlayer player)
            {
                if (player.IsInAttackMode)
                    return true;
            }
            else if (owner.IsAttacking)
            {
                return true;
            }

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

        internal GameLiving? ActiveTargetInternal => _activeTarget;
        internal bool GroupInCombat => _groupInCombat;
        internal void EngageTargetInternal(GameLiving target) => EngageTarget(target);
        internal void ClearCombatOrdersInternal(bool forceDisengage) => ClearCombatOrders(forceDisengage);
        internal GameLiving? EvaluateCampTargetInternal() => EvaluateCampTarget();
        internal GameLiving? ValidateTargetInternal(GameLiving? target) => ValidateTarget(target);
        internal void LogInstructionInternal(string instruction, bool force = false) => LogInstruction(instruction, force);

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
            bool ownerAggressive = OwnerIsAggressive(owner);
            bool ownerThreatened = IsOwnerUnderThreat();

            if (_pvpMode)
            {
                if (target is GamePlayer)
                    return true;

                return ownerThreatened || HasAggro;
            }

            if (_isTank)
                return true;

            if (_isDamageDealer)
                return ownerAggressive || ownerThreatened || HasAggro;

            if (_isCrowdControl)
                return ownerAggressive || ownerThreatened;

            if (_isSupport)
                return ownerThreatened || HasAggro;

            if (_isHealer)
                return ownerThreatened;

            if (_isPuller)
                return ownerAggressive || ownerThreatened;

            return ownerAggressive || HasAggro;
        }

        private bool ShouldEngageCampTarget()
        {
            if (_preventCombat)
                return false;

            if (_isPuller || _isTank || _isLeader)
                return true;

            if (_isCrowdControl && !_isHealer)
                return true;

            return false;
        }

        private bool TryCrowdControlAction()
        {
            if (_mimic.Spells == null || _mimic.Spells.Count == 0)
                return false;

            if (_mimic.IsCasting || _preventCombat)
                return false;

            if (GameLoop.GameLoopTime < _nextCrowdControlCheck)
                return false;

            _nextCrowdControlCheck = GameLoop.GameLoopTime + 3000;

            Spell? ccSpell = null;

            foreach (Spell spell in _mimic.Spells)
            {
                if (IsCrowdControlSpell(spell))
                {
                    ccSpell = spell;
                    break;
                }
            }

            if (ccSpell == null)
                return false;

            GameLiving? target = FindCrowdControlTarget(ccSpell);

            if (target == null)
                return false;

            _mimic.TargetObject = target;

            if (LivingHasEffect(target, ccSpell))
                return false;

            if (_mimic.CastSpell(ccSpell, s_mobSpellLine, checkLos: false))
            {
                LogInstruction($"Applying crowd control to {target.Name}.");
                return true;
            }

            return false;
        }

        private static bool IsCrowdControlSpell(Spell spell)
        {
            return spell.SpellType is eSpellType.Mesmerize or eSpellType.SpeedDecrease or eSpellType.Stun or eSpellType.DamageSpeedDecrease;
        }

        private GameLiving? FindCrowdControlTarget(Spell spell)
        {
            if (_mimic.CurrentRegion is not Region region)
                return null;

            int range = Math.Clamp(spell.CalculateEffectiveRange(_mimic), 1, 2000);
            GameLiving? primary = ValidateTarget(Owner?.TargetObject as GameLiving);
            List<GameLiving> candidates = new();

            void Consider(GameLiving? living)
            {
                if (living == null || living == primary || living == _mimic)
                    return;

                if (!living.IsAlive || living.ObjectState != GameObject.eObjectState.Active)
                    return;

                if (!GameServer.ServerRules.IsAllowedToAttack(_mimic, living, true))
                    return;

                if (!_mimic.IsWithinRadius(living, range))
                    return;

                if (LivingHasEffect(living, spell))
                    return;

                candidates.Add(living);
            }

            GameLiving? anchor = Owner as GameLiving ?? _mimic;

            foreach (GamePlayer player in region.GetPlayersInRadius(anchor, (ushort)range))
                Consider(player);

            foreach (GameNPC npc in region.GetNPCsInRadius(anchor, (ushort)range))
                Consider(npc);

            if (candidates.Count == 0)
                return null;

            return candidates[Util.Random(candidates.Count - 1)];
        }

        private void PerformScoutSweep()
        {
            if (_mimic.CurrentRegion is not Region region || Owner is not GameLiving owner)
                return;

            if (GameLoop.GameLoopTime < _nextScoutPulse)
                return;

            _nextScoutPulse = GameLoop.GameLoopTime + 5000;

            List<string> names = new();
            int radius = 1200;

            foreach (GamePlayer player in region.GetPlayersInRadius(owner, (ushort)radius))
            {
                if (player == owner || !player.IsAlive)
                    continue;

                if (!GameServer.ServerRules.IsAllowedToAttack(_mimic, player, true))
                    continue;

                names.Add(player.Name);

                if (names.Count >= 3)
                    break;
            }

            if (names.Count < 3)
            {
                foreach (GameNPC npc in region.GetNPCsInRadius(owner, (ushort)radius))
                {
                    if (npc == _mimic || !npc.IsAlive)
                        continue;

                    if (!GameServer.ServerRules.IsAllowedToAttack(_mimic, npc, true))
                        continue;

                    names.Add(npc.Name);

                    if (names.Count >= 3)
                        break;
                }
            }

            if (names.Count == 0)
                return;

            string report = string.Join(", ", names);

            if (report == _lastScoutReport)
                return;

            _lastScoutReport = report;
            AnnounceThreat($"{_mimic.Name} scouts: {report} nearby.");
        }

        private void AnnounceThreat(string message)
        {
            if (Owner is GamePlayer player)
                player.Out.SendMessage(message, eChatType.CT_System, eChatLoc.CL_SystemWindow);
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
