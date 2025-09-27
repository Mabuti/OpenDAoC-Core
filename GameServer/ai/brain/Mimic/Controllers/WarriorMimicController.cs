using System;
using System.Collections.Generic;
using System.Linq;
using DOL.AI.Brain;
using DOL.GS.Effects;
using DOL.GS.SkillHandler;
using DOL.GS.Spells;

namespace DOL.GS.Mimic.Controllers
{
    internal sealed class WarriorMimicController : IMimicController
    {
        private enum WarriorTankState
        {
            OutOfCombatPrep,
            Pull,
            EstablishThreat,
            Maintain,
            Crisis,
            ResetBetweenPacks
        }

        private sealed class CrowdControlPlan
        {
            public bool MezActive { get; set; }
            public HashSet<GameLiving> MezTargets { get; } = new();
            public HashSet<GameLiving> RootTargets { get; } = new();
            public HashSet<GameLiving> BreakWhitelist { get; } = new();

            public void Clear()
            {
                MezActive = false;
                MezTargets.Clear();
                RootTargets.Clear();
                BreakWhitelist.Clear();
            }
        }

        private sealed class WarriorBlackboard
        {
            public List<GameLiving> AllyHealerPriorityList { get; } = new();
            public List<GameLiving> FragileAllies { get; } = new();
            public CrowdControlPlan CCPlan { get; } = new();
            public GameLiving? FocusTarget { get; set; }
            public GameLiving? CurrentBoss { get; set; }
            public List<GameLiving> Adds { get; } = new();
            public Dictionary<GameLiving, long> ThreatTable { get; } = new();
            public List<GameLiving> EnemyCasters { get; } = new();
            public List<(GameLiving Enemy, GameLiving Victim, int Distance)> EnemyMeleeOnAllies { get; } = new();
            public int EndurancePct { get; set; }
            public bool MajorCooldownsReady { get; set; }
            public bool ShieldStunReady { get; set; }
            public bool EngageActive { get; set; }
            public Point3D Position { get; set; }
            public ushort Facing { get; set; }
            public bool SafeHealerLOS { get; set; }
            public bool IncomingBigDamage { get; set; }

            public void Reset()
            {
                AllyHealerPriorityList.Clear();
                FragileAllies.Clear();
                CCPlan.Clear();
                FocusTarget = null;
                CurrentBoss = null;
                Adds.Clear();
                ThreatTable.Clear();
                EnemyCasters.Clear();
                EnemyMeleeOnAllies.Clear();
                EndurancePct = 100;
                MajorCooldownsReady = false;
                ShieldStunReady = true;
                EngageActive = false;
                Position = new Point3D();
                Facing = 0;
                SafeHealerLOS = true;
                IncomingBigDamage = false;
            }
        }

        private static readonly HashSet<eCharacterClass> PrimaryHealers = new()
        {
            eCharacterClass.Cleric,
            eCharacterClass.Druid,
            eCharacterClass.Healer,
            eCharacterClass.Shaman,
            eCharacterClass.Friar,
            eCharacterClass.Bard,
            eCharacterClass.Warden,
            eCharacterClass.Valkyrie,
            eCharacterClass.Mentalist,
            eCharacterClass.Heretic
        };

        private static readonly HashSet<eCharacterClass> FragileCasterClasses = new()
        {
            eCharacterClass.Sorcerer,
            eCharacterClass.Wizard,
            eCharacterClass.Cabalist,
            eCharacterClass.Theurgist,
            eCharacterClass.Necromancer,
            eCharacterClass.Spiritmaster,
            eCharacterClass.Runemaster,
            eCharacterClass.Bonedancer,
            eCharacterClass.Warlock,
            eCharacterClass.Animist,
            eCharacterClass.Bainshee,
            eCharacterClass.Eldritch,
            eCharacterClass.Enchanter
        };

