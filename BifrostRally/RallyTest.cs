using System.Collections;
using System.Collections.Generic;
using System.Text;
using BifrostRally.Core;
using Jotunn.Entities;
using UnityEngine;

namespace BifrostRally.Valheim
{
    /// <summary>
    /// Scripted handling test: builds a flat platform high above the player, then drives a launch,
    /// a step steer, a handbrake turn, a brake-in-turn and a jump, and logs the numbers.
    /// The same surface every time, so results compare between tunes.
    /// </summary>
    internal class RallyTest
    {
        private const float PlatformHeight = 150f;
        private const float PlatformSize = 1500f;

        public static bool Running { get; private set; }

        private readonly StringBuilder _report = new StringBuilder();
        private RaycastCar _car;
        private Vector3 _start;
        private bool _flipped;

        public static void Begin(MonoBehaviour host, bool compareFeel)
        {
            if (Running)
            {
                LiveTuning.Print("rally_test is already running");
                return;
            }
            if (!RallyDriver.IsDriving || RallyDriver.Car == null)
            {
                LiveTuning.Print("Get in the car first (rally_spawn, then the drive key), then run rally_test");
                return;
            }
            host.StartCoroutine(new RallyTest().Run(compareFeel));
        }

        private IEnumerator Run(bool compareFeel)
        {
            Running = true;
            _car = RallyDriver.Car;
            Vector3 origin = _car.transform.position;
            Quaternion originRotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(_car.transform.forward, Vector3.up));
            GameObject platform = BuildPlatform(origin);
            _start = platform.transform.position + Vector3.up * (platform.transform.localScale.y * 0.5f + 0.75f);

            Line($"rally_test start. fixedDeltaTime={Time.fixedDeltaTime:F4}s, tyres={(_car.Settings.PacejkaTyres ? "pacejka" : "simple")}, wheelSpin={_car.Settings.WheelSpin}, grip={_car.Settings.Grip}");

            bool feelBefore = _car.Feel.Settings.Enabled;
            var passes = compareFeel ? new[] { false, true } : new[] { feelBefore };
            foreach (bool feel in passes)
            {
                _car.Feel.Settings.Enabled = feel;
                _car.RefreshMassProperties();
                Line($"--- feel layer {(feel ? "ON" : "OFF")} ---");
                yield return Launch();
                yield return StepSteer();
                yield return HandbrakeTurn();
                yield return BrakeInTurn();
                yield return Jump();
            }
            _car.Feel.Settings.Enabled = feelBefore;
            _car.RefreshMassProperties();

            RallyDriver.ScriptedInput = null;
            if (_car != null)
            {
                _car.ResetMotion(origin + Vector3.up * 1f, originRotation, 0f);
            }
            Object.Destroy(platform);
            Line("rally_test done");
            Jotunn.Logger.LogInfo("\n" + _report);
            global::Console.instance.Print("rally_test done, results are in BepInEx/LogOutput.log");
            Running = false;
        }

        private IEnumerator Launch()
        {
            yield return Reset(0f);
            float t = 0f;
            float t100 = -1f;
            float maxSlip = 0f;
            while (t < 12f && t100 < 0f)
            {
                Drive(new CarInput { Throttle = 1f });
                yield return new WaitForFixedUpdate();
                t += Time.fixedDeltaTime;
                maxSlip = Mathf.Max(maxSlip, _car.MaxSlipRatio);
                Watch();
                if (_car.SpeedKmh >= 100f)
                {
                    t100 = t;
                }
            }
            Line($"launch: 0-100 {(t100 > 0f ? t100.ToString("F2") + "s" : "not reached in 12s")}, gear {_car.Drivetrain.Gear}, max slip ratio {maxSlip:F2}{FlipNote()}");
        }

