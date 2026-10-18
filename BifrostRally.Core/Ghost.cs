using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace BifrostRally.Core
{
    /// <summary>A recorded run: car poses at a fixed interval.</summary>
    public class GhostRun
    {
        public float Interval = 0.1f;
        public List<Vector3> Positions = new List<Vector3>();
        public List<Quaternion> Rotations = new List<Quaternion>();

        public float Duration => Positions.Count > 1 ? (Positions.Count - 1) * Interval : 0f;

        /// <summary>Pose at a time in the run, interpolated between samples.</summary>
        public bool Sample(float time, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            if (Positions.Count == 0 || time < 0f)
            {
                return false;
            }
            float f = time / Interval;
            int i = Mathf.FloorToInt(f);
            if (i >= Positions.Count - 1)
            {
                position = Positions[Positions.Count - 1];
                rotation = Rotations[Rotations.Count - 1];
                return time <= Duration + Interval;
            }
            float t = f - i;
            position = Vector3.Lerp(Positions[i], Positions[i + 1], t);
            rotation = Nlerp(Rotations[i], Rotations[i + 1], t);
            return true;
        }

        /// <summary>
        /// Normalised lerp: close enough to slerp for 0.1 s steps, and plain managed maths,
        /// so it also runs outside the game in the offline checks.
        /// </summary>
        public static Quaternion Nlerp(Quaternion a, Quaternion b, float t)
        {
            float dot = a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w;
            float sign = dot < 0f ? -1f : 1f;
            var q = new Quaternion(
                a.x + (b.x * sign - a.x) * t,
                a.y + (b.y * sign - a.y) * t,
                a.z + (b.z * sign - a.z) * t,
                a.w + (b.w * sign - a.w) * t);
            float length = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            return length > 1e-6f ? new Quaternion(q.x / length, q.y / length, q.z / length, q.w / length) : a;
        }

        public string Serialize()
        {
            var sb = new StringBuilder();
            sb.AppendLine(Interval.ToString("R", CultureInfo.InvariantCulture));
            for (int i = 0; i < Positions.Count; i++)
            {
                Vector3 p = Positions[i];
                Quaternion q = Rotations[i];
                sb.AppendLine(string.Join(",", new[] { p.x, p.y, p.z, q.x, q.y, q.z, q.w }.Select(v => v.ToString("R", CultureInfo.InvariantCulture))));
            }
            return sb.ToString();
        }

        public static GhostRun Parse(string text)
        {
            string[] lines = text.Split('\n');
            if (lines.Length < 1 || !float.TryParse(lines[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float interval))
            {
                return null;
            }
            var run = new GhostRun { Interval = interval };
            for (int i = 1; i < lines.Length; i++)
            {
                string[] v = lines[i].Trim().Split(',');
                if (v.Length != 7)
                {
                    continue;
                }
                float[] n = new float[7];
                bool ok = true;
                for (int k = 0; k < 7 && ok; k++)
                {
                    ok = float.TryParse(v[k], NumberStyles.Float, CultureInfo.InvariantCulture, out n[k]);
                }
                if (ok)
                {
                    run.Positions.Add(new Vector3(n[0], n[1], n[2]));
                    run.Rotations.Add(new Quaternion(n[3], n[4], n[5], n[6]));
                }
            }
            return run;
        }

        public static GhostRun Load(string path) => File.Exists(path) ? Parse(File.ReadAllText(path)) : null;

        public void Save(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, Serialize());
        }
    }

    /// <summary>Samples the car at a fixed interval while a stage is running.</summary>
    public class GhostRecorder
    {
        public GhostRun Run { get; private set; }
        private float _next;

        public void Begin(float interval)
        {
            Run = new GhostRun { Interval = interval };
            _next = 0f;
        }

        public void Record(float elapsed, Vector3 position, Quaternion rotation)
        {
            if (Run == null)
            {
                return;
            }
            while (elapsed >= _next)
            {
                Run.Positions.Add(position);
                Run.Rotations.Add(rotation);
                _next += Run.Interval;
            }
        }
    }
}
