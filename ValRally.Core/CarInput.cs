namespace ValRally.Core
{
    /// <summary>
    /// Driver input for one physics step. The host game fills this in.
    /// </summary>
    public struct CarInput
    {
        /// <summary>-1 full left, +1 full right.</summary>
        public float Steer;

        /// <summary>0..1.</summary>
        public float Throttle;

        /// <summary>0..1. Also reverses when the car is nearly stopped.</summary>
        public float Brake;

        public bool Handbrake;
    }
}
