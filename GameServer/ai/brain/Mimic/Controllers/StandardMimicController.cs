using System;
using System.Collections.Generic;
using System.Linq;
using DOL.AI.Brain;
using DOL.GS;
using DOL.GS.Spells;

namespace DOL.GS.Mimic.Controllers
{
    internal sealed class StandardMimicController : IMimicController
    {
        private readonly MimicBrain _brain;
        private readonly MimicNPC _mimic;
        private readonly MimicBehaviorProfile _profile;
        private readonly List<GameLiving> _allies = new();
        private readonly HashSet<GameLiving> _allySet = new();
        private readonly List<GameLiving> _threats = new();
        private readonly Dictionary<GameLiving, long> _crowdControlTracker = new();
        private Spell? _healSpell;
        private Spell? _emergencyHealSpell;
        private Spell? _healOverTimeSpell;
        private Spell? _damageSpell;
        private Spell? _crowdControlSpell;
        private Spell? _diseaseSpell;
        private Spell? _nearsightSpell;
        private Spell? _strengthBuffSpell;
        private Spell? _powerBuffSpell;
        private long _nextHealCheck;
        private long _nextCrowdControlCheck;
        private long _nextInterruptCheck;
        private long _nextDiseaseCheck;
        private long _nextNearsightCheck;
        private long _nextStrengthRefresh;
        private long _nextPowerRefresh;
        private bool _enabled;
        private bool _disposed;

        private StandardMimicController(MimicBrain brain, MimicNPC mimic, MimicBehaviorProfile profile)
        {
            _brain = brain;
            _mimic = mimic;
            _profile = profile;
        }

        public static IMimicController? TryCreate(MimicBrain brain, MimicNPC mimic)
        {
            if (!MimicBehaviorProfiles.TryGetProfile(mimic.Template.CharacterClass, out MimicBehaviorProfile? profile) || profile == null)
                return null;

            // The warrior has a bespoke controller with a full tank planner.
            if (mimic.Template.CharacterClass == eCharacterClass.Warrior)
                return null;

            return new StandardMimicController(brain, mimic, profile);
        }

        public void Dispose()
        {
            _disposed = true;
            _allies.Clear();
            _threats.Clear();
            _crowdControlTracker.Clear();
        }

        public void OnRoleChanged(MimicRole role)
        {
            if (_disposed)
                return;

            _enabled = role != MimicRole.None;
            _nextHealCheck = 0;
            _nextCrowdControlCheck = 0;
            _nextInterruptCheck = 0;
            _nextDiseaseCheck = 0;
            _nextNearsightCheck = 0;
            _nextStrengthRefresh = 0;
            _nextPowerRefresh = 0;
        }

        public void OnPreventCombatChanged(bool value)
        {
        }

        public void OnPvPModeChanged(bool value)
        {
        }

        public void OnGuardTargetChanged(GameLiving? target)
        {
        }

        public void Think()
        {
            if (!_enabled || _disposed)
                return;

            RefreshGroupState();
            EnsureSpellCache();
            MaintainLongDurationBuffs();
        }

        public bool TryHandleRoleBehaviors()
        {
            if (!_enabled || _disposed)
                return false;

            bool performed = false;

            if (_profile.HasCapability(MimicBehaviorCapability.Heals))
                performed |= TryEmergencyHeal();

            if (!performed && _profile.HasCapability(MimicBehaviorCapability.CrowdControl))
                performed |= TryCrowdControl();

            if (!performed && _profile.HasCapability(MimicBehaviorCapability.Disease))
                performed |= TryApplyDisease();

            if (!performed && _profile.HasCapability(MimicBehaviorCapability.Nearsight))
                performed |= TryApplyNearsight();

            if (!performed && _profile.HasCapability(MimicBehaviorCapability.Interrupts))
                performed |= TryInterrupt();

            return performed;
        }

        public bool TryUpdateCombatOrder()
        {
            return false;
        }

