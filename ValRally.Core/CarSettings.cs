namespace ValRally.Core
{
    /// <summary>
    /// Tunables for the M1 raycast car. Units are SI (kg, m, N, N/m, N·s/m) unless the name says otherwise.
    /// Defaults are our own starting point, loosely in the range of AoR's Group B class setup.
    /// </summary>
    public class CarSettings
    {
        // Body
        public float Mass = 1200f;
        // Body-centre relative. The feel layer lowers it further; about 0.2 m above the wheel
        // centres gives real weight transfer, since tyre forces act at the wheel centres.
        public float CenterOfMassHeight = 0f;
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

        // Tyres. Grip multiplies everything and stands in for the surface until M4.
        public bool PacejkaTyres = true;
        public float Grip = 1.0f;

        // Pacejka tyres: peak force per unit load, curve shape, and slip relaxation
        public float LateralPeakFront = 1.6f;
        public float LateralPeakRear = 1.8f;
        public float LongitudinalPeak = 1.9f;
        public float PacejkaPeakSlipDeg = 9.5f;
        public float PacejkaShape = 1.5f;
        public float PacejkaCurvature = 0f;
        public float LoadSensitivity = 0f;
        public float ReferenceLoad = 3000f;
        public float RelaxationLength = 0.5f;
        public float RelaxationPerKmh = 0.02f;
        public float RelaxationSpeedFloor = 8f;

        // Wheel spin: each wheel has its own spin speed, and slip ratio drives the forward force.
        // Off falls back to pushing the drive/brake force straight through the tyre (step 1).
        public bool WheelSpin = true;
        public float WheelMass = 20f;
        public float DrivelineInertia = 0.5f;
        public float LongitudinalPeakSlip = 0.14f;
        public float LongitudinalShape = 1.65f;
        public float LongRelaxationLength = 0.35f;
        public float SpinSubsteps = 10f;
        // Fraction of each sub-step's spin change that is kept; lower is calmer but lazier wheels.
        public float SpinResponse = 0.4f;

        // Engine and gearbox (automatic). Torque in N·m at the crank.
        public float MaxTorque = 420f;
        public float PeakTorqueRpm = 5000f;
        public float IdleRpm = 1000f;
        public float RedlineRpm = 7800f;
        public float LaunchRpm = 3800f;
        public float EngineBrakeTorque = 60f;
        public float Gear1 = 3.2f;
        public float Gear2 = 2.2f;
        public float Gear3 = 1.6f;
        public float Gear4 = 1.25f;
        public float Gear5 = 1.0f;
        public float ReverseGear = 3.0f;
        public float FinalDrive = 4.6f;
        public float DriveEfficiency = 0.85f;
        public float ShiftUpRpm = 7300f;
        public float ShiftDownRpm = 3600f;
        public float ShiftTime = 0.25f;
        // 0 = front-wheel drive, 1 = rear-wheel drive, in between = AWD split.
        public float RearTorqueShare = 0.65f;
        // 0 = open, 1 = all driven wheels locked together.
        public float DiffLock = 0.3f;

        // Traction control: trims engine torque when the driven wheels spin past the target slip.
        public bool TractionControl = true;
        public float TractionSlipTarget = 0.2f;
        public float TractionStrength = 0.8f;
        public float TractionRecoverRate = 4f;

        // Simple tyres (the M1 model)
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
        public float HandbrakeForce = 12000f;
        public float HandbrakeGripFactor = 0.55f;

        // Steering
        public float MaxSteerDeg = 32f;
        public float SteerAtSpeedFactor = 0.45f;
        public float SteerSpeedKmh = 120f;
        public float SteerRate = 4f;
    }
}
