using UnityEngine;

namespace BifrostRally.Core
{
    /// <summary>
    /// Minimal raycast car. Each wheel casts one ray down, pushes the body with a linear
    /// spring and damper, and adds tyre forces at the wheel centre. Weight transfer comes
    /// out of the physics. CarFeel adds the arcade helpers on top.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class RaycastCar : MonoBehaviour
    {
        private class Wheel
        {
            public Vector3 Mount;
            public bool Front;
            public Transform Pivot;
            public Transform Tyre;
            public float Compression;
            public float PrevCompression;
            public float SuspensionForce;
            public bool Grounded;
            public RaycastHit Hit;
            public float SpinDeg;
            public WheelState State;
        }

        public CarSettings Settings = new CarSettings();
        public CarInput Input;
        public int GroundMask = ~0;

        /// <summary>Where a driver sits. May be null.</summary>
        public Transform Seat;

        public CarFeel Feel = new CarFeel();
        public Drivetrain Drivetrain = new Drivetrain();

        public float SpeedKmh { get; private set; }
        public float MaxSlipAngleDeg { get; private set; }
        public float MaxSlipRatio { get; private set; }
        public int GroundedWheels { get; private set; }
        public Vector3 Velocity => _body != null ? _body.GetPointVelocity(_body.worldCenterOfMass) : Vector3.zero;

        /// <summary>Spin around world up, in rad/s.</summary>
        public float YawRate => _body != null ? Vector3.Dot(_body.angularVelocity, Vector3.up) : 0f;

        private Rigidbody _body;
        private Wheel[] _wheels;
        private float _steer;
        private bool _resetSuspension;

        /// <summary>
        /// Wheel order is front-left, front-right, rear-left, rear-right.
        /// Visuals are optional; pivots steer and tyres spin.
        /// </summary>
        public void Init(CarSettings settings, FeelSettings feel, int groundMask, Transform[] pivots, Transform[] tyres)
        {
            Settings = settings;
            Feel.Settings = feel ?? new FeelSettings();
            GroundMask = groundMask;
            _body = GetComponent<Rigidbody>();
            _body.mass = settings.Mass;
            Feel.ApplyMassProperties(_body, settings.CenterOfMassHeight);

            float x = settings.TrackWidth * 0.5f;
            float z = settings.Wheelbase * 0.5f;
            float y = settings.MountHeight;
            Vector3[] mounts =
            {
                new Vector3(-x, y, z), new Vector3(x, y, z),
                new Vector3(-x, y, -z), new Vector3(x, y, -z),
            };

            _wheels = new Wheel[4];
            for (int i = 0; i < 4; i++)
            {
                _wheels[i] = new Wheel
                {
                    Mount = mounts[i],
                    Front = i < 2,
                    Pivot = pivots != null ? pivots[i] : null,
                    Tyre = tyres != null ? tyres[i] : null,
                };
            }
        }

        private void FixedUpdate()
        {
            if (_wheels == null)
            {
                return;
            }

            float dt = Time.fixedDeltaTime;
            CarSettings s = Settings;
            Vector3 up = transform.up;
            Vector3 velocity = _body.GetPointVelocity(_body.worldCenterOfMass);
            float forwardSpeed = Vector3.Dot(velocity, transform.forward);
            SpeedKmh = forwardSpeed * 3.6f;

            _steer = Mathf.MoveTowards(_steer, Mathf.Clamp(Input.Steer, -1f, 1f), s.SteerRate * dt);
            float speedT = Mathf.Clamp01(Mathf.Abs(SpeedKmh) / s.SteerSpeedKmh);
            float steerDeg = _steer * s.MaxSteerDeg * Mathf.Lerp(1f, s.SteerAtSpeedFactor, speedT);

            // Suspension: compression and spring/damper force per wheel.
            float maxDist = s.RestLength + s.WheelRadius;
            GroundedWheels = 0;
            foreach (Wheel w in _wheels)
            {
                Vector3 origin = transform.TransformPoint(w.Mount);
                w.PrevCompression = w.Compression;
                w.Grounded = Physics.Raycast(origin, -up, out w.Hit, maxDist, GroundMask, QueryTriggerInteraction.Ignore);
                w.Compression = w.Grounded ? maxDist - w.Hit.distance : 0f;
                if (_resetSuspension)
                {
                    // No damper kick on the first step after a teleport.
                    w.PrevCompression = w.Compression;
                }
                float compressionVelocity = (w.Compression - w.PrevCompression) / dt;
                w.SuspensionForce = s.SpringRate * w.Compression + s.DamperRate * compressionVelocity;
                if (w.Grounded)
                {
                    GroundedWheels++;
                }
            }

            _resetSuspension = false;
            ApplyAntiRoll(_wheels[0], _wheels[1], s.AntiRollRate);
            ApplyAntiRoll(_wheels[2], _wheels[3], s.AntiRollRate);

            // Drive torque for this step, from the gearbox and the driven wheels' spin.
            bool reversing = Input.Brake > 0f && Input.Throttle <= 0f && forwardSpeed < 1f;
            bool spinModel = s.PacejkaTyres && s.WheelSpin;
            float driveTorque = 0f;
            if (spinModel)
            {
                float throttle = Mathf.Clamp01(reversing ? Input.Brake : Input.Throttle);
                driveTorque = Drivetrain.Step(s, throttle, reversing, DrivenOmega(s), forwardSpeed, dt);
            }

            float massPerWheel = s.Mass * 0.25f;
            MaxSlipAngleDeg = 0f;
            MaxSlipRatio = 0f;
            for (int i = 0; i < _wheels.Length; i++)
            {
                Wheel w = _wheels[i];
                float load = w.Grounded ? Mathf.Max(0f, w.SuspensionForce) : 0f;
                Vector3 normal = w.Grounded ? w.Hit.normal : up;
                Vector3 centre = transform.TransformPoint(w.Mount) - up * (s.RestLength - w.Compression);

                float wheelSteer = w.Front ? steerDeg : 0f;
                Vector3 heading = Quaternion.AngleAxis(wheelSteer, up) * transform.forward;
                Vector3 fwd = Vector3.ProjectOnPlane(heading, normal).normalized;
                Vector3 right = Vector3.Cross(normal, fwd);

                Vector3 v = _body.GetPointVelocity(w.Grounded ? w.Hit.point : centre);
                float vLong = Vector3.Dot(v, fwd);
                float vLat = Vector3.Dot(v, right);
                bool handbraking = Input.Handbrake && !w.Front;

                Vector2 force;
                if (spinModel)
                {
                    force = SpinningTyre(w, vLong, vLat, load, driveTorque, reversing, handbraking, dt);
                }
                else if (w.Grounded)
                {
                    force = ForceDemandTyre(w, vLong, vLat, load, forwardSpeed, reversing, handbraking, massPerWheel, dt);
                    w.State.Omega = vLong / s.WheelRadius;
                }
                else
                {
                    continue;
                }

                if (w.Grounded)
                {
                    _body.AddForceAtPosition(normal * load + fwd * force.x + right * force.y, centre);
                    MaxSlipAngleDeg = Mathf.Max(MaxSlipAngleDeg, Mathf.Abs(Mathf.Atan(w.State.LateralSlip) * Mathf.Rad2Deg));
                    MaxSlipRatio = Mathf.Max(MaxSlipRatio, Mathf.Abs(w.State.SlipRatio));
                }
                w.SpinDeg += w.State.Omega * Mathf.Rad2Deg * dt;
            }

            if (spinModel)
            {
                LockDrivenWheels(s);
            }

            _body.AddForce(-velocity * velocity.magnitude * s.AeroDrag);
            Feel.Step(_body, transform, Input, GroundedWheels, _wheels.Length, dt);
            UpdateVisuals(steerDeg);
        }

        /// <summary>
        /// Teleport and set a forward speed, with wheels already rolling at that speed, so a
        /// scripted test starts clean. Uses velocity changes rather than setting velocity
        /// directly, which keeps it working across Unity versions.
        /// </summary>
        public void ResetMotion(Vector3 position, Quaternion rotation, float forwardSpeed)
        {
            _body.position = position;
            _body.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
            Vector3 target = rotation * Vector3.forward * forwardSpeed;
            _body.AddForce(target - _body.GetPointVelocity(_body.worldCenterOfMass), ForceMode.VelocityChange);
            _body.angularVelocity = Vector3.zero;
            _steer = 0f;
            Drivetrain = new Drivetrain();
            foreach (Wheel w in _wheels)
            {
                w.State = new WheelState { Omega = forwardSpeed / Settings.WheelRadius };
            }
            _resetSuspension = true;
        }

        public bool AllWheelsGrounded => GroundedWheels == _wheels.Length;

        /// <summary>Re-apply mass, centre of mass and inertia after their settings change.</summary>
        public void RefreshMassProperties()
        {
            _body.mass = Settings.Mass;
            Feel.ApplyMassProperties(_body, Settings.CenterOfMassHeight);
        }

        private float AxleShare(CarSettings s, bool front)
        {
            return 0.5f * (front ? 1f - s.RearTorqueShare : s.RearTorqueShare);
        }

        private float DrivenOmega(CarSettings s)
        {
            float sum = 0f;
            float weight = 0f;
            foreach (Wheel w in _wheels)
            {
                float share = AxleShare(s, w.Front);
                sum += w.State.Omega * share;
                weight += share;
            }
            return weight > 0f ? sum / weight : 0f;
        }

        /// <summary>
        /// Limited-slip coupling: pull every driven wheel's spin part way towards their average,
        /// so one unloaded wheel can't take all the power.
        /// </summary>
        private void LockDrivenWheels(CarSettings s)
        {
            if (s.DiffLock <= 0f)
            {
                return;
            }
            float average = DrivenOmega(s);
            foreach (Wheel w in _wheels)
            {
                if (AxleShare(s, w.Front) > 0f)
                {
                    w.State.Omega += Mathf.Clamp01(s.DiffLock) * (average - w.State.Omega);
                }
            }
        }

        /// <summary>Step 2 model: wheel spin, slip ratio and combined slip.</summary>
        private Vector2 SpinningTyre(Wheel w, float vLong, float vLat, float load, float driveTorque, bool reversing,
            bool handbraking, float dt)
        {
            CarSettings s = Settings;
            float loadFactor = Tyre.LoadFactor(load, s.ReferenceLoad, s.LoadSensitivity);
            float share = AxleShare(s, w.Front);
            float brakeShare = w.Front ? s.BrakeBiasFront : 1f - s.BrakeBiasFront;
            float brakeForce = reversing ? 0f : Mathf.Clamp01(Input.Brake) * s.BrakeForce * brakeShare * 0.5f;
            if (handbraking)
            {
                brakeForce += s.HandbrakeForce * 0.5f;
            }
            brakeForce += s.RollingResistance * load;

            var input = new WheelDynamics.Inputs
            {
                VLong = vLong,
                VLat = vLat,
                Load = load,
                DriveTorque = driveTorque * share,
                BrakeTorque = brakeForce * s.WheelRadius,
                LateralPeak = (w.Front ? s.LateralPeakFront : s.LateralPeakRear) * s.Grip * loadFactor * load
                    * (handbraking ? s.HandbrakeGripFactor : 1f),
                LongitudinalPeak = s.LongitudinalPeak * s.Grip * loadFactor * load,
                SpeedKmh = SpeedKmh,
                Inertia = WheelDynamics.Inertia(s, share > 0f),
                Grounded = w.Grounded,
            };
            return WheelDynamics.Step(ref w.State, input, s, dt);
        }

        /// <summary>Step 1 and M1 models: drive and brake forces pushed straight through the tyre.</summary>
        private Vector2 ForceDemandTyre(Wheel w, float vLong, float vLat, float load, float forwardSpeed, bool reversing,
            bool handbraking, float massPerWheel, float dt)
        {
            CarSettings s = Settings;
            float handbrakeGrip = handbraking ? s.HandbrakeGripFactor : 1f;
            float fy;
            float fxLimit;
            if (s.PacejkaTyres)
            {
                float loadFactor = Tyre.LoadFactor(load, s.ReferenceLoad, s.LoadSensitivity);
                float lateralPeak = (w.Front ? s.LateralPeakFront : s.LateralPeakRear) * s.Grip * loadFactor * load;
                w.State.LateralSlip = Tyre.RelaxLateral(w.State.LateralSlip, vLat, vLong, SpeedKmh, s, dt);
                float slipDeg = Mathf.Atan(w.State.LateralSlip) * Mathf.Rad2Deg;
                fy = -lateralPeak * handbrakeGrip * Tyre.MagicFormula(slipDeg, s.PacejkaPeakSlipDeg, s.PacejkaShape, s.PacejkaCurvature);
                fxLimit = s.LongitudinalPeak * s.Grip * loadFactor * load;
            }
            else
            {
                // Slip angle with a speed floor so the tyre stays calm when nearly stopped.
                float slipAngle = Mathf.Atan2(vLat, Mathf.Max(Mathf.Abs(vLong), s.LowSpeedSlipFloor));
                float peak = s.PeakSlipAngleDeg * Mathf.Deg2Rad;
                fy = -Mathf.Sign(slipAngle) * s.Grip * handbrakeGrip * load * GripCurve(Mathf.Abs(slipAngle) / peak, s.GripFalloff);
                fxLimit = s.Grip * load;
                w.State.LateralSlip = Mathf.Tan(slipAngle);
            }

            float fx = 0f;
            if (!w.Front)
            {
                if (reversing)
                {
                    if (forwardSpeed * 3.6f > -s.MaxReverseKmh)
                    {
                        fx -= Input.Brake * s.ReverseForce * 0.5f;
                    }
                }
                else
                {
                    float falloff = Mathf.Clamp01(1f - SpeedKmh / s.MaxSpeedKmh);
                    fx += Mathf.Clamp01(Input.Throttle) * s.DriveForce * 0.5f * falloff;
                }
            }

            float brakeShare = w.Front ? s.BrakeBiasFront : 1f - s.BrakeBiasFront;
            float brake = reversing ? 0f : Mathf.Clamp01(Input.Brake) * s.BrakeForce * brakeShare * 0.5f;
            if (handbraking)
            {
                brake += s.HandbrakeForce * 0.5f;
            }
            brake += s.RollingResistance * load;
            fx += Resist(vLong, brake, massPerWheel, dt);

            if (s.PacejkaTyres)
            {
                // Friction ellipse: the share of forward grip in use is taken off the side grip.
                fx = Mathf.Clamp(fx, -fxLimit, fxLimit);
                float used = fxLimit > 0f ? fx / fxLimit : 0f;
                fy *= Mathf.Sqrt(Mathf.Max(0f, 1f - used * used));
            }
            else
            {
                // Friction circle: drive/brake force uses up side grip.
                float total = Mathf.Sqrt(fx * fx + fy * fy);
                if (total > fxLimit && total > 0f)
                {
                    float scale = fxLimit / total;
                    fx *= scale;
                    fy *= scale;
                }
            }
            w.State.SlipRatio = 0f;
            return new Vector2(fx, fy);
        }

        /// <summary>
        /// Normalised slip x = slip / peak. Rises to 1 at the peak, then falls gently
        /// to (1 - falloff) by three times the peak slip.
        /// </summary>
        public static float GripCurve(float x, float falloff)
        {
            if (x <= 1f)
            {
                return Mathf.Sin(x * Mathf.PI * 0.5f);
            }
            return 1f - falloff * Mathf.Clamp01((x - 1f) * 0.5f);
        }

        private static void ApplyAntiRoll(Wheel left, Wheel right, float rate)
        {
            float force = (left.Compression - right.Compression) * rate;
            if (left.Grounded)
            {
                left.SuspensionForce += force;
            }
            if (right.Grounded)
            {
                right.SuspensionForce -= force;
            }
        }

        /// <summary>
        /// Force opposing the rolling speed, capped so it stops the wheel instead of
        /// pushing it backwards within one step.
        /// </summary>
        private static float Resist(float speed, float force, float mass, float dt)
        {
            float stopForce = Mathf.Abs(speed) * mass / dt;
            return -Mathf.Sign(speed) * Mathf.Min(force, stopForce);
        }

        private void UpdateVisuals(float steerDeg)
        {
            CarSettings s = Settings;
            foreach (Wheel w in _wheels)
            {
                if (w.Pivot != null)
                {
                    w.Pivot.localPosition = w.Mount - Vector3.up * (s.RestLength - w.Compression);
                    w.Pivot.localRotation = Quaternion.Euler(0f, w.Front ? steerDeg : 0f, 0f);
                }
                if (w.Tyre != null)
                {
                    w.Tyre.localRotation = Quaternion.Euler(w.SpinDeg, 0f, 0f) * Quaternion.Euler(0f, 0f, 90f);
                }
            }
        }
    }
}