        private IEnumerator StepSteer()
        {
            yield return Reset(60f);
            yield return Hold(60f, 0f, 1f);
            var yaw = new List<float>();
            float t = 0f, maxSlipAngle = 0f, maxBodySlip = 0f;
            while (t < 3f)
            {
                Drive(Cruise(60f, 1f));
                yield return new WaitForFixedUpdate();
                t += Time.fixedDeltaTime;
                yaw.Add(_car.YawRate * Mathf.Rad2Deg);
                maxSlipAngle = Mathf.Max(maxSlipAngle, _car.MaxSlipAngleDeg);
                maxBodySlip = Mathf.Max(maxBodySlip, Mathf.Abs(BodySlip()));
                Watch();
            }

            int tail = Mathf.Max(1, Mathf.RoundToInt(0.5f / Time.fixedDeltaTime));
            float steady = 0f, peak = 0f;
            for (int i = 0; i < yaw.Count; i++)
            {
                peak = Mathf.Max(peak, Mathf.Abs(yaw[i]));
                if (i >= yaw.Count - tail)
                {
                    steady += Mathf.Abs(yaw[i]) / tail;
                }
            }
            float t90 = -1f;
            for (int i = 0; i < yaw.Count; i++)
            {
                if (Mathf.Abs(yaw[i]) >= 0.9f * steady)
                {
                    t90 = (i + 1) * Time.fixedDeltaTime;
                    break;
                }
            }
            Line($"step steer @60: steady yaw {steady:F1} deg/s, 90% in {t90:F2}s, overshoot {(steady > 0f ? peak / steady : 0f):F2}x, max tyre slip {maxSlipAngle:F1} deg, max body slip {maxBodySlip:F1} deg, speed now {_car.SpeedKmh:F0}{FlipNote()}");
        }

        private IEnumerator HandbrakeTurn()
        {
            yield return Reset(50f);
            yield return Hold(50f, 0f, 0.5f);
            float heading0 = Heading();
            float t = 0f, maxBodySlip = 0f;
            while (t < 0.8f)
            {
                Drive(new CarInput { Steer = 1f, Handbrake = true });
                yield return new WaitForFixedUpdate();
                t += Time.fixedDeltaTime;
                maxBodySlip = Mathf.Max(maxBodySlip, Mathf.Abs(BodySlip()));
                Watch();
            }
            float turned = Mathf.Abs(Mathf.DeltaAngle(heading0, Heading()));

            t = 0f;
            float recovered = -1f;
            float calm = 0f;
            while (t < 4f)
            {
                Drive(new CarInput { Throttle = 0.3f });
                yield return new WaitForFixedUpdate();
                t += Time.fixedDeltaTime;
                float slip = Mathf.Abs(BodySlip());
                maxBodySlip = Mathf.Max(maxBodySlip, slip);
                Watch();
                calm = slip < 5f && Mathf.Abs(_car.YawRate * Mathf.Rad2Deg) < 20f ? calm + Time.fixedDeltaTime : 0f;
                if (recovered < 0f && calm >= 0.3f)
                {
                    recovered = t - 0.3f;
                }
            }
            float total = Mathf.Abs(Mathf.DeltaAngle(heading0, Heading()));
            Line($"handbrake @50: turned {turned:F0} deg during 0.8s pull, {total:F0} deg total, max body slip {maxBodySlip:F0} deg, straight again after {(recovered >= 0f ? recovered.ToString("F2") + "s" : "never (4s)")}, spun {(maxBodySlip > 100f ? "YES" : "no")}{FlipNote()}");
        }

        private IEnumerator BrakeInTurn()
        {
            yield return Reset(70f);
            yield return Hold(70f, 0f, 0.5f);
            yield return Hold(70f, 0.5f, 1f);
            float heading0 = Heading();
            float t = 0f, maxBodySlip = 0f;
            while (t < 2.5f && _car.SpeedKmh > 2f)
            {
                Drive(new CarInput { Steer = 0.5f, Brake = 1f });
                yield return new WaitForFixedUpdate();
                t += Time.fixedDeltaTime;
                maxBodySlip = Mathf.Max(maxBodySlip, Mathf.Abs(BodySlip()));
                Watch();
            }
            Line($"brake in turn @70: stopped/2.5s after {t:F2}s at {_car.SpeedKmh:F0} km/h, turned {Mathf.Abs(Mathf.DeltaAngle(heading0, Heading())):F0} deg while braking, max body slip {maxBodySlip:F0} deg, spun {(maxBodySlip > 100f ? "YES" : "no")}{FlipNote()}");
        }