        private readonly MimicBrain _brain;
        private readonly MimicNPC _mimic;
        private readonly WarriorBlackboard _bb = new();
        private WarriorTankState _state = WarriorTankState.OutOfCombatPrep;
        private bool _enabled;
        private bool _disposed;
        private long _stateEnteredAt;
        private long _establishThreatUntil;
        private long _nextGuardEvaluation;
        private long _lastHealthSampleTime;
        private int _lastHealthPercent;
        private long _lastCrisisMitigation;
        private long _lastPeelCallout;
        private long _nextEngageWindow;
        private GameLiving? _assignedGuardTarget;

        public WarriorMimicController(MimicBrain brain, MimicNPC mimic)
        {
            _brain = brain;
            _mimic = mimic;
            _lastHealthPercent = mimic.HealthPercent;
            EnsureDefensiveAbilities();
        }

        public void Dispose()
        {
            _disposed = true;
        }

        public void OnRoleChanged(MimicRole role)
        {
            bool shouldEnable = role.HasFlag(MimicRole.Tank);

            if (_enabled == shouldEnable)
                return;

            _enabled = shouldEnable;
            _state = WarriorTankState.OutOfCombatPrep;
            _stateEnteredAt = GameLoop.GameLoopTime;

            if (!_enabled)
                _brain.ClearCombatOrdersInternal(true);
        }

        public void OnPreventCombatChanged(bool value)
        {
            if (value)
                _state = WarriorTankState.OutOfCombatPrep;
        }

        public void OnPvPModeChanged(bool value)
        {
        }

        public void OnGuardTargetChanged(GameLiving? target)
        {
            _assignedGuardTarget = target;
        }

        public void Think()
        {
            if (!_enabled || _disposed)
                return;

            UpdateBlackboard();
            MaintainGuardAssignments();
            SampleIncomingDamage();
        }

        public bool TryHandleRoleBehaviors()
        {
            if (!_enabled || _disposed)
                return false;

            ExecuteBehaviorTree();
            return true;
        }

        public bool TryUpdateCombatOrder()
        {
            if (!_enabled || _disposed)
                return false;

            UpdateStateMachine();
            ExecuteStateBehaviors();
            return true;
        }

        private void ExecuteBehaviorTree()
        {
            if (PreserveCrowdControl())
                return;

            if (CrisisResponse())
                return;

            if (PeelSubroutine())
                return;

            if (InterruptHighValueCasters())
                return;

            MaintainBossControl();
            AddControl();
            ResourceDiscipline();
            AssistWindow();
            Fallback();
        }

        private bool PreserveCrowdControl()
        {
            CrowdControlPlan plan = _bb.CCPlan;

            if (!plan.MezActive)
                return false;

            GameLiving? activeTarget = _brain.ActiveTargetInternal;

            if (activeTarget == null)
                return false;

            if (plan.BreakWhitelist.Contains(activeTarget))
                return false;

            if (plan.MezTargets.Contains(activeTarget) || plan.RootTargets.Contains(activeTarget))
            {
                _brain.LogInstructionInternal($"Crowd control active on {activeTarget.Name}; suppressing attacks.");
                _brain.ClearCombatOrdersInternal(false);
                return true;
            }

            return false;
        }

        private bool CrisisResponse()
        {
            GameLiving? guardTarget = GetPrimaryGuardTarget();

            bool healerInDanger = guardTarget != null &&
                                  (_bb.EnemyMeleeOnAllies.Any(p => p.Victim == guardTarget && p.Distance < 200) ||
                                   guardTarget.HealthPercent < 45);

            if (!_bb.IncomingBigDamage && !healerInDanger)
                return false;

            if (guardTarget != null)
            {
                EnsureProtectEffect(guardTarget);
                EnsureInterceptEffect(guardTarget);
            }

            GameLiving? target = SelectPeelTarget(preferGuard: true);

            if (target != null)
            {
                FocusOnTarget(target, highThreat: true, logReason: "Crisis response - locking target");
                TryStartEngage(target);
            }

            UseMitigationCooldowns();
            return true;
        }

        private bool PeelSubroutine()
        {
            if (_bb.EnemyMeleeOnAllies.Count == 0)
                return false;

            GameLiving? target = SelectPeelTarget(preferGuard: false);

            if (target == null)
                return false;

            FocusOnTarget(target, highThreat: true, logReason: "Peel subroutine");
            TryStartEngage(target);
            return true;
        }

