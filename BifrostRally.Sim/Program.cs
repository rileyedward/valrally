using System;
using System.Globalization;
using BifrostRally.Core;
using UnityEngine;

namespace BifrostRally.Sim
{
    /// <summary>
    /// Flat-ground handling checks without the game: a planar car (forward, sideways and yaw)
    /// with quasi-static weight transfer, driven by the real Core tyre, wheel-spin, drivetrain,
    /// traction-control and feel-layer maths. No suspension or roll dynamics, so it judges
    /// tipping from inside-wheel unload rather than real flips.
    ///
    /// Usage: BifrostRally.Sim [Key=value ...] [Feel.Key=value ...]
    /// </summary>
    internal static class Program
    {
        private const float Dt = 0.02f;
        private const float G = 9.81f;
        private const float BodyWidth = 1.7f;
        private const float BodyLength = 3.9f;

        private static CarSettings _s = new CarSettings();
        private static FeelSettings _f = new FeelSettings();
        private static SurfaceProfile _surface;

        private static int Main(string[] args)
        {
            foreach (string arg in args)
            {
                string[] kv = arg.Split('=');
                if (kv[0] == "Surface")
                {
                    _surface = SurfaceTable.Default((Surface)Enum.Parse(typeof(Surface), kv[1]));
                    continue;
                }
                object target = kv[0].StartsWith("Feel.") ? (object)_f : _s;
                string name = kv[0].StartsWith("Feel.") ? kv[0].Substring(5) : kv[0];
                var field = target.GetType().GetField(name);
                object value = field.FieldType == typeof(bool) ? (object)bool.Parse(kv[1]) : float.Parse(kv[1], CultureInfo.InvariantCulture);
                field.SetValue(target, value);
            }

            if (_surface == null)
            {
                _surface = new SurfaceProfile { RollingDrag = _s.RollingResistance };
            }
            bool ok = true;
            foreach (bool feel in new[] { false, true })
            {
                _f.Enabled = feel;
                Console.WriteLine($"--- feel layer {(feel ? "ON" : "OFF")} ---");
                ok &= Launch();
                ok &= StepSteer();
                ok &= HandbrakeTurn();
                ok &= BrakeInTurn();
            }
            Console.WriteLine(ok ? "ALL CHECKS PASSED" : "SOME CHECKS FAILED");
            return ok ? 0 : 1;
        }

        private class Car
        {
            public float U, V, R, Yaw;
            public float Ax, Ay;
            public float Steer;
            public readonly WheelState[] Wheels = new WheelState[4];
            public readonly Drivetrain Drivetrain = new Drivetrain();
            public readonly TractionControl Traction = new TractionControl();
            public float MinLoadRatio = 1f;
            public float MaxTyreSlipDeg;
            public float GearChanges;
            private int _lastGear = 1;

            public float SpeedKmh => U * 3.6f;
            public float BodySlipDeg => new Vector2(U, V).magnitude < 2f ? 0f : Mathf.Atan2(V, U) * Mathf.Rad2Deg;

            public Car(float speed)
            {
                U = speed;
                for (int i = 0; i < 4; i++)
                {
                    Wheels[i].Omega = speed / _s.WheelRadius;
                }
            }

            private static bool Front(int i) => i < 2;
            private static float X(int i) => (i % 2 == 0 ? -0.5f : 0.5f) * _s.TrackWidth;
            private static float Z(int i) => (Front(i) ? 0.5f : -0.5f) * _s.Wheelbase;
            private static float Share(int i) => 0.5f * (Front(i) ? 1f - _s.RearTorqueShare : _s.RearTorqueShare);

            /// <summary>CoM height above the wheel centres, which is the lever for weight transfer.</summary>
            private static float TransferHeight()
            {
                float drop = _f.Enabled ? _f.CenterOfMassDrop : 0f;
                float sag = _s.Mass * G * 0.25f / _s.SpringRate;
                float wheelCentre = _s.MountHeight - (_s.RestLength - sag);
                return Mathf.Max(0f, _s.CenterOfMassHeight - drop - wheelCentre);
            }

