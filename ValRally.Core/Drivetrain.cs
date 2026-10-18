using UnityEngine;

namespace ValRally.Core
{
    /// <summary>
    /// Engine, automatic 5-speed gearbox and a slipping clutch for launches.
    /// It turns throttle and wheel speed into one total drive torque at the wheels.
    /// </summary>
    public class Drivetrain
    {
        public const int Reverse = -1;
        public const int TopGear = 5;

        /// <summary>-1 reverse, 1..5 forward.</summary>
        public int Gear { get; private set; } = 1;
        public float Rpm { get; private set; }
        public bool Shifting => _shiftTimer > 0f;

        private const float MinTimeInGear = 0.6f;
        private const float LimiterBand = 400f;

        private float _shiftTimer;
        private float _timeInGear;

        private void Shift(int gear, CarSettings s)
        {
            Gear = gear;
            _shiftTimer = s.ShiftTime;
            _timeInGear = 0f;
        }

        /// <summary>Revs the lower gear would sit at for the same road speed.</summary>
        private float UpshiftRpm(CarSettings s, int lowerGear, float roadRpmNow)
        {
            return roadRpmNow * GearRatio(s, lowerGear) / GearRatio(s, Gear);
        }

        public float GearRatio(CarSettings s, int gear)
        {
            switch (gear)
            {
                case Reverse: return -s.ReverseGear;
                case 1: return s.Gear1;
                case 2: return s.Gear2;
                case 3: return s.Gear3;
                case 4: return s.Gear4;
                default: return s.Gear5;
            }
        }

        /// <summary>
        /// Torque curve: a smooth hump peaking at PeakTorqueRpm, never below 30% of peak
        /// across the usable range.
        /// </summary>
        public static float EngineTorque(CarSettings s, float rpm)
        {
            float x = (rpm - s.PeakTorqueRpm) / s.PeakTorqueRpm;
            return s.MaxTorque * Mathf.Clamp(1f - 0.6f * x * x, 0.3f, 1f);
        }

        /// <param name="throttle">0..1, already resolved for direction (brake pedal when reversing).</param>
        /// <param name="wantReverse">Driver is asking to back up.</param>
        /// <param name="drivenOmega">Average spin of the driven wheels, rad/s.</param>
        /// <param name="forwardSpeed">Car speed along its nose, m/s.</param>
        /// <returns>Total torque at the driven wheels, N·m.</returns>
        public float Step(CarSettings s, float throttle, bool wantReverse, float drivenOmega, float forwardSpeed, float dt)
        {
            // Pick direction only when nearly stopped, like an automatic selector.
            if (Mathf.Abs(forwardSpeed) < 1f)
            {
                if (wantReverse && Gear != Reverse)
                {
                    Gear = Reverse;
                }
                else if (!wantReverse && Gear == Reverse && throttle > 0f)
                {
                    Gear = 1;
                }
            }

            float ratio = GearRatio(s, Gear) * s.FinalDrive;
            float wheelRpm = Mathf.Abs(drivenOmega * ratio) * 60f / (2f * Mathf.PI);

            // Below launch revs the clutch slips: the engine holds revs set by the throttle and
            // still sends its torque through.
            float clutchRpm = s.IdleRpm + (s.LaunchRpm - s.IdleRpm) * throttle;
            Rpm = Mathf.Min(Mathf.Max(wheelRpm, clutchRpm), s.RedlineRpm);

            // Shift on road speed, not wheel spin, so wheelspin doesn't make the box hunt.
            float roadRpm = Mathf.Abs(forwardSpeed / s.WheelRadius * ratio) * 60f / (2f * Mathf.PI);
            _timeInGear += dt;
            if (_shiftTimer > 0f)
            {
                _shiftTimer -= dt;
            }
            else if (Gear != Reverse && _timeInGear > MinTimeInGear)
            {
                if (roadRpm > s.ShiftUpRpm && Gear < TopGear)
                {
                    Shift(Gear + 1, s);
                }
                else if (roadRpm < s.ShiftDownRpm && Gear > 1 && UpshiftRpm(s, Gear - 1, roadRpm) < s.ShiftUpRpm)
                {
                    Shift(Gear - 1, s);
                }
            }

            float engine;
            if (_shiftTimer > 0f)
            {
                engine = 0f;
            }
            else if (throttle > 0f)
            {
                // Soft rev limiter: drive fades out over the last few hundred revs, so a spinning
                // wheel bounces off the limiter smoothly instead of chattering on and off.
                float limiter = Mathf.Clamp01((s.RedlineRpm - wheelRpm) / LimiterBand);
                engine = EngineTorque(s, Rpm) * throttle * limiter;
            }
            else
            {
                // Engine braking only while the clutch is engaged.
                engine = wheelRpm > clutchRpm ? -s.EngineBrakeTorque * (Rpm / s.RedlineRpm) : 0f;
            }

            return engine * ratio * s.DriveEfficiency;
        }
    }
}
