using System;
using System.Collections.Generic;
using BifrostRally.Core;
using UnityEngine;

namespace BifrostRally.Sim
{
    /// <summary>Checks for stage timing, routes, pace notes and saved runs.</summary>
    internal static class StageTests
    {
        private static bool _ok;

        public static bool Run()
        {
            _ok = true;
            Console.WriteLine("--- stage logic ---");
            RouteOrder();
            TimerFlow();
            Notes();
            SaveLoad();
            return _ok;
        }

        private static void Expect(bool pass, string what)
        {
            Console.WriteLine((pass ? "  ok   " : "  FAIL ") + what);
            _ok &= pass;
        }

        private static void RouteOrder()
        {
            var start = new Vector3(0, 0, 0);
            var cps = new List<Vector3> { new Vector3(0, 0, 200), new Vector3(100, 0, 100), new Vector3(5000, 0, 5000) };
            var finishes = new List<Vector3> { new Vector3(200, 0, 300) };
            StageRoute route = RouteBuilder.Build("t", start, cps, finishes, 3000f);
            Expect(route != null, "route builds");
            Expect(route != null && route.Checkpoints.Count == 2 && route.Checkpoints[0] == cps[1] && route.Checkpoints[1] == cps[0],
                "checkpoints chained nearest-first, far checkpoint of another stage left out");
            Expect(RouteBuilder.Build("t", start, cps, new List<Vector3>(), 3000f) == null, "no finish means no route");
        }

        private static void TimerFlow()
        {
            var route = new StageRoute { Id = "t", Start = Vector3.zero, Finish = new Vector3(0, 0, 300) };
            route.Checkpoints.Add(new Vector3(0, 0, 100));
            route.Checkpoints.Add(new Vector3(0, 0, 200));
            var timer = new StageTimer();
            timer.SetRoute(route);

            Expect(timer.Update(new Vector3(0, 0, 2), 30f, 0f) == StageEvent.None, "fast through the start does not arm");
            Expect(timer.Update(new Vector3(0, 0, 2), 0f, 1f) == StageEvent.Armed, "stopping in the start gate arms");
            Expect(timer.Update(new Vector3(0, 0, 2.5f), 3f, 1.5f) == StageEvent.None, "creeping a little keeps it armed");
            Expect(timer.Update(new Vector3(0, 0, 4f), 10f, 2f) == StageEvent.Started, "rolling off starts the clock");
            Expect(timer.Update(new Vector3(0, 0, 200), 80f, 10f) == StageEvent.None, "skipping checkpoint 1 does not count checkpoint 2");
            Expect(timer.Update(new Vector3(0, 0, 300), 80f, 11f) == StageEvent.None, "finish ignored before all checkpoints");
            Expect(timer.Update(new Vector3(3, 0, 100), 80f, 12f) == StageEvent.Checkpoint, "checkpoint 1 within radius");
            Expect(timer.Update(new Vector3(0, 5, 200), 80f, 15f) == StageEvent.Checkpoint, "checkpoint 2 counts with height difference");
            Expect(timer.Update(new Vector3(0, 0, 300), 80f, 20f) == StageEvent.Finished, "finish after all checkpoints");
            Expect(Mathf.Abs(timer.Elapsed - 18f) < 0.001f && timer.Splits.Count == 3, $"time 18.00 with 3 splits (got {timer.Elapsed:F2}, {timer.Splits.Count})");
            Expect(StageTimer.Format(83.456f) == "1:23.46" && StageTimer.FormatDelta(-0.5f) == "-0.50", "time formatting");
        }

        private static void Notes()
        {
            // North 100 m, right 90 degrees, east 100 m, right 90 degrees, south to the finish.
            var route = new StageRoute { Start = Vector3.zero, Finish = new Vector3(100, 0, 0) };
            route.Checkpoints.Add(new Vector3(0, 0, 100));
            route.Checkpoints.Add(new Vector3(100, 0, 100));
            List<PaceNote> notes = PaceNotes.Generate(route);
            Expect(notes.Count == 2, "one note per checkpoint");
            Expect(notes.Count == 2 && notes[0].Text == "Right 3, 100", $"90 deg right reads 'Right 3, 100' (got '{(notes.Count > 0 ? notes[0].Text : "")}')");
            Expect(notes.Count == 2 && notes[1].Direction > 0 && notes[1].Severity == 3, $"second 90 deg right (got '{(notes.Count > 1 ? notes[1].Text : "")}')");
            Expect(PaceNotes.Make(1, -60f, 200f).Text == "Left 4, 200", "60 deg left reads Left 4");
            Expect(PaceNotes.Make(1, -170f, 55f).Text == "Hairpin left, 50", $"hairpin and distance rounding (got '{PaceNotes.Make(1, -170f, 55f).Text}')");
            Expect(PaceNotes.Make(1, 5f, 420f).Text == "Straight, 400", "straight");
        }

        private static void SaveLoad()
        {
            string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "bifrost-sim-test");
            var record = new StageRecord { Time = 61.25f };
            record.Splits.AddRange(new[] { 20.5f, 40.25f, 61.25f });
            record.Save(System.IO.Path.Combine(dir, "best.txt"));
            StageRecord back = StageRecord.Load(System.IO.Path.Combine(dir, "best.txt"));
            Expect(back != null && back.Time == 61.25f && back.Splits.Count == 3 && back.Splits[1] == 40.25f, "best time round-trips");

            var recorder = new GhostRecorder();
            recorder.Begin(0.1f);
            for (int i = 0; i <= 20; i++)
            {
                // Yaw of i degrees, built by hand: Unity's Quaternion.Euler is native and can't run here.
                float half = i * 0.5f * Mathf.Deg2Rad;
                recorder.Record(i * 0.05f, new Vector3(i, 0, 0), new Quaternion(0f, Mathf.Sin(half), 0f, Mathf.Cos(half)));
            }
            recorder.Run.Save(System.IO.Path.Combine(dir, "ghost.txt"));
            GhostRun ghost = GhostRun.Load(System.IO.Path.Combine(dir, "ghost.txt"));
            Expect(ghost != null && ghost.Positions.Count == recorder.Run.Positions.Count, $"ghost round-trips ({ghost?.Positions.Count} samples)");
            Expect(ghost != null && ghost.Sample(0.55f, out Vector3 p, out _) && Mathf.Abs(p.x - 11f) < 0.6f, "ghost interpolates between samples");
        }
    }
}
