using UnityEngine;

namespace BifrostRally.Core
{
    /// <summary>
    /// Simple traction control: when the driven wheels slip more than the target, cut engine
    /// torque in proportion to the excess, then let it come back smoothly.
    /// </summary>
    public class TractionControl
    {
        /// <summary>1 = full torque, lower = trimmed.</summary>
        public float Factor { get; private set; } = 1f;

        public bool Active => Factor < 0.98f;

        public float Step(CarSettings s, float drivenSlipRatio, float dt)
        {
            if (!s.TractionControl || s.TractionSlipTarget <= 0f)
            {
                Factor = 1f;
                return Factor;
            }

            float excess = Mathf.Clamp01((drivenSlipRatio - s.TractionSlipTarget) / s.TractionSlipTarget);
            float target = 1f - excess * Mathf.Clamp01(s.TractionStrength);
            // Cut at once, recover gradually, like a real system.
            Factor = target < Factor ? target : Mathf.MoveTowards(Factor, target, s.TractionRecoverRate * dt);
            return Factor;
        }

        public void Reset()
        {
            Factor = 1f;
        }
    }
}