        private IEnumerator Jump()
        {
            yield return Reset(50f, 3f);
            float t = 0f, airTime = -1f;
            while (t < 3f && airTime < 0f)
            {
                Drive(new CarInput { Throttle = 0.3f });
                yield return new WaitForFixedUpdate();
                t += Time.fixedDeltaTime;
                Watch();
                if (_car.GroundedWheels > 0)
                {
                    airTime = t;
                }
            }

            t = 0f;
            float maxUp = 0f, maxTilt = 0f, settled = -1f, calm = 0f;
            while (t < 2f)
            {
                Drive(new CarInput { Throttle = 0.3f });
                yield return new WaitForFixedUpdate();
                t += Time.fixedDeltaTime;
                Watch();
                float up = _car.Velocity.y;
                maxUp = Mathf.Max(maxUp, up);
                maxTilt = Mathf.Max(maxTilt, Vector3.Angle(_car.transform.up, Vector3.up));
                calm = _car.AllWheelsGrounded && Mathf.Abs(up) < 0.3f ? calm + Time.fixedDeltaTime : 0f;
                if (settled < 0f && calm >= 0.3f)
                {
                    settled = t - 0.3f;
                }
            }
            Line($"jump 3m @50: air {airTime:F2}s, max bounce {maxUp:F2} m/s up, max tilt after landing {maxTilt:F0} deg, settled after {(settled >= 0f ? settled.ToString("F2") + "s" : "not in 2s")}{FlipNote()}");
        }

        private IEnumerator Reset(float speedKmh, float extraHeight = 0f)
        {
            _flipped = false;
            Drive(new CarInput());
            _car.ResetMotion(_start + Vector3.up * extraHeight, Quaternion.identity, speedKmh / 3.6f);
            yield return new WaitForFixedUpdate();
        }

        private IEnumerator Hold(float speedKmh, float steer, float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.fixedDeltaTime)
            {
                Drive(Cruise(speedKmh, steer));
                yield return new WaitForFixedUpdate();
                Watch();
            }
        }

        /// <summary>Simple cruise control so the steering tests run at a known speed.</summary>
        private CarInput Cruise(float speedKmh, float steer)
        {
            float error = speedKmh - _car.SpeedKmh;
            return new CarInput { Steer = steer, Throttle = Mathf.Clamp01(error * 0.15f + 0.2f) };
        }

        private void Drive(CarInput input)
        {
            RallyDriver.ScriptedInput = input;
            _car.Input = input;
        }

        private void Watch()
        {
            if (_car.transform.up.y < 0.2f)
            {
                _flipped = true;
            }
        }

        private string FlipNote() => _flipped ? " [FLIPPED]" : "";

        private float BodySlip()
        {
            Vector3 v = _car.Velocity;
            float forward = Vector3.Dot(v, _car.transform.forward);
            float side = Vector3.Dot(v, _car.transform.right);
            return new Vector2(forward, side).magnitude < 2f ? 0f : Mathf.Atan2(side, forward) * Mathf.Rad2Deg;
        }

        private float Heading()
        {
            Vector3 f = _car.transform.forward;
            return Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
        }

        private void Line(string text)
        {
            _report.AppendLine("[rally_test] " + text);
            global::Console.instance.Print(text);
        }

        private static GameObject BuildPlatform(Vector3 origin)
        {
            GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
            platform.name = "BifrostRallyTestPlatform";
            platform.layer = LayerMask.NameToLayer("Default");
            platform.transform.position = new Vector3(origin.x, origin.y + PlatformHeight, origin.z);
            platform.transform.localScale = new Vector3(PlatformSize, 2f, PlatformSize);
            Material material = RallyDriver.FindCarMaterial();
            if (material != null)
            {
                platform.GetComponent<Renderer>().sharedMaterial = material;
            }
            return platform;
        }
    }

    internal class TestCommand : ConsoleCommand
    {
        private readonly MonoBehaviour _host;

        public TestCommand(MonoBehaviour host)
        {
            _host = host;
        }

        public override string Name => "rally_test";

        public override string Help => "rally_test [compare] - scripted handling test on a flat platform; 'compare' runs it with the feel layer off, then on";

        public override void Run(string[] args)
        {
            RallyTest.Begin(_host, args.Length > 0 && args[0] == "compare");
        }
    }
}