        private bool InterruptHighValueCasters()
        {
            if (_bb.EnemyCasters.Count == 0)
                return false;

            GameLiving? priorityCaster = _bb.EnemyCasters
                .OrderByDescending(c => IsTargetThreateningGuard(c) ? 2 : 0)
                .ThenByDescending(c => c.CurrentSpellHandler?.Spell.CastTime ?? 0)
                .FirstOrDefault();

            if (priorityCaster == null)
                return false;

            FocusOnTarget(priorityCaster, highThreat: false, logReason: "Interrupting caster");
            return true;
        }

        private void MaintainBossControl()
        {
            GameLiving? boss = _bb.CurrentBoss;

            if (boss == null)
                return;

            if (_brain.ActiveTargetInternal != boss)
                FocusOnTarget(boss, highThreat: false, logReason: "Maintaining boss control");

            MaintainFacingDiscipline(boss);
            EnsureThreatPadding(boss);
        }

        private void AddControl()
        {
            if (_bb.Adds.Count == 0)
                return;

            foreach (GameLiving add in _bb.Adds)
            {
                if (IsUnderCrowdControl(add))
                    continue;

                if (IsTargetThreateningGuard(add))
                {
                    FocusOnTarget(add, highThreat: true, logReason: "Add threatening guard");
                    TryStartEngage(add);
                    return;
                }

                if (ShouldEngageAdd(add))
                {
                    TryStartEngage(add);
                    return;
                }
            }
        }

        private void ResourceDiscipline()
        {
            if (_bb.EndurancePct >= 25)
                return;

            _brain.LogInstructionInternal("Endurance critical; throttling to light threat styles.");
        }

        private void AssistWindow()
        {
            if (!_mimic.PvPMode || _bb.FocusTarget == null)
                return;

            if (!_bb.ShieldStunReady)
                return;

            GameLiving focus = _bb.FocusTarget;

            if (!GameServer.ServerRules.IsAllowedToAttack(_mimic, focus, true))
                return;

            if (_bb.EnemyMeleeOnAllies.Count > 0)
                return;

            _brain.LogInstructionInternal($"Stabilized – ready to open stun on {focus.Name} when assist is called.");
        }

        private void Fallback()
        {
            if (_bb.CurrentBoss == null && _bb.EnemyMeleeOnAllies.Count == 0)
            {
                GameLiving? guard = GetPrimaryGuardTarget();

                if (guard != null && !_mimic.IsWithinRadius(guard, 220))
                {
                    _mimic.Follow(guard, 120, 300);
                    _brain.LogInstructionInternal($"Holding formation near {guard.Name}.");
                }
            }
        }

        private void UpdateStateMachine()
        {
            long now = GameLoop.GameLoopTime;
            GameLiving? boss = _bb.CurrentBoss;
            bool inCombat = _brain.GroupInCombat || _bb.EnemyMeleeOnAllies.Count > 0 || boss != null;

            switch (_state)
            {
                case WarriorTankState.OutOfCombatPrep:
                    if (inCombat)
                        TransitionTo(WarriorTankState.Pull);
                    break;
                case WarriorTankState.Pull:
                    if (boss != null)
                    {
                        bool bossFacingUs = boss.TargetObject == _mimic || boss.attackComponent?.attackAction?.LastAttackData?.Target == _mimic;

                        if (bossFacingUs)
                        {
                            TransitionTo(WarriorTankState.EstablishThreat);
                            _establishThreatUntil = now + 8000;
                        }
                    }
                    else if (!inCombat)
                    {
                        TransitionTo(WarriorTankState.OutOfCombatPrep);
                    }
                    break;
                case WarriorTankState.EstablishThreat:
                    if (boss == null)
                    {
                        TransitionTo(WarriorTankState.ResetBetweenPacks);
                    }
                    else if (now >= _establishThreatUntil || boss.TargetObject == _mimic)
                    {
                        TransitionTo(WarriorTankState.Maintain);
                    }
                    break;
                case WarriorTankState.Maintain:
                    if (!inCombat)
                    {
                        TransitionTo(WarriorTankState.ResetBetweenPacks);
                    }
                    else if (_bb.IncomingBigDamage || IsGuardInImmediateDanger())
                    {
                        TransitionTo(WarriorTankState.Crisis);
                    }
                    break;
                case WarriorTankState.Crisis:
                    if (!_bb.IncomingBigDamage && !IsGuardInImmediateDanger())
                    {
                        TransitionTo(boss != null ? WarriorTankState.Maintain : WarriorTankState.ResetBetweenPacks);
                    }
                    break;
                case WarriorTankState.ResetBetweenPacks:
                    if (inCombat)
                    {
                        TransitionTo(WarriorTankState.Pull);
                    }
                    else if (now - _stateEnteredAt > 4000)
                    {
                        TransitionTo(WarriorTankState.OutOfCombatPrep);
                    }
                    break;
            }
        }

