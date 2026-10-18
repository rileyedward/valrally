using UnityEngine;

namespace BifrostRally.Core
{
    /// <summary>Per-wheel state carried between physics steps.</summary>
    public struct WheelState
    {
        /// <summary>Spin speed in rad/s, positive rolling forwards.</summary>
        public float Omega;

        /// <summary>Relaxed slip ratio: (wheel surface speed − ground speed) / speed.</summary>
        public float SlipRatio;

        /// <summary>Relaxed tan(slip angle).</summary>
        public float LateralSlip;
    }

    /// <summary>
    /// One wheel's spin and tyre forces for one physics step, sub-stepped so the stiff coupling
    /// between spin and tyre force stays stable at a 50 Hz physics rate.
    /// </summary>
    public static class WheelDynamics
    {
        private const float MaxOmega = 400f;

        public struct Inputs
        {
            public float VLong;
            public float VLat;
            public float Load;
            public float DriveTorque;
            public float BrakeTorque;
            public float LateralPeak;
            public float LongitudinalPeak;
            public float SpeedKmh;
            public float Inertia;
            public bool Grounded;

            /// <summary>Surface tweaks; 0 means 1 so a default struct still works.</summary>
            public float PeakSlipScale;
            public float ShapeScale;
        }

        /// <summary>Returns the average (forward, side) force over the step, in the wheel's frame.</summary>
        public static Vector2 Step(ref WheelState state, Inputs input, CarSettings s, float dt)
        {
            int substeps = Mathf.Max(1, Mathf.RoundToInt(s.SpinSubsteps));
            float h = dt / substeps;
            float radius = s.WheelRadius;
            float rollSpeed = Mathf.Max(Mathf.Abs(input.VLong), s.RelaxationSpeedFloor);
            float lengthX = Mathf.Max(s.LongRelaxationLength, Mathf.Abs(input.SpeedKmh) * s.RelaxationPerKmh);
            float lengthY = Mathf.Max(s.RelaxationLength, Mathf.Abs(input.SpeedKmh) * s.RelaxationPerKmh);
            float kx = Mathf.Min(1f, rollSpeed * h / lengthX);
            float ky = Mathf.Min(1f, rollSpeed * h / lengthY);

            float peakScale = input.PeakSlipScale > 0f ? input.PeakSlipScale : 1f;
            float shapeScale = input.ShapeScale > 0f ? input.ShapeScale : 1f;
            float peakRatio = s.LongitudinalPeakSlip * peakScale;
            float peakAngle = s.PacejkaPeakSlipDeg * peakScale;
            float shapeLong = s.LongitudinalShape * shapeScale;
            float shapeLat = s.PacejkaShape * shapeScale;

            Vector2 sum = Vector2.zero;
            for (int i = 0; i < substeps; i++)
            {
                float fx = 0f;
                float fy = 0f;
                if (input.Grounded)
                {
                    float targetRatio = (state.Omega * radius - input.VLong) / rollSpeed;
                    state.SlipRatio = Mathf.Clamp(state.SlipRatio + kx * (targetRatio - state.SlipRatio), -1.5f, 1.5f);
                    state.LateralSlip += ky * (input.VLat / rollSpeed - state.LateralSlip);

                    // Combined slip: each slip measured against its own peak, and the length of
                    // that vector decides how far along the curve both forces are.
                    float sx = state.SlipRatio / peakRatio;
                    float sy = Mathf.Atan(state.LateralSlip) * Mathf.Rad2Deg / peakAngle;
                    float combined = Mathf.Max(Mathf.Sqrt(sx * sx + sy * sy), 1e-4f);
                    fx = input.LongitudinalPeak * (sx / combined) * Tyre.MagicFormula(combined, 1f, shapeLong, 0f);
                    fy = -input.LateralPeak * (sy / combined) * Tyre.MagicFormula(combined, 1f, shapeLat, s.PacejkaCurvature);
                }
                else
                {
                    state.SlipRatio = 0f;
                    state.LateralSlip = 0f;
                }

                // SpinResponse scales every torque, which acts like a heavier wheel and keeps the
                // stiff spin/tyre coupling calm.
                float gain = s.SpinResponse * h / input.Inertia;
                float omega = state.Omega + (input.DriveTorque - fx * radius) * gain;

                // Brakes are friction: they pull spin towards zero and hold it there, never past it.
                float brakeStep = input.BrakeTorque * gain;
                omega = Mathf.Abs(omega) <= brakeStep ? 0f : omega - Mathf.Sign(omega) * brakeStep;

                state.Omega = Mathf.Clamp(omega, -MaxOmega, MaxOmega);
                sum.x += fx;
                sum.y += fy;
            }
            return sum / substeps;
        }

        public static float Inertia(CarSettings s, bool driven)
        {
            return 0.5f * s.WheelMass * s.WheelRadius * s.WheelRadius + (driven ? s.DrivelineInertia : 0f);
        }
    }
}
