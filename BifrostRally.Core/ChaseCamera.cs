using UnityEngine;

namespace BifrostRally.Core
{
    public class ChaseCameraSettings
    {
        public float Distance = 30f;
        public float Height = 17f;
        public float AimHeight = 0.5f;
        public float Fov = 40f;
        public float FastFovScale = 1.1f;
        public float FastSpeedStart = 20f;
        public float FastSpeedEnd = 35f;
        public float FastAimRaise = 2f;
        public float YawFollowRate = 1.5f;
        public float HeightFollowRate = 2f;
        public float SpinHoldStart = 2.8f;
        public float SpinHoldEnd = 3.5f;
        public float SpeedSmoothTime = 0.75f;
        public float MinDistanceScale = 0.45f;
    }

    public struct CameraPose
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public float Fov;

        public static CameraPose Lerp(CameraPose a, CameraPose b, float t)
        {
            return new CameraPose
            {
                Position = Vector3.Lerp(a.Position, b.Position, t),
                Rotation = Quaternion.Slerp(a.Rotation, b.Rotation, t),
                Fov = Mathf.Lerp(a.Fov, b.Fov, t),
            };
        }
    }

    /// <summary>
    /// High, tilted chase view. The camera sits behind the car along a lagged heading,
    /// trails its height, and looks at the car with world up, so it never rolls.
    /// The heading stops following while the car spins hard, so flips don't whip the view.
    /// </summary>
    public class ChaseCamera
    {
        public ChaseCameraSettings Settings = new ChaseCameraSettings();

        private float _yaw;
        private float _height;
        private float _fastT;
        private float _fastVelocity;

        /// <summary>Snap the camera state to the car, e.g. when getting in.</summary>
        public void Reset(Vector3 carPosition, Vector3 carForward)
        {
            _yaw = HeadingOf(carForward, 0f);
            _height = carPosition.y;
            _fastT = 0f;
            _fastVelocity = 0f;
        }

        /// <param name="blocked">
        /// Optional line-of-sight test from the aim point towards the camera. Returns the free
        /// distance, or a negative number when the path is clear.
        /// </param>
        public CameraPose Update(Vector3 carPosition, Vector3 carForward, Vector3 carVelocity, float yawRate, float dt,
            System.Func<Vector3, Vector3, float, float> blocked = null)
        {
            ChaseCameraSettings s = Settings;

            // Fade heading follow out while the car spins quickly.
            float spin = Mathf.Abs(yawRate);
            float follow = 1f - Mathf.InverseLerp(s.SpinHoldStart, s.SpinHoldEnd, spin);
            float targetYaw = HeadingOf(carForward, _yaw);
            _yaw = Mathf.LerpAngle(_yaw, targetYaw, Mathf.Clamp01(s.YawFollowRate * follow * dt));
            _height = Mathf.Lerp(_height, carPosition.y, Mathf.Clamp01(s.HeightFollowRate * dt));

            float speed = new Vector3(carVelocity.x, 0f, carVelocity.z).magnitude;
            float fastTarget = Mathf.InverseLerp(s.FastSpeedStart, s.FastSpeedEnd, speed);
            _fastT = Mathf.SmoothDamp(_fastT, fastTarget, ref _fastVelocity, s.SpeedSmoothTime, Mathf.Infinity, dt);

            Vector3 aim = carPosition + Vector3.up * (s.AimHeight + s.FastAimRaise * _fastT);
            Vector3 back = Quaternion.Euler(0f, _yaw, 0f) * Vector3.back;
            Vector3 offset = back * s.Distance + Vector3.up * (_height - carPosition.y + s.Height);

            float length = offset.magnitude;
            if (blocked != null && length > 0f)
            {
                float free = blocked(aim, offset / length, length);
                if (free >= 0f)
                {
                    offset *= Mathf.Max(s.MinDistanceScale, free / length);
                }
            }

            Vector3 position = aim + offset;
            return new CameraPose
            {
                Position = position,
                Rotation = Quaternion.LookRotation(aim - position, Vector3.up),
                Fov = s.Fov * Mathf.Lerp(1f, s.FastFovScale, _fastT),
            };
        }

        private static float HeadingOf(Vector3 forward, float fallback)
        {
            Vector3 flat = new Vector3(forward.x, 0f, forward.z);
            return flat.sqrMagnitude < 1e-4f ? fallback : Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
        }
    }
}