            public void Step(CarInput input)
            {
                CarSettings s = _s;
                Steer = Mathf.MoveTowards(Steer, input.Steer, s.SteerRate * Dt);
                float steerDeg = RaycastCar.SteerAngle(s, Steer, SpeedKmh);
                bool reversing = input.Brake > 0f && input.Throttle <= 0f && U < 1f;

                float drivenOmega = 0f, weight = 0f, drivenSlip = 0f;
                for (int i = 0; i < 4; i++)
                {
                    drivenOmega += Wheels[i].Omega * Share(i);
                    weight += Share(i);
                    if (Share(i) > 0f)
                    {
                        drivenSlip = Mathf.Max(drivenSlip, Mathf.Abs(Wheels[i].SlipRatio));
                    }
                }
                float throttle = Mathf.Clamp01(reversing ? input.Brake : input.Throttle);
                float drive = Drivetrain.Step(s, throttle, reversing, drivenOmega / weight, U, Dt);
                if (drive * Mathf.Sign(U + 0.01f) > 0f)
                {
                    drive *= Traction.Step(s, drivenSlip, Dt);
                }
                if (Drivetrain.Gear != _lastGear)
                {
                    GearChanges++;
                    _lastGear = Drivetrain.Gear;
                }

                float h = TransferHeight();
                float longTransfer = s.Mass * Ax * h / s.Wheelbase;
                float latTransfer = s.Mass * Ay * h / s.TrackWidth;
                float fu = 0f, fv = 0f, moment = 0f;
                MaxTyreSlipDeg = 0f;
                for (int i = 0; i < 4; i++)
                {
                    float stat = s.Mass * G * 0.25f;
                    float load = stat + (Front(i) ? -0.5f : 0.5f) * longTransfer + (X(i) < 0f ? 0.5f : -0.5f) * latTransfer;
                    MinLoadRatio = Mathf.Min(MinLoadRatio, load / stat);
                    load = Mathf.Max(0f, load);

                    float delta = (Front(i) ? steerDeg : 0f) * Mathf.Deg2Rad;
                    float cos = Mathf.Cos(delta), sin = Mathf.Sin(delta);
                    float up = U - R * X(i);
                    float vp = V + R * Z(i);
                    float vLong = up * cos + vp * sin;
                    float vLat = -up * sin + vp * cos;

                    bool handbraking = input.Handbrake && !Front(i);
                    float brakeShare = Front(i) ? s.BrakeBiasFront : 1f - s.BrakeBiasFront;
                    float brake = (reversing ? 0f : input.Brake * s.BrakeForce * brakeShare * 0.5f)
                        + (handbraking ? s.HandbrakeForce * 0.5f : 0f) + _surface.RollingDrag * load;
                    float loadFactor = Tyre.LoadFactor(load, s.ReferenceLoad, s.LoadSensitivity);

                    var wheelInput = new WheelDynamics.Inputs
                    {
                        VLong = vLong,
                        VLat = vLat,
                        Load = load,
                        DriveTorque = drive * Share(i),
                        BrakeTorque = brake * s.WheelRadius,
                        LateralPeak = (Front(i) ? s.LateralPeakFront : s.LateralPeakRear) * s.Grip * _surface.Grip * loadFactor * load * (handbraking ? s.HandbrakeGripFactor : 1f),
                        LongitudinalPeak = s.LongitudinalPeak * s.Grip * _surface.Grip * loadFactor * load,
                        SpeedKmh = SpeedKmh,
                        Inertia = WheelDynamics.Inertia(s, Share(i) > 0f),
                        Grounded = true,
                        PeakSlipScale = _surface.PeakSlipScale,
                        ShapeScale = _surface.ShapeScale,
                    };
                    Vector2 f = WheelDynamics.Step(ref Wheels[i], wheelInput, s, Dt);
                    MaxTyreSlipDeg = Mathf.Max(MaxTyreSlipDeg, Mathf.Abs(Mathf.Atan(Wheels[i].LateralSlip) * Mathf.Rad2Deg));

                    float wu = f.x * cos - f.y * sin;
                    float wv = f.x * sin + f.y * cos;
                    fu += wu;
                    fv += wv;
                    moment += Z(i) * wv - X(i) * wu;
                }

                if (s.DiffLock > 0f)
                {
                    float avg = 0f;
                    for (int i = 0; i < 4; i++) avg += Wheels[i].Omega * Share(i) / weight;
                    for (int i = 0; i < 4; i++) if (Share(i) > 0f) Wheels[i].Omega += Mathf.Clamp01(s.DiffLock) * (avg - Wheels[i].Omega);
                }

                float speed = new Vector2(U, V).magnitude;
                fu -= s.AeroDrag * U * speed;
                fv -= s.AeroDrag * V * speed;
                moment += CarFeel.SteerYaw(_f, input.Steer, U, V, true) + CarFeel.SlideYaw(_f, U, V, !input.Handbrake);

                float inertia = s.Mass / 12f * (BodyWidth * BodyWidth + BodyLength * BodyLength) * (_f.Enabled ? _f.InertiaScale : 1f);
                Ax = fu / s.Mass;
                Ay = fv / s.Mass;
                U += (Ax + V * R) * Dt;
                V += (Ay - U * R) * Dt;
                R += moment / inertia * Dt;
                Yaw += R * Mathf.Rad2Deg * Dt;
            }

            public CarInput Cruise(float kmh, float steer)
            {
                return new CarInput { Steer = steer, Throttle = Mathf.Clamp01((kmh - SpeedKmh) * 0.15f + 0.2f) };
            }
        }

        private static bool Check(bool pass, string what)
        {
            if (!pass)
            {
                Console.WriteLine("    FAIL: " + what);
            }
            return pass;
        }

