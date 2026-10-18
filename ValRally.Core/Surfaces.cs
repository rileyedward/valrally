using System;
using System.Collections.Generic;
using UnityEngine;

namespace ValRally.Core
{
    public enum Surface
    {
        Tarmac,
        Gravel,
        SoftDirt,
        OffRoad,
        Mud,
        Snow,
        DeepSnow,
        LooseSand,
        Ash,
        Wood,
        Stone,
        Ice,
        Water,
    }

    /// <summary>How one surface changes the tyre.</summary>
    public class SurfaceProfile
    {
        /// <summary>Multiplies every tyre peak force.</summary>
        public float Grip = 1f;

        /// <summary>Rolling drag as a fraction of wheel load (replaces CarSettings.RollingResistance).</summary>
        public float RollingDrag = 0.018f;

        /// <summary>Loose surfaces reach peak grip at larger slip. Multiplies both peak slips.</summary>
        public float PeakSlipScale = 1f;

        /// <summary>Below 1 flattens the curve past the peak, so a slide holds more grip.</summary>
        public float ShapeScale = 1f;

        public SurfaceProfile Clone() => (SurfaceProfile)MemberwiseClone();
    }

    /// <summary>The host game says what a wheel's ray hit.</summary>
    public interface ISurfaceProvider
    {
        Surface Query(RaycastHit hit);
    }

    /// <summary>
    /// Default profiles. Grip and rolling drag lean on AoR's surface table where it has one
    /// (tarmac, gravel, grass, snow); the rest and the slip-curve tweaks are our own guesses.
    /// </summary>
    public static class SurfaceTable
    {
        public static SurfaceProfile Default(Surface surface)
        {
            switch (surface)
            {
                case Surface.Tarmac: return P(1.08f, 0.018f, 1.0f, 1.0f);
                case Surface.Gravel: return P(0.97f, 0.059f, 1.3f, 0.85f);
                case Surface.SoftDirt: return P(0.85f, 0.07f, 1.4f, 0.8f);
                case Surface.OffRoad: return P(0.75f, 0.08f, 1.4f, 0.8f);
                case Surface.Mud: return P(0.6f, 0.12f, 1.6f, 0.75f);
                case Surface.Snow: return P(0.82f, 0.058f, 1.4f, 0.8f);
                case Surface.DeepSnow: return P(0.6f, 0.14f, 1.6f, 0.75f);
                case Surface.LooseSand: return P(0.65f, 0.15f, 1.6f, 0.75f);
                case Surface.Ash: return P(0.8f, 0.07f, 1.3f, 0.85f);
                case Surface.Wood: return P(0.95f, 0.02f, 1.0f, 1.0f);
                case Surface.Stone: return P(1.0f, 0.02f, 1.0f, 1.0f);
                case Surface.Ice: return P(0.3f, 0.01f, 1.0f, 1.0f);
                case Surface.Water: return P(0.5f, 0.25f, 1.0f, 1.0f);
                default: return new SurfaceProfile();
            }
        }

        public static IEnumerable<Surface> All => (Surface[])Enum.GetValues(typeof(Surface));

        private static SurfaceProfile P(float grip, float rolling, float peakSlip, float shape)
        {
            return new SurfaceProfile { Grip = grip, RollingDrag = rolling, PeakSlipScale = peakSlip, ShapeScale = shape };
        }
    }
}
