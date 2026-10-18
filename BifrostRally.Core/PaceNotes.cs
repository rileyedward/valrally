using System.Collections.Generic;
using UnityEngine;

namespace BifrostRally.Core
{
    public struct PaceNote
    {
        /// <summary>Index into the route's points (start = 0) where this corner is.</summary>
        public int PointIndex;

        /// <summary>-1 left, +1 right, 0 straight on.</summary>
        public int Direction;

        /// <summary>Rally scale: 1 = hairpin (tightest) ... 6 = slight. 0 for straight.</summary>
        public int Severity;

        /// <summary>Metres from this corner to the next point.</summary>
        public float DistanceToNext;

        public string Text;
    }

    /// <summary>
    /// Pace notes from stage geometry. Each checkpoint is treated as a corner: the turn angle
    /// between the leg coming in and the leg going out sets direction and severity, using a
    /// rally-style 1 (hairpin) to 6 (slight) scale. Coarse with few checkpoints, by design.
    /// </summary>
    public static class PaceNotes
    {
        // Turn angles (degrees) at which severity steps from 6 (slight) down to 1 (hairpin).
        private static readonly float[] SeverityLimits = { 15f, 30f, 50f, 75f, 105f, 140f };

        public static List<PaceNote> Generate(StageRoute route)
        {
            var notes = new List<PaceNote>();
            if (route == null)
            {
                return notes;
            }
            List<Vector3> points = route.Points();
            for (int i = 1; i < points.Count - 1; i++)
            {
                Vector3 inLeg = Flat(points[i] - points[i - 1]);
                Vector3 outLeg = Flat(points[i + 1] - points[i]);
                float turn = Vector3.SignedAngle(inLeg, outLeg, Vector3.up);
                float distance = Flat(points[i + 1] - points[i]).magnitude;
                notes.Add(Make(i, turn, distance));
            }
            return notes;
        }

        public static PaceNote Make(int pointIndex, float turnDeg, float distanceToNext)
        {
            float angle = Mathf.Abs(turnDeg);
            int severity = 0;
            if (angle >= SeverityLimits[0])
            {
                severity = 6;
                for (int k = 1; k < SeverityLimits.Length; k++)
                {
                    if (angle >= SeverityLimits[k])
                    {
                        severity = 6 - k;
                    }
                }
            }
            int direction = severity == 0 ? 0 : (turnDeg > 0f ? 1 : -1);
            string distance = RoundDistance(distanceToNext);
            string text;
            if (severity == 0)
            {
                text = "Straight, " + distance;
            }
            else if (severity == 1)
            {
                text = (direction > 0 ? "Hairpin right" : "Hairpin left") + ", " + distance;
            }
            else
            {
                text = (direction > 0 ? "Right " : "Left ") + severity + ", " + distance;
            }
            return new PaceNote { PointIndex = pointIndex, Direction = direction, Severity = severity, DistanceToNext = distanceToNext, Text = text };
        }

        /// <summary>Co-driver style distances: 30, 50, 80, 100, 150, 200, ... then "long".</summary>
        public static string RoundDistance(float metres)
        {
            int[] steps = { 30, 50, 80, 100, 150, 200, 250, 300, 400, 500 };
            foreach (int step in steps)
            {
                if (metres <= step * 1.15f)
                {
                    return step.ToString();
                }
            }
            return "long";
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