        private static bool Launch()
        {
            var car = new Car(0f);
            float t = 0f, t100 = -1f;
            while (t < 15f && t100 < 0f)
            {
                car.Step(new CarInput { Throttle = 1f });
                t += Dt;
                if (car.SpeedKmh >= 100f) t100 = t;
            }
            Console.WriteLine($"  launch: 0-100 {t100:F2}s, gear changes {car.GearChanges}, TC trim now {car.Traction.Factor:F2}");
            return Check(t100 > 0f && t100 < 8f, "0-100 under 8 s") & Check(car.GearChanges <= 3, "no gear hunting (at most 3 shifts to 100)");
        }

        private static bool StepSteer()
        {
            var car = new Car(60f / 3.6f);
            for (float t = 0f; t < 1f; t += Dt) car.Step(car.Cruise(60f, 0f));
            float[] yaw = new float[(int)(3f / Dt)];
            float maxBody = 0f;
            for (int i = 0; i < yaw.Length; i++)
            {
                car.Step(car.Cruise(60f, 1f));
                yaw[i] = car.R * Mathf.Rad2Deg;
                maxBody = Mathf.Max(maxBody, Mathf.Abs(car.BodySlipDeg));
            }
            int tail = (int)(0.5f / Dt);
            float steady = 0f, peak = 0f;
            for (int i = 0; i < yaw.Length; i++)
            {
                peak = Mathf.Max(peak, Mathf.Abs(yaw[i]));
                if (i >= yaw.Length - tail) steady += Mathf.Abs(yaw[i]) / tail;
            }
            float t90 = -1f;
            for (int i = 0; i < yaw.Length; i++) if (Mathf.Abs(yaw[i]) >= 0.9f * steady) { t90 = (i + 1) * Dt; break; }
            float lateralG = Mathf.Abs(car.U * car.R) / G;
            Console.WriteLine($"  step steer @60: steady yaw {steady:F1} deg/s ({lateralG:F2} g), 90% in {t90:F2}s, overshoot {peak / Mathf.Max(steady, 0.01f):F2}x, max body slip {maxBody:F1} deg, max tyre slip {car.MaxTyreSlipDeg:F1} deg, min wheel load {car.MinLoadRatio:F2}");
            return Check(t90 > 0f && t90 < 0.6f, "yaw builds within 0.6 s") & Check(peak / Mathf.Max(steady, 0.01f) < 1.5f, "overshoot under 1.5x")
                & Check(maxBody < 25f, "no spin-out on step steer (body slip under 25 deg)") & Check(car.MinLoadRatio > 0f, "inside wheels stay loaded");
        }

        private static bool HandbrakeTurn()
        {
            var car = new Car(50f / 3.6f);
            for (float t = 0f; t < 0.5f; t += Dt) car.Step(car.Cruise(50f, 0f));
            float yaw0 = car.Yaw, maxBody = 0f;
            for (float t = 0f; t < 0.8f; t += Dt)
            {
                car.Step(new CarInput { Steer = 1f, Handbrake = true });
                maxBody = Mathf.Max(maxBody, Mathf.Abs(car.BodySlipDeg));
            }
            float turned = Mathf.Abs(car.Yaw - yaw0);
            float recovered = -1f, calm = 0f;
            for (float t = 0f; t < 4f; t += Dt)
            {
                car.Step(new CarInput { Throttle = 0.3f });
                float slip = Mathf.Abs(car.BodySlipDeg);
                maxBody = Mathf.Max(maxBody, slip);
                calm = slip < 5f && Mathf.Abs(car.R * Mathf.Rad2Deg) < 20f ? calm + Dt : 0f;
                if (recovered < 0f && calm >= 0.3f) recovered = t - 0.3f;
            }
            Console.WriteLine($"  handbrake @50: turned {turned:F0} deg in 0.8s, {Mathf.Abs(car.Yaw - yaw0):F0} deg total, max body slip {maxBody:F0} deg, straight after {recovered:F2}s, min wheel load {car.MinLoadRatio:F2}");
            return Check(turned > 25f, "handbrake rotates the car (over 25 deg)") & Check(recovered >= 0f && recovered < 3f, "recovers within 3 s");
        }

        private static bool BrakeInTurn()
        {
            var car = new Car(70f / 3.6f);
            for (float t = 0f; t < 0.5f; t += Dt) car.Step(car.Cruise(70f, 0f));
            for (float t = 0f; t < 1f; t += Dt) car.Step(car.Cruise(70f, 0.5f));
            float maxBody = 0f, time = 0f;
            while (time < 2.5f && car.SpeedKmh > 2f)
            {
                car.Step(new CarInput { Steer = 0.5f, Brake = 1f });
                maxBody = Mathf.Max(maxBody, Mathf.Abs(car.BodySlipDeg));
                time += Dt;
            }
            Console.WriteLine($"  brake in turn @70: stopped in {time:F2}s, max body slip {maxBody:F0} deg, min wheel load {car.MinLoadRatio:F2}");
            return Check(maxBody < 60f, "no spin under braking in a turn (body slip under 60 deg)");
        }
    }
}