        private void RefreshGroupState()
        {
            _allies.Clear();
            _allySet.Clear();
            _threats.Clear();

            void AddAlly(GameLiving? ally)
            {
                if (ally == null || !ally.IsAlive)
                    return;

                if (_allySet.Add(ally))
                    _allies.Add(ally);
            }

            AddAlly(_mimic);

            if (_mimic.Owner is GameLiving owner)
            {
                AddAlly(owner);

                if (owner is GamePlayer ownerPlayer)
                {
                    Group? group = ownerPlayer.Group;

                    if (group != null)
                    {
                        foreach (GameLiving member in group.GetMembersInTheGroup())
                            AddAlly(member);
                    }
                }
            }

            foreach (MimicNPC other in _mimic.GroupState.Members)
                AddAlly(other);

            foreach (GameLiving ally in _allies)
            {
                AttackComponent? attackComponent = ally.attackComponent;

                if (attackComponent == null)
                    continue;

                foreach (GameLiving attacker in attackComponent.AttackerTracker.Attackers)
                {
                    if (attacker == null || !attacker.IsAlive)
                        continue;

                    if (!GameServer.ServerRules.IsAllowedToAttack(_mimic, attacker, true))
                        continue;

                    if (!_threats.Contains(attacker))
                        _threats.Add(attacker);
                }
            }

            // Clean expired crowd-control tracking.
            long now = GameLoop.GameLoopTime;
            foreach ((GameLiving target, long expires) in _crowdControlTracker.ToList())
            {
                if (expires <= now || !_threats.Contains(target))
                    _crowdControlTracker.Remove(target);
            }
        }

        private void EnsureSpellCache()
        {
            if (_mimic.Spells == null || _mimic.Spells.Count == 0)
                return;

            _healSpell ??= _mimic.Spells.FirstOrDefault(s => s.SpellType == eSpellType.Heal && s.CastTime > 0);
            _emergencyHealSpell ??= _mimic.Spells.FirstOrDefault(s => s.SpellType == eSpellType.Heal && s.CastTime == 0);
            _healOverTimeSpell ??= _mimic.Spells.FirstOrDefault(s => s.SpellType == eSpellType.HealOverTime);
            _damageSpell ??= _mimic.Spells.FirstOrDefault(s => s.SpellType == eSpellType.DirectDamage);
            _crowdControlSpell ??= _mimic.Spells.FirstOrDefault(s => s.SpellType is eSpellType.Mesmerize or eSpellType.SpeedDecrease or eSpellType.Snare or eSpellType.Stun);
            _diseaseSpell ??= _mimic.Spells.FirstOrDefault(s => s.SpellType == eSpellType.Disease);
            _nearsightSpell ??= _mimic.Spells.FirstOrDefault(s => s.SpellType == eSpellType.Nearsight);
            _strengthBuffSpell ??= _mimic.Spells.FirstOrDefault(s => s.SpellType == eSpellType.StrengthConstitutionBuff);
            _powerBuffSpell ??= _mimic.Spells.FirstOrDefault(s => s.SpellType == eSpellType.PowerRegenBuff);
        }

        private void MaintainLongDurationBuffs()
        {
            if (!_profile.HasCapability(MimicBehaviorCapability.Buffs))
                return;

            long now = GameLoop.GameLoopTime;

            if (_strengthBuffSpell != null && now >= _nextStrengthRefresh)
            {
                if (CastSpellOnSelf(_strengthBuffSpell, "Reinforcing the group's physical buffers."))
                    _nextStrengthRefresh = now + Math.Max(_strengthBuffSpell.Duration * 800L, 120000L);
                else
                    _nextStrengthRefresh = now + 5000;
            }

            if (_powerBuffSpell != null && now >= _nextPowerRefresh)
            {
                if (CastSpellOnSelf(_powerBuffSpell, "Refreshing group power regeneration."))
                    _nextPowerRefresh = now + Math.Max(_powerBuffSpell.Duration * 800L, 120000L);
                else
                    _nextPowerRefresh = now + 5000;
            }
        }

        private bool TryEmergencyHeal()
        {
            long now = GameLoop.GameLoopTime;

            if (now < _nextHealCheck)
                return false;

            GameLiving? target = FindAllyNeedingHealing();

            if (target == null)
                return false;

            Spell? spell = SelectHealForTarget(target);

            if (spell == null)
                return false;

            if (!EnsureRange(spell, target))
                return false;

            _mimic.TargetObject = target;

            if (_mimic.CastSpell(spell, MimicBrain.BehaviorSpellLine, false))
            {
                _brain.LogInstructionInternal($"Stabilizing {target.Name} at {target.HealthPercent}% health.");
                int cooldown = spell.CastTime > 0 ? spell.CastTime + 500 : 1000;
                _nextHealCheck = now + Math.Max(cooldown, 800);
                return true;
            }

            _nextHealCheck = now + 750;
            return false;
        }