        private void ExecuteStateBehaviors()
        {
            switch (_state)
            {
                case WarriorTankState.OutOfCombatPrep:
                    _brain.ClearCombatOrdersInternal(false);
                    PrepareForPull();
                    break;
                case WarriorTankState.Pull:
                    AcquirePullTarget();
                    break;
                case WarriorTankState.EstablishThreat:
                case WarriorTankState.Maintain:
                    GameLiving? boss = _bb.CurrentBoss;
                    if (boss != null)
                        FocusOnTarget(boss, highThreat: false, logReason: "Primary boss focus");
                    break;
                case WarriorTankState.Crisis:
                    GameLiving? peelTarget = SelectPeelTarget(preferGuard: true);
                    if (peelTarget != null)
                        FocusOnTarget(peelTarget, highThreat: true, logReason: "Crisis state focus");
                    break;
                case WarriorTankState.ResetBetweenPacks:
                    _brain.ClearCombatOrdersInternal(false);
                    break;
            }
        }

        private void PrepareForPull()
        {
            GameLiving? guard = GetPrimaryGuardTarget();

            if (guard != null && !_mimic.IsWithinRadius(guard, 250))
            {
                _mimic.Follow(guard, 120, 300);
            }

            if (guard != null)
            {
                EnsureGuardEffect(guard);
                EnsureProtectEffect(guard);
            }
        }

        private void AcquirePullTarget()
        {
            GameLiving? target = _bb.FocusTarget ?? _bb.CurrentBoss ?? _brain.EvaluateCampTargetInternal();

            if (target == null)
                return;

            FocusOnTarget(target, highThreat: false, logReason: "Pulling target");
        }

        private void FocusOnTarget(GameLiving target, bool highThreat, string logReason)
        {
            if (!_mimic.IsAlive)
                return;

            if (_brain.ActiveTargetInternal != target)
                _brain.LogInstructionInternal($"{logReason}: switching to {target.Name}.");

            _brain.EngageTargetInternal(target);
            _mimic.TargetObject = target;
            _mimic.attackComponent.RequestStartAttack();

            if (highThreat)
                _brain.AddToAggroList(target, 5);
        }

        private bool ShouldEngageAdd(GameLiving add)
        {
            if (_bb.CurrentBoss == null)
                return true;

            if (IsTargetThreateningGuard(add))
                return true;

            return !_bb.ThreatTable.TryGetValue(add, out long threat) || threat < 10;
        }

        private void TryStartEngage(GameLiving enemy)
        {
            if (GameLoop.GameLoopTime < _nextEngageWindow)
                return;

            if (EffectListService.GetEffectOnTarget(_mimic, eEffect.Engage) is EngageECSGameEffect engageEffect)
            {
                if (engageEffect.EngageTarget == enemy)
                    return;

                engageEffect.Cancel(true, true);
            }

            if (enemy.LastAttackedByEnemyTick > GameLoop.GameLoopTime - EngageAbilityHandler.ENGAGE_ATTACK_DELAY_TICK)
                return;

            _mimic.TargetObject = enemy;
            ECSGameEffectFactory.Create(new(_mimic, 0, 1), static (in ECSGameEffectInitParams i) => new EngageECSGameEffect(i));
            _brain.LogInstructionInternal($"Engaging {enemy.Name} to pin their swings.");
            _nextEngageWindow = GameLoop.GameLoopTime + 3000;
        }

