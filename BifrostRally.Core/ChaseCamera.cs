using UnityEngine;

namespace BifrostRally.Core
{
    public class ChaseCameraSettings
    {
        // Measured from the aim point, which sits AimAhead in front of the car.
        public float Distance = 7.5f;
        public float Height = 2.6f;
        public float AimHeight = 1f;
        public float AimAhead = 1.5f;
        public float Fov = 60f;
        public float FastFovScale = 1.12f;
        public float FastSpeedStart = 20f;
        public float FastSpeedEnd = 35f;
        public float FastAimRaise = 0.3f;
        // How fast the camera swings round behind the car. Low values let the car rotate in frame first.
        public float YawFollowRate = 1.5f;
        // 0 = follow where the car points, 1 = follow where it is travelling (reads drifts better).
        public float VelocityHeading = 0.7f;
        public float VelocityHeadingMinSpeed = 4f;
        public float HeightFollowRate = 4f;
        public float SpinHoldStart = 2.8f;
        public float SpinHoldEnd = 3.5f;
        public float SpeedSmoothTime = 0.75f;
        public float MinDistanceScale = 0.3f;

        private static readonly System.Reflection.FieldInfo[] Fields = typeof(ChaseCameraSettings).GetFields();

        /// <summary>Blend every setting between two presets, for a smooth switch.</summary>
        public static ChaseCameraSettings Lerp(ChaseCameraSettings a, ChaseCameraSettings b, float t)
        {
            var result = new ChaseCameraSettings();
            foreach (System.Reflection.FieldInfo field in Fields)
            {
                field.SetValue(result, Mathf.Lerp((float)field.GetValue(a), (float)field.GetValue(b), t));
            }
            return result;
        }

        /// <summary>Close, low chase like a traditional racing game. The default.</summary>
        public static ChaseCameraSettings Close() => new ChaseCameraSettings();

        /// <summary>High, far, tilted view in the spirit of AoR's default camera.</summary>
        public static ChaseCameraSettings High() => new ChaseCameraSettings
        {
            Distance = 30f,
            Height = 17f,
            AimHeight = 0.5f,
            AimAhead = 0f,
            Fov = 40f,
            FastFovScale = 1.1f,
            FastAimRaise = 2f,
            YawFollowRate = 1.5f,
            VelocityHeading = 0f,
            HeightFollowRate = 2f,
            MinDistanceScale = 0.45f,
        };
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
            float targetYaw = TargetHeading(carForward, carVelocity, s);
            _yaw = Mathf.LerpAngle(_yaw, targetYaw, Mathf.Clamp01(s.YawFollowRate * follow * dt));
            _height = Mathf.Lerp(_height, carPosition.y, Mathf.Clamp01(s.HeightFollowRate * dt));

            float speed = new Vector3(carVelocity.x, 0f, carVelocity.z).magnitude;
            float fastTarget = Mathf.InverseLerp(s.FastSpeedStart, s.FastSpeedEnd, speed);
            _fastT = Mathf.SmoothDamp(_fastT, fastTarget, ref _fastVelocity, s.SpeedSmoothTime, Mathf.Infinity, dt);

            Quaternion heading = Quaternion.Euler(0f, _yaw, 0f);
            Vector3 aim = carPosition + Vector3.up * (s.AimHeight + s.FastAimRaise * _fastT) + heading * Vector3.forward * s.AimAhead;
            Vector3 back = heading * Vector3.back;
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

        /// <summary>
        /// Blend the car's facing towards its direction of travel. Only when moving forwards at speed,
        /// so reversing or crawling never swings the camera round to the front.
        /// </summary>
        private float TargetHeading(Vector3 carForward, Vector3 carVelocity, ChaseCameraSettings s)
        {
            float facing = HeadingOf(carForward, _yaw);
            Vector3 flat = new Vector3(carVelocity.x, 0f, carVelocity.z);
            float speed = flat.magnitude;
            if (s.VelocityHeading <= 0f || speed < s.VelocityHeadingMinSpeed || Vector3.Dot(flat, carForward) <= 0f)
            {
                return facing;
            }
            float travel = HeadingOf(flat, facing);
            float weight = s.VelocityHeading * Mathf.InverseLerp(s.VelocityHeadingMinSpeed, s.VelocityHeadingMinSpeed * 2f, speed);
            return Mathf.LerpAngle(facing, travel, weight);
        }

        private static float HeadingOf(Vector3 forward, float fallback)
        {
            Vector3 flat = new Vector3(forward.x, 0f, forward.z);
            return flat.sqrMagnitude < 1e-4f ? fallback : Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
        }
    }
}