        private Spell? SelectHealForTarget(GameLiving target)
        {
            if (target.HealthPercent <= 45 && _emergencyHealSpell != null)
                return _emergencyHealSpell;

            if (target.HealthPercent <= 80 && _healSpell != null)
                return _healSpell;

            if (_profile.PrimaryHealer && _healSpell != null)
                return _healSpell;

            return _healOverTimeSpell;
        }

        private GameLiving? FindAllyNeedingHealing()
        {
            GameLiving? candidate = null;
            int lowestHealth = 95;

            foreach (GameLiving ally in _allies)
            {
                if (!ally.IsAlive)
                    continue;

                int health = ally.HealthPercent;

                if (health >= 95)
                    continue;

                if (!_profile.PrimaryHealer && ally != _mimic.Owner && health >= 60)
                    continue;

                if (health < lowestHealth)
                {
                    lowestHealth = health;
                    candidate = ally;
                }
            }

            return candidate;
        }

        private bool TryCrowdControl()
        {
            if (_crowdControlSpell == null)
                return false;

            long now = GameLoop.GameLoopTime;

            if (now < _nextCrowdControlCheck)
                return false;

            if (!ShouldEngageOffensively())
                return false;

            GameLiving? target = FindCrowdControlTarget(now);

            if (target == null)
                return false;

            if (!EnsureRange(_crowdControlSpell, target))
                return false;

            _mimic.TargetObject = target;

            if (_mimic.CastSpell(_crowdControlSpell, MimicBrain.BehaviorSpellLine, false))
            {
                _brain.LogInstructionInternal($"Snaring {target.Name} to peel them off the backline.");
                long duration = Math.Max(_crowdControlSpell.Duration * 1000L, 6000L);
                _crowdControlTracker[target] = now + duration;
                _nextCrowdControlCheck = now + 1500;
                return true;
            }

            _nextCrowdControlCheck = now + 1000;
            return false;
        }

        private GameLiving? FindCrowdControlTarget(long now)
        {
            GameLiving? fallback = null;

            foreach (GameLiving threat in _threats)
            {
                if (threat == null || !threat.IsAlive)
                    continue;

                if (_crowdControlTracker.TryGetValue(threat, out long expires) && expires > now)
                    continue;

                if (_mimic.Owner is GameLiving owner && owner.IsWithinRadius(threat, 450))
                    return threat;

                fallback ??= threat;
            }

            return fallback;
        }

        private bool TryInterrupt()
        {
            if (_damageSpell == null)
                return false;

            long now = GameLoop.GameLoopTime;

            if (now < _nextInterruptCheck)
                return false;

            if (!ShouldEngageOffensively())
                return false;

            GameLiving? target = FindInterruptTarget();

            if (target == null)
                return false;

            if (!EnsureRange(_damageSpell, target))
                return false;

            _mimic.TargetObject = target;

            if (_mimic.CastSpell(_damageSpell, MimicBrain.BehaviorSpellLine, false))
            {
                _brain.LogInstructionInternal($"Pressuring {target.Name} to keep them from casting freely.");
                int recast = _damageSpell.RecastDelay > 0 ? _damageSpell.RecastDelay : 1500;
                _nextInterruptCheck = now + Math.Max(recast, 1500);
                return true;
            }

            _nextInterruptCheck = now + 1200;
            return false;
        }

        private bool TryApplyDisease()
        {
            if (_diseaseSpell == null)
                return false;

            long now = GameLoop.GameLoopTime;

            if (now < _nextDiseaseCheck)
                return false;

            if (!ShouldEngageOffensively())
                return false;

            GameLiving? target = FindMeleeThreat() ?? FindInterruptTarget();

            if (target == null)
                return false;

            if (!EnsureRange(_diseaseSpell, target))
                return false;

            _mimic.TargetObject = target;

            if (_mimic.CastSpell(_diseaseSpell, MimicBrain.BehaviorSpellLine, false))
            {
                _brain.LogInstructionInternal($"Blanketing {target.Name} with disease to weaken their push.");
                int recast = _diseaseSpell.RecastDelay > 0 ? _diseaseSpell.RecastDelay : 2500;
                _nextDiseaseCheck = now + Math.Max(recast, 2500);
                return true;
            }

            _nextDiseaseCheck = now + 1500;
            return false;
        }