        private void MaintainFacingDiscipline(GameLiving boss)
        {
            GameLiving? guard = GetPrimaryGuardTarget();
            if (guard == null)
                return;

            int dx = guard.X - boss.X;
            int dy = guard.Y - boss.Y;
            double length = Math.Sqrt((dx * dx) + (dy * dy));

            if (length <= 0.01)
                return;

            int offsetX = boss.X + (int)(dx / length * 120.0);
            int offsetY = boss.Y + (int)(dy / length * 120.0);
            Point3D offset = new(offsetX, offsetY, boss.Z);

            if (!_mimic.IsWithinRadius(offset, 120))
            {
                _mimic.WalkTo(offset, _mimic.MaxSpeed);
                _brain.LogInstructionInternal("Micro-adjusting tank position for healer LoS.");
            }

            _mimic.TurnTo(boss);
        }

        private void EnsureThreatPadding(GameLiving boss)
        {
            if (boss.TargetObject == _mimic)
                return;

            _brain.AddToAggroList(boss, 1);
        }

        private void MaintainGuardAssignments()
        {
            if (GameLoop.GameLoopTime < _nextGuardEvaluation)
                return;

            _nextGuardEvaluation = GameLoop.GameLoopTime + 2000;

            GameLiving? desired = GetPrimaryGuardTarget();

            if (desired != null && desired != _assignedGuardTarget)
            {
                _mimic.SetGuardTarget(desired);
                _assignedGuardTarget = desired;
            }

            if (desired != null)
            {
                EnsureGuardEffect(desired);
                EnsureProtectEffect(desired);
            }
        }

        private GameLiving? GetPrimaryGuardTarget()
        {
            if (_bb.AllyHealerPriorityList.Count > 0)
                return _bb.AllyHealerPriorityList[0];

            if (_assignedGuardTarget != null)
                return _assignedGuardTarget;

            return _brain.Owner;
        }

        private void EnsureGuardEffect(GameLiving target)
        {
            GuardAbilityHandler.CheckExistingEffectsOnTarget(_mimic, target, cancelOurs: false, out bool found, out GuardECSGameEffect other);

            if (found)
                return;

            other?.Stop();
            GuardAbilityHandler.CancelOurEffectThenAddOnTarget(_mimic, target);
        }

        private void EnsureProtectEffect(GameLiving target)
        {
            ProtectAbilityHandler.CheckExistingEffectsOnTarget(_mimic, target, cancelOurs: false, out bool found, out ProtectECSGameEffect other);

            if (found)
                return;

            other?.Stop();
            ProtectAbilityHandler.CancelOurEffectThenAddOnTarget(_mimic, target);
        }

        private void EnsureInterceptEffect(GameLiving target)
        {
            InterceptAbilityHandler.CheckExistingEffectsOnTarget(_mimic, target, cancelOurs: false, out bool found, out InterceptECSGameEffect other);

            if (found)
                return;

            other?.Stop();
            InterceptAbilityHandler.CancelOurEffectThenAddOnTarget(_mimic, target);
            Ability? ability = _mimic.GetAbility(Abilities.Intercept);
            if (ability != null)
                _mimic.DisableSkill(ability, InterceptAbilityHandler.REUSE_TIMER);
        }

        private void UpdateBlackboard()
        {
            _bb.Reset();
            _bb.Position = new Point3D(_mimic.X, _mimic.Y, _mimic.Z);
            _bb.Facing = _mimic.Heading;
            _bb.EndurancePct = _mimic.EndurancePercent;
            _bb.EngageActive = EffectListService.GetEffectOnTarget(_mimic, eEffect.Engage) != null;

            UpdateGroupAwareness();
            UpdateEnemyAwareness();
            UpdateCooldownAwareness();
            UpdateCrowdControlAwareness();
        }

