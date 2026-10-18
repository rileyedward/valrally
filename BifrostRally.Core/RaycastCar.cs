using UnityEngine;

namespace BifrostRally.Core
{
    /// <summary>
    /// Minimal raycast car. Each wheel casts one ray down, pushes the body with a linear
    /// spring and damper, and adds tyre forces at the wheel centre. Weight transfer comes
    /// out of the physics; there is no extra helper layer yet.
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
        }

        public CarSettings Settings = new CarSettings();
        public CarInput Input;
        public int GroundMask = ~0;

        /// <summary>Where a driver sits. May be null.</summary>
        public Transform Seat;

        public float SpeedKmh { get; private set; }
        public int GroundedWheels { get; private set; }

        private Rigidbody _body;
        private Wheel[] _wheels;
        private float _steer;

        /// <summary>
        /// Wheel order is front-left, front-right, rear-left, rear-right.
        /// Visuals are optional; pivots steer and tyres spin.
        /// </summary>
        public void Init(CarSettings settings, int groundMask, Transform[] pivots, Transform[] tyres)
        {
            Settings = settings;
            GroundMask = groundMask;
            _body = GetComponent<Rigidbody>();
            _body.mass = settings.Mass;
            _body.centerOfMass = new Vector3(0f, settings.CenterOfMassHeight, 0f);

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
                float compressionVelocity = (w.Compression - w.PrevCompression) / dt;
                w.SuspensionForce = s.SpringRate * w.Compression + s.DamperRate * compressionVelocity;
                if (w.Grounded)
                {
                    GroundedWheels++;
                }
            }

            ApplyAntiRoll(_wheels[0], _wheels[1], s.AntiRollRate);
            ApplyAntiRoll(_wheels[2], _wheels[3], s.AntiRollRate);

            // Tyres: lateral grip, drive, brakes, handbrake.
            float massPerWheel = s.Mass * 0.25f;
            foreach (Wheel w in _wheels)
            {
                if (!w.Grounded)
                {
                    continue;
                }

                float load = Mathf.Max(0f, w.SuspensionForce);
                Vector3 normal = w.Hit.normal;
                Vector3 centre = transform.TransformPoint(w.Mount) - up * (s.RestLength - w.Compression);

                float wheelSteer = w.Front ? steerDeg : 0f;
                Vector3 heading = Quaternion.AngleAxis(wheelSteer, up) * transform.forward;
                Vector3 fwd = Vector3.ProjectOnPlane(heading, normal).normalized;
                Vector3 right = Vector3.Cross(normal, fwd);

                Vector3 v = _body.GetPointVelocity(w.Hit.point);
                float vLong = Vector3.Dot(v, fwd);
                float vLat = Vector3.Dot(v, right);

                // Slip angle with a speed floor so the tyre stays calm when nearly stopped.
                float slipAngle = Mathf.Atan2(vLat, Mathf.Max(Mathf.Abs(vLong), s.LowSpeedSlipFloor));
                float peak = s.PeakSlipAngleDeg * Mathf.Deg2Rad;
                bool handbraking = Input.Handbrake && !w.Front;
                float lateralGrip = s.Grip * (handbraking ? s.HandbrakeGripFactor : 1f);
                float fy = -Mathf.Sign(slipAngle) * lateralGrip * load * GripCurve(Mathf.Abs(slipAngle) / peak, s.GripFalloff);

                float fx = 0f;
                bool reversing = Input.Brake > 0f && Input.Throttle <= 0f && forwardSpeed < 1f;
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

                // Friction circle: drive/brake force uses up side grip.
                float limit = s.Grip * load;
                float total = Mathf.Sqrt(fx * fx + fy * fy);
                if (total > limit && total > 0f)
                {
                    float scale = limit / total;
                    fx *= scale;
                    fy *= scale;
                }

                _body.AddForceAtPosition(normal * load + fwd * fx + right * fy, centre);
                w.SpinDeg += vLong / s.WheelRadius * Mathf.Rad2Deg * dt;
            }

            _body.AddForce(-velocity * velocity.magnitude * s.AeroDrag);
            UpdateVisuals(steerDeg);
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
