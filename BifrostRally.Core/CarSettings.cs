namespace BifrostRally.Core
{
    /// <summary>
    /// Tunables for the M1 raycast car. Units are SI (kg, m, N, N/m, N·s/m) unless the name says otherwise.
    /// Defaults are our own starting point, loosely in the range of AoR's Group B class setup.
    /// </summary>
    public class CarSettings
    {
        // Body
        public float Mass = 1200f;
        public float CenterOfMassHeight = -0.35f;
        public float AeroDrag = 0.45f;

        // Layout (local to the body centre)
        public float Wheelbase = 2.5f;
        public float TrackWidth = 1.6f;
        public float MountHeight = -0.1f;

        // Suspension (per wheel)
        public float RestLength = 0.35f;
        public float WheelRadius = 0.33f;
        public float SpringRate = 26000f;
        public float DamperRate = 5500f;
        public float AntiRollRate = 15000f;

        // Tyres
        public float Grip = 1.0f;
        public float PeakSlipAngleDeg = 8f;
        public float GripFalloff = 0.15f;
        public float LowSpeedSlipFloor = 4f;
        public float RollingResistance = 0.015f;

        // Drive and brakes
        public float DriveForce = 6500f;
        public float MaxSpeedKmh = 160f;
        public float ReverseForce = 3000f;
        public float MaxReverseKmh = 30f;
        public float BrakeForce = 12000f;
        public float BrakeBiasFront = 0.6f;
        public float HandbrakeForce = 4000f;
        public float HandbrakeGripFactor = 0.35f;

        // Steering
        public float MaxSteerDeg = 32f;
        public float SteerAtSpeedFactor = 0.45f;
        public float SteerSpeedKmh = 120f;
        public float SteerRate = 4f;
    }
}
