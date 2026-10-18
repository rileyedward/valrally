using System.Collections.Generic;
using System.IO;
using System.Linq;
using BifrostRally.Core;
using UnityEngine;

namespace BifrostRally.Valheim
{
    internal class StageOptions
    {
        public bool Enabled = true;
        public bool Ghost = true;
        public bool PaceNotes = true;

        // Call the next corner this many seconds before reaching it.
        public float PaceCallSeconds = 3f;
        public float PaceCallMinDistance = 40f;
        public float PaceShowSeconds = 2.5f;
        public float GhostInterval = 0.1f;
    }

    /// <summary>
    /// Runs stages in Valheim: finds the start gate you're at, chains its route from the placed
    /// pieces, times the run, shows splits against your best, records and replays a ghost, and
    /// calls pace notes. Best times and ghosts are saved per world and start position.
    /// </summary>
    internal static class StageRunner
    {
        public static StageOptions Options = new StageOptions();
        public static readonly StageTimer Timer = new StageTimer();

        private static List<PaceNote> _notes = new List<PaceNote>();
        private static StageRecord _best;
        private static GhostRun _bestGhost;
        private static readonly GhostRecorder Recorder = new GhostRecorder();
        private static GameObject _ghost;
        private static int _calledNote = -1;
        private static float _callUntil;
        private static float _nextScan;
        private static List<Vector3> _starts = new List<Vector3>();

        public static StageRoute Route => Timer.Route;
        public static StageRecord Best => _best;

        public static void Update()
        {
            RaycastCar car = RallyDriver.Car;
            if (!Options.Enabled || car == null || !RallyDriver.IsDriving)
            {
                if (Timer.State == StageState.Running || Timer.State == StageState.Armed)
                {
                    // Getting out abandons the run.
                    Timer.Reset();
                }
                ClearHud();
                HideGhost();
                return;
            }

            Vector3 position = car.transform.position;
            if (Timer.State == StageState.Idle || Timer.State == StageState.Finished)
            {
                LookForStart(position);
                if (Timer.State == StageState.Idle && Route != null && FlatDistance(position, Route.Start) > Timer.Settings.GateRadius * 3f)
                {
                    ClearHud();
                }
            }

            StageEvent e = Timer.Update(position, car.SpeedKmh, Time.time);
            switch (e)
            {
                case StageEvent.Armed:
                    RallyHud.TimerText = "READY";
                    RallyHud.SplitText = _best != null ? "Best " + StageTimer.Format(_best.Time) + "  -  go when ready" : "Go when ready";
                    _calledNote = -1;
                    break;
                case StageEvent.Started:
                    Recorder.Begin(Options.GhostInterval);
                    RallyHud.SplitText = $"{Route.Checkpoints.Count} checkpoints";
                    break;
                case StageEvent.Checkpoint:
                    ShowSplit(Timer.Splits.Count - 1, $"CP {Timer.Splits.Count}");
                    break;
                case StageEvent.Finished:
                    Finish();
                    break;
            }

            if (Timer.State == StageState.Running)
            {
                RallyHud.TimerText = StageTimer.Format(Timer.Elapsed);
                Recorder.Record(Timer.Elapsed, position, car.transform.rotation);
                UpdateGhost(Timer.Elapsed);
                UpdatePaceNotes(car, position);
            }
            else
            {
                HideGhost();
            }

            if (Time.time > _callUntil)
            {
                RallyHud.CallText = null;
            }
        }

        private static void LookForStart(Vector3 position)
        {
            if (Time.time >= _nextScan)
            {
                // Scanning the world's objects is not free, so only every few seconds while idle.
                _nextScan = Time.time + 3f;
                _starts = PiecePositions(RallyPieces.Start);
            }
            float radius = Timer.Settings.GateRadius;
            foreach (Vector3 start in _starts)
            {
                if (FlatDistance(start, position) <= radius * 1.5f)
                {
                    string id = StageId(start);
                    if (Route == null || Route.Id != id)
                    {
                        LoadStage(id, start);
                    }
                    return;
                }
            }
        }

        private static void LoadStage(string id, Vector3 start)
        {
            StageRoute route = RouteBuilder.Build(id, start, PiecePositions(RallyPieces.Checkpoint), PiecePositions(RallyPieces.Finish), Timer.Settings.MaxLeg);
            if (route == null)
            {
                RallyHud.SplitText = "No finish within reach of this start";
                return;
            }
            Timer.SetRoute(route);
            _notes = PaceNotes.Generate(route);
            _best = StageRecord.Load(PathFor(id, "best"));
            _bestGhost = _best != null ? GhostRun.Load(PathFor(id, "ghost")) : null;
            float length = 0f;
            List<Vector3> points = route.Points();
            for (int i = 1; i < points.Count; i++)
            {
                length += FlatDistance(points[i - 1], points[i]);
            }
            RallyHud.TimerText = "STAGE";
            RallyHud.SplitText = $"{route.Checkpoints.Count} checkpoints, {length / 1000f:F1} km. Stop at the start to arm" +
                (_best != null ? $". Best {StageTimer.Format(_best.Time)}" : "");
            Jotunn.Logger.LogInfo($"Stage {id}: {route.Checkpoints.Count} checkpoints, {length:F0} m");
        }

