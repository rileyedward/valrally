using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace ValRally.Core
{
    /// <summary>A stage: start, ordered checkpoints, finish.</summary>
    public class StageRoute
    {
        public string Id;
        public Vector3 Start;
        public List<Vector3> Checkpoints = new List<Vector3>();
        public Vector3 Finish;

        /// <summary>Start, checkpoints, finish, in driving order.</summary>
        public List<Vector3> Points()
        {
            var points = new List<Vector3> { Start };
            points.AddRange(Checkpoints);
            points.Add(Finish);
            return points;
        }
    }

    public static class RouteBuilder
    {
        /// <summary>
        /// Chains the route nearest-first from the start: each step goes to the closest unused
        /// checkpoint, unless a finish is closer, which ends the stage. Checkpoints that belong to
        /// other stages further away are simply never reached.
        /// </summary>
        public static StageRoute Build(string id, Vector3 start, IList<Vector3> checkpoints, IList<Vector3> finishes, float maxLeg)
        {
            if (finishes.Count == 0)
            {
                return null;
            }
            var route = new StageRoute { Id = id, Start = start };
            var remaining = new List<Vector3>(checkpoints);
            Vector3 at = start;
            while (true)
            {
                int nearestCp = Nearest(at, remaining, out float cpDistance);
                int nearestFinish = Nearest(at, finishes, out float finishDistance);
                if (nearestFinish >= 0 && (nearestCp < 0 || finishDistance <= cpDistance) && finishDistance <= maxLeg)
                {
                    route.Finish = finishes[nearestFinish];
                    return route;
                }
                if (nearestCp < 0 || cpDistance > maxLeg)
                {
                    return null;
                }
                at = remaining[nearestCp];
                route.Checkpoints.Add(at);
                remaining.RemoveAt(nearestCp);
            }
        }

        private static int Nearest(Vector3 from, IList<Vector3> points, out float distance)
        {
            int best = -1;
            distance = float.MaxValue;
            for (int i = 0; i < points.Count; i++)
            {
                float d = Vector3.Distance(from, points[i]);
                if (d < distance)
                {
                    distance = d;
                    best = i;
                }
            }
            return best;
        }
    }

    public enum StageState { Idle, Armed, Running, Finished }

    public enum StageEvent { None, Armed, Disarmed, Started, Checkpoint, Finished }

    public class StageSettings
    {
        public float GateRadius = 8f;
        public float ArmSpeedKmh = 4f;
        public float StartMoveDistance = 1.5f;
        public float MaxLeg = 3000f;
    }

    /// <summary>
    /// Stage timing. Stop inside the start gate to arm; the clock starts the moment the car
    /// rolls. Checkpoints count only in order; the finish only after the last checkpoint.
    /// </summary>
    public class StageTimer
    {
        public StageSettings Settings = new StageSettings();
        public StageRoute Route { get; private set; }
        public StageState State { get; private set; } = StageState.Idle;
        public float Elapsed { get; private set; }
        public int NextCheckpoint { get; private set; }
        public List<float> Splits { get; } = new List<float>();

        private float _startTime;
        private Vector3 _armPosition;

        public void SetRoute(StageRoute route)
        {
            Route = route;
            Reset();
        }

        public void Reset()
        {
            State = StageState.Idle;
            Elapsed = 0f;
            NextCheckpoint = 0;
            Splits.Clear();
        }

        public StageEvent Update(Vector3 car, float speedKmh, float time)
        {
            if (Route == null)
            {
                return StageEvent.None;
            }
            float r = Settings.GateRadius;
            bool atStart = Flat(car, Route.Start) <= r;

            switch (State)
            {
                case StageState.Idle:
                case StageState.Finished:
                    if (atStart && Mathf.Abs(speedKmh) <= Settings.ArmSpeedKmh)
                    {
                        Reset();
                        State = StageState.Armed;
                        _armPosition = car;
                        return StageEvent.Armed;
                    }
                    return StageEvent.None;

                case StageState.Armed:
                    if (Flat(car, _armPosition) > Settings.StartMoveDistance)
                    {
                        State = StageState.Running;
                        _startTime = time;
                        return StageEvent.Started;
                    }
                    return StageEvent.None;

                case StageState.Running:
                    Elapsed = time - _startTime;
                    if (NextCheckpoint < Route.Checkpoints.Count)
                    {
                        if (Flat(car, Route.Checkpoints[NextCheckpoint]) <= r)
                        {
                            Splits.Add(Elapsed);
                            NextCheckpoint++;
                            return StageEvent.Checkpoint;
                        }
                    }
                    else if (Flat(car, Route.Finish) <= r)
                    {
                        Splits.Add(Elapsed);
                        State = StageState.Finished;
                        return StageEvent.Finished;
                    }
                    return StageEvent.None;
            }
            return StageEvent.None;
        }

        /// <summary>Horizontal distance, so gates on slopes still trigger.</summary>
        private static float Flat(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        public static string Format(float seconds)
        {
            int minutes = Mathf.FloorToInt(seconds / 60f);
            float rest = seconds - minutes * 60f;
            return minutes.ToString(CultureInfo.InvariantCulture) + ":" + rest.ToString("00.00", CultureInfo.InvariantCulture);
        }

        public static string FormatDelta(float delta)
        {
            return (delta >= 0f ? "+" : "-") + Mathf.Abs(delta).ToString("0.00", CultureInfo.InvariantCulture);
        }
    }

    /// <summary>Best time and its splits for one stage, stored as plain text.</summary>
    public class StageRecord
    {
        public float Time;
        public List<float> Splits = new List<float>();

        public string Serialize()
        {
            var sb = new StringBuilder();
            sb.Append(Time.ToString("R", CultureInfo.InvariantCulture));
            foreach (float split in Splits)
            {
                sb.Append(',').Append(split.ToString("R", CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        public static StageRecord Parse(string line)
        {
            string[] parts = line.Trim().Split(',');
            if (parts.Length == 0 || !float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float time))
            {
                return null;
            }
            var record = new StageRecord { Time = time };
            for (int i = 1; i < parts.Length; i++)
            {
                if (float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out float split))
                {
                    record.Splits.Add(split);
                }
            }
            return record;
        }

        public static StageRecord Load(string path)
        {
            return File.Exists(path) ? Parse(File.ReadAllText(path)) : null;
        }

        public void Save(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, Serialize());
        }
    }
}
