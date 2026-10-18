using BifrostRally.Core;
using Jotunn.Entities;
using UnityEngine;

namespace BifrostRally.Valheim
{
    /// <summary>rally_stage: what stage you're on, its notes and best; "forget" clears the best and ghost.</summary>
    internal class StageCommand : ConsoleCommand
    {
        public override string Name => "rally_stage";

        public override string Help => "rally_stage [forget] - show the current stage, its pace notes and best time; 'forget' deletes the best time and ghost";

        public override void Run(string[] args)
        {
            StageRoute route = StageRunner.Route;
            if (args.Length > 0 && args[0] == "forget")
            {
                StageRunner.ForgetBest();
                LiveTuning.Print(route != null ? "Best time and ghost forgotten for " + route.Id : "No stage loaded");
                return;
            }

            LiveTuning.Print($"Pieces placed: {StageRunner.PiecePositions(RallyPieces.Start).Count} starts, " +
                $"{StageRunner.PiecePositions(RallyPieces.Checkpoint).Count} checkpoints, {StageRunner.PiecePositions(RallyPieces.Finish).Count} finishes");
            if (route == null)
            {
                LiveTuning.Print("No stage loaded. Drive up to a rally start in the car.");
                return;
            }
            LiveTuning.Print($"Stage {route.Id}: {route.Checkpoints.Count} checkpoints, state {StageRunner.Timer.State}");
            foreach (PaceNote note in PaceNotes.Generate(route))
            {
                LiveTuning.Print($"  CP {note.PointIndex}: {note.Text}");
            }
            StageRecord best = StageRunner.Best;
            LiveTuning.Print(best != null ? "Best " + StageTimer.Format(best.Time) : "No best time yet");
        }
    }
}