        private static void ShowSplit(int index, string label)
        {
            float split = Timer.Splits[index];
            string text = $"{label}  {StageTimer.Format(split)}";
            if (_best != null && index < _best.Splits.Count)
            {
                text += "  " + StageTimer.FormatDelta(split - _best.Splits[index]);
            }
            RallyHud.SplitText = text;
        }

        private static void Finish()
        {
            float time = Timer.Elapsed;
            bool record = _best == null || time < _best.Time;
            ShowSplit(Timer.Splits.Count - 1, "FINISH");
            RallyHud.TimerText = StageTimer.Format(time) + (record ? "  NEW BEST" : "");
            if (record)
            {
                _best = new StageRecord { Time = time, Splits = new List<float>(Timer.Splits) };
                _best.Save(PathFor(Route.Id, "best"));
                if (Recorder.Run != null)
                {
                    Recorder.Run.Save(PathFor(Route.Id, "ghost"));
                    _bestGhost = Recorder.Run;
                }
            }
            Jotunn.Logger.LogInfo($"Stage {Route.Id} finished in {StageTimer.Format(time)}{(record ? " (new best)" : "")}");
        }

        private static void UpdatePaceNotes(RaycastCar car, Vector3 position)
        {
            if (!Options.PaceNotes || _notes.Count == 0)
            {
                return;
            }
            int noteIndex = Timer.NextCheckpoint;
            if (noteIndex >= _notes.Count || noteIndex == _calledNote)
            {
                return;
            }
            Vector3 corner = Route.Checkpoints[noteIndex];
            float distance = FlatDistance(position, corner);
            float speed = Mathf.Max(5f, Mathf.Abs(car.SpeedKmh) / 3.6f);
            if (distance <= speed * Options.PaceCallSeconds || distance <= Options.PaceCallMinDistance)
            {
                _calledNote = noteIndex;
                RallyHud.CallText = _notes[noteIndex].Text;
                _callUntil = Time.time + Options.PaceShowSeconds;
            }
        }

        private static void UpdateGhost(float elapsed)
        {
            if (!Options.Ghost || _bestGhost == null || !_bestGhost.Sample(elapsed, out Vector3 p, out Quaternion q))
            {
                HideGhost();
                return;
            }
            if (_ghost == null)
            {
                _ghost = CreateGhost();
            }
            _ghost.SetActive(true);
            _ghost.transform.SetPositionAndRotation(p, q);
        }

        private static void HideGhost()
        {
            if (_ghost != null)
            {
                _ghost.SetActive(false);
            }
        }

        /// <summary>A see-through box the size of the car, with no collider.</summary>
        private static GameObject CreateGhost()
        {
            GameObject ghost = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ghost.name = "BifrostRallyGhost";
            Object.Destroy(ghost.GetComponent<Collider>());
            ghost.transform.localScale = new Vector3(1.7f, 0.9f, 3.9f);
            Shader shader = Shader.Find("Sprites/Default");
            Material material = shader != null ? new Material(shader) : RallyDriver.FindCarMaterial();
            if (material != null)
            {
                if (shader != null)
                {
                    material.color = new Color(0.55f, 0.85f, 1f, 0.35f);
                }
                ghost.GetComponent<Renderer>().sharedMaterial = material;
            }
            return ghost;
        }

        private static void ClearHud()
        {
            RallyHud.TimerText = null;
            RallyHud.SplitText = null;
            RallyHud.CallText = null;
        }

        /// <summary>World positions of every placed piece of a kind, loaded or not.</summary>
        public static List<Vector3> PiecePositions(string prefab)
        {
            var result = new List<Vector3>();
            if (ZDOMan.instance == null)
            {
                return result;
            }
            var zdos = new List<ZDO>();
            int index = 0;
            while (!ZDOMan.instance.GetAllZDOsWithPrefabIterative(prefab, zdos, ref index))
            {
            }
            foreach (ZDO zdo in zdos.Distinct())
            {
                result.Add(zdo.GetPosition());
            }
            return result;
        }

        private static string StageId(Vector3 start)
        {
            string world = ZNet.World != null ? ZNet.World.m_name : "world";
            return $"{world}_{Mathf.RoundToInt(start.x)}_{Mathf.RoundToInt(start.z)}";
        }

        private static string PathFor(string id, string kind)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                id = id.Replace(c, '_');
            }
            return Path.Combine(BepInEx.Paths.ConfigPath, "bifrostrally-stages", id + "." + kind + ".txt");
        }

        public static void ForgetBest()
        {
            if (Route == null)
            {
                return;
            }
            File.Delete(PathFor(Route.Id, "best"));
            File.Delete(PathFor(Route.Id, "ghost"));
            _best = null;
            _bestGhost = null;
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