        private bool TryApplyNearsight()
        {
            if (_nearsightSpell == null)
                return false;

            long now = GameLoop.GameLoopTime;

            if (now < _nextNearsightCheck)
                return false;

            if (!ShouldEngageOffensively())
                return false;

            GameLiving? target = FindRangedThreat();

            if (target == null)
                target = FindInterruptTarget();

            if (target == null)
                return false;

            if (!EnsureRange(_nearsightSpell, target))
                return false;

            _mimic.TargetObject = target;

            if (_mimic.CastSpell(_nearsightSpell, MimicBrain.BehaviorSpellLine, false))
            {
                _brain.LogInstructionInternal($"Applying nearsight to {target.Name} to break their ranged pressure.");
                int recast = _nearsightSpell.RecastDelay > 0 ? _nearsightSpell.RecastDelay : 3000;
                _nextNearsightCheck = now + Math.Max(recast, 2500);
                return true;
            }

            _nextNearsightCheck = now + 1500;
            return false;
        }

        private GameLiving? FindInterruptTarget()
        {
            if (_mimic.Owner is GameLiving owner)
            {
                if (owner.TargetObject is GameLiving ownerTarget && ownerTarget.IsAlive && GameServer.ServerRules.IsAllowedToAttack(_mimic, ownerTarget, true))
                    return ownerTarget;
            }

            foreach (GameLiving threat in _threats)
            {
                if (threat == null || !threat.IsAlive)
                    continue;

                if (threat.IsCasting)
                    return threat;

                if (threat is GamePlayer)
                    return threat;
            }

            return _threats.FirstOrDefault(t => t != null && t.IsAlive);
        }

        private GameLiving? FindMeleeThreat()
        {
            if (_mimic.Owner is not GameLiving owner)
                return null;

            foreach (GameLiving threat in _threats)
            {
                if (threat == null || !threat.IsAlive)
                    continue;

                if (owner.IsWithinRadius(threat, 300))
                    return threat;
            }

            return null;
        }

        private GameLiving? FindRangedThreat()
        {
            foreach (GameLiving threat in _threats)
            {
                if (threat == null || !threat.IsAlive)
                    continue;

                if (threat.IsCasting)
                    return threat;

                if (threat is GamePlayer player && player.ActiveWeaponSlot == (int)eActiveWeaponSlot.Distance)
                    return threat;
            }

            return null;
        }

        private bool CastSpellOnSelf(Spell spell, string instruction)
        {
            if (_mimic.IsCasting)
                return false;

            _mimic.TargetObject = _mimic;

            if (_mimic.CastSpell(spell, MimicBrain.BehaviorSpellLine, false))
            {
                _brain.LogInstructionInternal(instruction);
                return true;
            }

            return false;
        }

        private bool EnsureRange(Spell spell, GameLiving target)
        {
            int range = Math.Clamp(spell.CalculateEffectiveRange(_mimic), 150, 2000);
            return _mimic.IsWithinRadius(target, range);
        }

        private bool ShouldEngageOffensively()
        {
            if (_brain.GroupInCombat)
                return true;

            if (_mimic.Owner is GameLiving owner && OwnerIsAggressive(owner))
                return true;

            foreach (GameLiving ally in _allies)
            {
                AttackComponent? attackComponent = ally.attackComponent;

                if (attackComponent != null && attackComponent.AttackerTracker.Count > 0)
                    return true;
            }

            return false;
        }

        private static bool OwnerIsAggressive(GameLiving owner)
        {
            if (owner is GamePlayer player && player.IsInAttackMode)
                return true;

            if (owner.IsAttacking)
                return true;

            ISpellHandler? handler = owner.CurrentSpellHandler;
            return handler != null && handler.Spell.Target == eSpellTarget.ENEMY;
        }
    }
}