        private void UpdateGroupAwareness()
        {
            Group? group = _mimic.Group;

            if (group == null)
                return;

            List<GameLiving> members = group.GetMembersInTheGroup();

            foreach (GameLiving member in members)
            {
                if (!member.IsAlive || member == _mimic)
                    continue;

                if (member is GamePlayer player)
                {
                    eCharacterClass charClass = (eCharacterClass)player.CharacterClass.ID;

                    if (PrimaryHealers.Contains(charClass))
                        _bb.AllyHealerPriorityList.Add(member);

                    if (FragileCasterClasses.Contains(charClass))
                        _bb.FragileAllies.Add(member);
                }
            }

            if (_bb.AllyHealerPriorityList.Count > 1)
                _bb.AllyHealerPriorityList.Sort((a, b) => a.HealthPercent.CompareTo(b.HealthPercent));

            GameLiving? owner = _brain.Owner;
            _bb.FocusTarget = owner != null ? _brain.ValidateTargetInternal(owner.TargetObject as GameLiving) : null;

            GameLiving? guard = GetPrimaryGuardTarget();
            if (guard != null)
                _bb.SafeHealerLOS = guard.IsWithinRadius(_mimic, 600);
        }

        private void UpdateEnemyAwareness()
        {
            foreach (StandardMobBrain.OrderedAggroListElement entry in _brain.GetOrderedAggroList())
            {
                GameLiving enemy = entry.Living;

                if (enemy == null || !enemy.IsAlive)
                    continue;

                _bb.ThreatTable[enemy] = entry.AggroAmount;

                if (_bb.CurrentBoss == null)
                    _bb.CurrentBoss = enemy;

                if (_bb.CurrentBoss != null && enemy != _bb.CurrentBoss)
                    _bb.Adds.Add(enemy);

                if (enemy.IsCasting)
                {
                    ISpellHandler? handler = enemy.CurrentSpellHandler;
                    if (handler != null && handler.Spell.CastTime >= 1500)
                        _bb.EnemyCasters.Add(enemy);
                }

                GameLiving? victim = enemy.TargetObject as GameLiving;

                if (victim != null && victim.Realm == _mimic.Realm && victim != _mimic)
                {
                    int distance = (int)enemy.GetDistanceTo(victim);
                    _bb.EnemyMeleeOnAllies.Add((enemy, victim, distance));
                }
            }

            if (_bb.CurrentBoss == null)
                _bb.CurrentBoss = _bb.Adds.FirstOrDefault();
        }

        private void UpdateCooldownAwareness()
        {
            Ability? rampage = _mimic.GetAbility(Abilities.Rampage);
            Ability? bodyguard = _mimic.GetAbility(Abilities.Bodyguard);

            bool rampageReady = rampage == null || _mimic.GetSkillDisabledDuration(rampage) <= 0;
            bool bodyguardReady = bodyguard == null || _mimic.GetSkillDisabledDuration(bodyguard) <= 0;

            _bb.MajorCooldownsReady = rampageReady || bodyguardReady;
            _bb.ShieldStunReady = true;
        }

        private void UpdateCrowdControlAwareness()
        {
            foreach (GameLiving enemy in _bb.ThreatTable.Keys)
            {
                if (EffectListService.GetEffectOnTarget(enemy, eEffect.Mez) != null)
                {
                    _bb.CCPlan.MezTargets.Add(enemy);
                    _bb.CCPlan.MezActive = true;
                }

                if (EffectListService.GetEffectOnTarget(enemy, eEffect.Snare) != null)
                {
                    _bb.CCPlan.RootTargets.Add(enemy);
                    _bb.CCPlan.MezActive = true;
                }
            }
        }

        private void SampleIncomingDamage()
        {
            long now = GameLoop.GameLoopTime;

            if (_lastHealthSampleTime == 0)
            {
                _lastHealthSampleTime = now;
                _lastHealthPercent = _mimic.HealthPercent;
                return;
            }

            if (now - _lastHealthSampleTime < 2000)
                return;

            int newHealth = _mimic.HealthPercent;
            int delta = _lastHealthPercent - newHealth;
            _bb.IncomingBigDamage = delta >= 20;
            _lastHealthSampleTime = now;
            _lastHealthPercent = newHealth;
        }

