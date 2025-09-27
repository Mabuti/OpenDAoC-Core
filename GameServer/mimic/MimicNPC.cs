using System;
using DOL.AI.Brain;
using DOL.GS;

namespace DOL.GS.Mimic
{
    public class MimicNPC : GameNPC
    {
        private readonly MimicTemplate _template;
        private readonly MimicBrain _brain;
        private byte _lastHealthPercent = byte.MaxValue;
        private byte _lastManaPercent = byte.MaxValue;
        private byte _lastEndurancePercent = byte.MaxValue;

        public MimicRole Role { get; private set; }
        public bool PreventCombat { get; private set; }
        public bool PvPMode { get; private set; }
        public GameLiving? GuardTarget { get; private set; }
        public MimicGroupState GroupState { get; }
        public GamePlayer Owner { get; }
        public MimicTemplate Template => _template;

        public MimicNPC(MimicTemplate template, GamePlayer owner, MimicGroupState groupState, int level)
        {
            _template = template;
            Owner = owner;
            GroupState = groupState;
            Name = template.DisplayName;
            Level = (byte)Math.Clamp(level, template.MinimumLevel, template.MaximumLevel);
            Realm = template.Realm;
            Model = template.ModelId;
            Role = MimicRole.None;
            PreventCombat = false;
            PvPMode = false;
            InternalID ??= $"mimic_{Guid.NewGuid():N}";
            _brain = new MimicBrain(owner, this);
            SetOwnBrain(_brain);
        }

        public void AssignRole(MimicRole role)
        {
            Role = role;
            _brain.OnRoleChanged(role);
            MimicLoadoutBuilder.Configure(this, role);
        }

        public void SetPreventCombat(bool value)
        {
            PreventCombat = value;
            _brain.SetPreventCombat(value);
        }

        public void SetPvPMode(bool value)
        {
            PvPMode = value;
            _brain.SetPvPMode(value);
        }

        public void SetGuardTarget(GameLiving? living)
        {
            GuardTarget = living;
            _brain.SetGuardTarget(living);
        }

        public void TeleportTo(GamePlayer player)
        {
            if (player.CurrentRegion == null)
                return;

            MoveTo(player.CurrentRegionID, player.X, player.Y, player.Z, player.Heading);
        }

        public override bool AddToWorld()
        {
            bool result = base.AddToWorld();
            if (result)
            {
                Follow(Owner);
                UpdateGroupStatus(force: true);
            }

            return result;
        }

        public override bool RemoveFromWorld()
        {
            _brain.Dispose();
            return base.RemoveFromWorld();
        }

        public void Follow(GameLiving target)
        {
            if (target == null)
                return;

            Follow(target, 150, 350);
        }

        public override int ChangeHealth(GameObject changeSource, eHealthChangeType healthChangeType, int changeAmount)
        {
            int changed = base.ChangeHealth(changeSource, healthChangeType, changeAmount);

            if (changed != 0)
                UpdateGroupStatus();

            return changed;
        }

        public override int ChangeMana(GameObject changeSource, eManaChangeType manaChangeType, int changeAmount)
        {
            int changed = base.ChangeMana(changeSource, manaChangeType, changeAmount);

            if (changed != 0)
                UpdateGroupStatus();

            return changed;
        }

        public override int ChangeEndurance(GameObject changeSource, eEnduranceChangeType enduranceChangeType, int changeAmount)
        {
            int changed = base.ChangeEndurance(changeSource, enduranceChangeType, changeAmount);

            if (changed != 0)
                UpdateGroupStatus();

            return changed;
        }

        private void UpdateGroupStatus(bool force = false)
        {
            Group? group = Group;

            if (group == null)
                return;

            byte healthPercent = HealthPercent;
            byte manaPercent = ManaPercent;
            byte endurancePercent = EndurancePercent;

            if (!force &&
                healthPercent == _lastHealthPercent &&
                manaPercent == _lastManaPercent &&
                endurancePercent == _lastEndurancePercent)
            {
                return;
            }

            _lastHealthPercent = healthPercent;
            _lastManaPercent = manaPercent;
            _lastEndurancePercent = endurancePercent;

            group.UpdateMember(this, updateIcons: false, updateOtherRegions: false);
        }
    }
}
