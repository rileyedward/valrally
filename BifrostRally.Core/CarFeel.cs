using UnityEngine;

namespace BifrostRally.Core
{
    /// <summary>
    /// Strengths for the feel layer. Set any strength to 0 to switch that piece off.
    /// </summary>
    public class FeelSettings
    {
        public bool Enabled = true;

        // Lower centre of mass and heavier rotation: calmer, less tippy.
        public float CenterOfMassDrop = 0.1f;
        public float InertiaScale = 1.5f;

        // Extra yaw from the steering input, so the car turns in eagerly.
        public float SteerYawTorque = 5000f;
        public float SteerYawMaxTorque = 8000f;
        public float SteerYawFullSpeed = 45f;
        public float AssistMinSpeedKmh = 35f;

        // Turns the nose towards the direction of travel when sliding, so drifts are catchable.
        public float SlideStabilityTorque = 3000f;
        public float SlideFullAngleDeg = 15f;
        public float SlideMinSpeed = 7f;

        // In the air: rotate back towards level (deg/s per degree of tilt, capped).
        public float AirLevelGain = 1.5f;
        public float AirLevelMaxRate = 30f;

        // On landing: halve spin and cut bounce.
        public float LandingSpinKeep = 0.5f;
        public float LandingBounceKeep = 0.1f;
        public float LandingWindow = 0.15f;
        public float LandingMaxSpin = 0.52f;

        // Downforce = coefficient · horizontal speed², capped.
        public float Downforce = 1.5f;
        public float DownforceMax = 8000f;
    }

    /// <summary>
    /// Non-physical helpers on top of the physics, in the spirit of AoR's arcade layer.
    /// Each piece is its own term so it can be tuned or switched off alone.
    /// </summary>
    public class CarFeel
    {
        public FeelSettings Settings = new FeelSettings();

        private float _airTime;
        private float _firstContact = -1f;
        private bool _wasAirborne;

        /// <summary>Applied once when the car is built (needs a valid collider-based inertia).</summary>
        public void ApplyMassProperties(Rigidbody body, float baseCenterOfMassHeight)
        {
            FeelSettings f = Settings;
            float drop = f.Enabled ? f.CenterOfMassDrop : 0f;
            body.centerOfMass = new Vector3(0f, baseCenterOfMassHeight - drop, 0f);
            body.ResetInertiaTensor();
            if (f.Enabled && f.InertiaScale > 0f)
            {
                body.inertiaTensor *= f.InertiaScale;
            }
        }

        public void Step(Rigidbody body, Transform t, CarInput input, int groundedWheels, int wheelCount, float dt)
        {
            FeelSettings f = Settings;
            if (!f.Enabled)
            {
                return;
            }

            Vector3 velocity = body.GetPointVelocity(body.worldCenterOfMass);
            float forwardSpeed = Vector3.Dot(velocity, t.forward);
            float lateralSpeed = Vector3.Dot(velocity, t.right);
            bool allGrounded = groundedWheels == wheelCount;

            // Slide help stays off while the handbrake is held, so a handbrake flick can rotate fully.
            float yaw = SteerYaw(f, input.Steer, forwardSpeed, allGrounded) + SlideYaw(f, forwardSpeed, lateralSpeed, groundedWheels > 0 && !input.Handbrake);
            if (yaw != 0f)
            {
                body.AddTorque(t.up * yaw);
            }

            Vector3 flat = new Vector3(velocity.x, 0f, velocity.z);
            if (f.Downforce > 0f && groundedWheels > 0)
            {
                body.AddForce(-t.up * Mathf.Min(f.Downforce * flat.sqrMagnitude, f.DownforceMax));
            }

            UpdateAirAndLanding(body, t, groundedWheels, wheelCount, dt);
        }

        /// <summary>Yaw torque from the raw steering input, so the car reacts before the wheels do.</summary>
        public static float SteerYaw(FeelSettings f, float steer, float forwardSpeed, bool allGrounded)
        {
            if (!f.Enabled || !allGrounded || Mathf.Abs(forwardSpeed) * 3.6f <= f.AssistMinSpeedKmh)
            {
                return 0f;
            }
            float speedScale = Mathf.Clamp01(Mathf.Abs(forwardSpeed) / f.SteerYawFullSpeed);
            float yaw = Mathf.Clamp(steer * f.SteerYawTorque * speedScale, -f.SteerYawMaxTorque, f.SteerYawMaxTorque);
            return yaw * Mathf.Sign(forwardSpeed);
        }

        /// <summary>
        /// Positive slip means travelling to the right of the nose; positive yaw turns the nose
        /// right, so this lines the car up with where it is going.
        /// </summary>
        public static float SlideYaw(FeelSettings f, float forwardSpeed, float lateralSpeed, bool grounded)
        {
            if (!f.Enabled || !grounded || forwardSpeed <= f.SlideMinSpeed)
            {
                return 0f;
            }
            float slipDeg = Mathf.Atan2(lateralSpeed, forwardSpeed) * Mathf.Rad2Deg;
            float amount = Mathf.Clamp01(Mathf.Abs(slipDeg) / f.SlideFullAngleDeg);
            return Mathf.Sign(slipDeg) * amount * f.SlideStabilityTorque;
        }

        private void UpdateAirAndLanding(Rigidbody body, Transform t, int groundedWheels, int wheelCount, float dt)
        {
            FeelSettings f = Settings;
            if (groundedWheels == 0)
            {
                _airTime += dt;
                _firstContact = -1f;
                if (_airTime > 0.1f && f.AirLevelGain > 0f)
                {
                    Vector3 heading = Vector3.ProjectOnPlane(t.forward, Vector3.up);
                    if (heading.sqrMagnitude > 1e-4f)
                    {
                        Quaternion level = Quaternion.LookRotation(heading, Vector3.up);
                        float tilt = Quaternion.Angle(body.rotation, level);
                        float rate = Mathf.Min(f.AirLevelMaxRate, tilt * f.AirLevelGain);
                        body.MoveRotation(Quaternion.RotateTowards(body.rotation, level, rate * dt));
                    }
                }
                _wasAirborne = _airTime > 0.25f;
                return;
            }

            _airTime = 0f;
            if (!_wasAirborne)
            {
                return;
            }

            if (_firstContact < 0f)
            {
                _firstContact = 0f;
            }
            _firstContact += dt;

            if (groundedWheels == wheelCount)
            {
                // A clean four-wheel landing: settle it instead of letting it bounce or tumble.
                if (_firstContact <= f.LandingWindow && body.angularVelocity.magnitude < f.LandingMaxSpin)
                {
                    body.angularVelocity *= f.LandingSpinKeep;
                    float up = Vector3.Dot(body.GetPointVelocity(body.worldCenterOfMass), Vector3.up);
                    if (up > 0f)
                    {
                        body.AddForce(Vector3.up * (-up * (1f - f.LandingBounceKeep)), ForceMode.VelocityChange);
                    }
                }
                _wasAirborne = false;
            }
            else if (_firstContact > f.LandingWindow)
            {
                _wasAirborne = false;
            }
        }
    }
}