        private void UseMitigationCooldowns()
        {
            if (GameLoop.GameLoopTime - _lastCrisisMitigation < 10000)
                return;

            Ability? rampage = _mimic.GetAbility(Abilities.Rampage);

            if (rampage != null && _mimic.GetSkillDisabledDuration(rampage) <= 0)
            {
                TryExecuteAbility(rampage);
                _brain.LogInstructionInternal("Activating Rampage for mitigation.");
                _lastCrisisMitigation = GameLoop.GameLoopTime;
                return;
            }

            Ability? fury = _mimic.GetAbility(Abilities.Fury);

            if (fury != null && _mimic.GetSkillDisabledDuration(fury) <= 0)
            {
                TryExecuteAbility(fury);
                _brain.LogInstructionInternal("Activating Fury for mitigation.");
                _lastCrisisMitigation = GameLoop.GameLoopTime;
            }
        }

        private void TryExecuteAbility(Ability ability)
        {
            IAbilityActionHandler? handler = SkillBase.GetAbilityActionHandler(ability.KeyName);

            if (handler is SpellCastingAbilityHandler spellHandler)
            {
                if (spellHandler.CheckPreconditions(_mimic, spellHandler.Preconditions))
                    return;

                Spell? spell = SkillBase.GetSpellByID(spellHandler.SpellID);
                SpellLine? line = SkillBase.GetSpellLine(GlobalSpellsLines.Character_Abilities);

                if (spell != null && line != null)
                    _mimic.CastSpell(spell, line, false);

                return;
            }

            handler?.Execute(ability, _mimic.Owner);
        }

        private bool IsGuardInImmediateDanger()
        {
            GameLiving? guard = GetPrimaryGuardTarget();
            if (guard == null)
                return false;

            return _bb.EnemyMeleeOnAllies.Any(e => e.Victim == guard && e.Distance < 220) || guard.HealthPercent < 50;
        }

        private GameLiving? SelectPeelTarget(bool preferGuard)
        {
            if (_bb.EnemyMeleeOnAllies.Count == 0)
                return null;

            IEnumerable<(GameLiving Enemy, GameLiving Victim, int Distance)> ordered = _bb.EnemyMeleeOnAllies.OrderBy(e => e.Distance);

            if (preferGuard)
            {
                GameLiving? guard = GetPrimaryGuardTarget();
                if (guard != null)
                {
                    GameLiving? guardThreat = ordered.FirstOrDefault(e => e.Victim == guard).Enemy;
                    if (guardThreat != null)
                        return guardThreat;
                }
            }

            foreach ((GameLiving Enemy, GameLiving Victim, int _) entry in ordered)
            {
                if (_bb.FragileAllies.Contains(entry.Victim))
                    return entry.Enemy;
            }

            return ordered.First().Enemy;
        }

        private bool IsTargetThreateningGuard(GameLiving enemy)
        {
            GameLiving? guard = GetPrimaryGuardTarget();
            if (guard == null)
                return false;

            return _bb.EnemyMeleeOnAllies.Any(e => e.Enemy == enemy && e.Victim == guard);
        }

        private bool IsUnderCrowdControl(GameLiving enemy)
        {
            return _bb.CCPlan.MezTargets.Contains(enemy) || _bb.CCPlan.RootTargets.Contains(enemy);
        }

        private void TransitionTo(WarriorTankState state)
        {
            if (_state == state)
                return;

            _state = state;
            _stateEnteredAt = GameLoop.GameLoopTime;
            _brain.LogInstructionInternal($"Transitioning to {_state} state.");
        }

        private void EnsureDefensiveAbilities()
        {
            EnsureAbility(Abilities.Guard);
            EnsureAbility(Abilities.Protect);
            EnsureAbility(Abilities.Intercept);
            EnsureAbility(Abilities.Engage);
            EnsureAbility(Abilities.Bodyguard);
            EnsureAbility(Abilities.Rampage);
            EnsureAbility(Abilities.Fury);
        }

        private void EnsureAbility(string keyName, int level = 1)
        {
            if (_mimic.HasAbility(keyName))
                return;

            Ability? ability = SkillBase.GetAbility(keyName, level);
            if (ability != null)
                _mimic.AddAbility(ability, false);
        }
    }
}
