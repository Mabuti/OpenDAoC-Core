using DOL.AI.Brain;
using DOL.GS;

namespace DOL.GS.Mimic
{
    public class MimicBrain : FollowOwnerBrain
    {
        private readonly MimicNPC _mimic;
        private bool _preventCombat;
        private bool _pvpMode;
        private GameLiving? _guardTarget;

        public MimicBrain(GameLiving owner, MimicNPC mimic) : base(owner)
        {
            _mimic = mimic;
        }

        public override void Think()
        {
            base.Think();

            if (_preventCombat)
                return;

            if (_guardTarget != null)
            {
                if (_guardTarget.ObjectState == GameObject.eObjectState.Active)
                {
                    if (!_mimic.IsWithinRadius(_guardTarget, 150))
                        _mimic.Follow(_guardTarget, 120, 320);
                    return;
                }

                _guardTarget = null;
            }

            if (Owner != null && !_mimic.IsWithinRadius(Owner, 350))
            {
                _mimic.Follow(Owner, 150, 350);
            }
        }

        public void SetPreventCombat(bool value)
        {
            _preventCombat = value;
        }

        public void SetPvPMode(bool value)
        {
            _pvpMode = value;
        }

        public void SetGuardTarget(GameLiving? target)
        {
            _guardTarget = target;
        }
    }
}
