using UnityEngine;

namespace BifrostRally.Core
{
    /// <summary>
    /// Tyre force maths. Kept free of Unity components so it can be tested and tuned on its own.
    /// </summary>
    public static class Tyre
    {
        /// <summary>
        /// Magic Formula: F = D·sin(C·atan(B·x − E·(B·x − atan(B·x)))).
        /// D is the peak force, C sets how far the force drops past the peak, and B is chosen
        /// so the peak lands at <paramref name="peakSlip"/> (exact when E is 0).
        /// Returns a normalised force, -1..1, with the same sign as the slip.
        /// </summary>
        public static float MagicFormula(float slip, float peakSlip, float shape, float curvature)
        {
            float c = Mathf.Max(1.01f, shape);
            float b = Mathf.Tan(Mathf.PI / (2f * c)) / Mathf.Max(0.01f, peakSlip);
            float bx = b * slip;
            return Mathf.Sin(c * Mathf.Atan(bx - curvature * (bx - Mathf.Atan(bx))));
        }

        /// <summary>
        /// Peak force per unit load falls as load rises when sensitivity is above zero,
        /// so a heavily loaded tyre grips a little less per newton than a light one.
        /// </summary>
        public static float LoadFactor(float load, float referenceLoad, float sensitivity)
        {
            if (sensitivity <= 0f || referenceLoad <= 0f)
            {
                return 1f;
            }
            return Mathf.Clamp(1f - sensitivity * (load / referenceLoad - 1f), 0.5f, 1.5f);
        }

        /// <summary>
        /// Lateral slip with relaxation: a real tyre needs to roll a short distance before side force
        /// builds up. The state is tan(slip angle) and moves towards its steady value by the fraction
        /// of the relaxation length rolled this step (capped at 1, so it never overshoots).
        /// </summary>
        public static float RelaxLateral(float state, float vLat, float vLong, float speedKmh, CarSettings s, float dt)
        {
            float rollSpeed = Mathf.Max(Mathf.Abs(vLong), s.RelaxationSpeedFloor);
            float length = Mathf.Max(s.RelaxationLength, Mathf.Abs(speedKmh) * s.RelaxationPerKmh);
            float k = Mathf.Min(1f, rollSpeed * dt / length);
            return state + k * (vLat / rollSpeed - state);
        }
    }
}
