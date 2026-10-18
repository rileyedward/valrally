using System.Linq;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;

namespace BifrostRally.Valheim
{
    /// <summary>
    /// Start gate, checkpoint and finish as hammer pieces. Each is a copy of a vanilla piece
    /// (a banner, falling back to a pole) so it looks at home without new art. What the piece
    /// does comes from its prefab name; the stage runner finds them through the world's saved
    /// objects, so even far-away, unloaded checkpoints count.
    /// </summary>
    internal static class RallyPieces
    {
        public const string Start = "bifrost_rally_start";
        public const string Checkpoint = "bifrost_rally_checkpoint";
        public const string Finish = "bifrost_rally_finish";

        private static readonly string[] StartBases = { "piece_banner01", "wood_pole2", "wood_pole" };
        private static readonly string[] CheckpointBases = { "piece_banner02", "piece_groundtorch_wood", "wood_pole" };
        private static readonly string[] FinishBases = { "piece_banner03", "wood_pole2", "wood_pole" };

        public static void Register()
        {
            PrefabManager.OnVanillaPrefabsAvailable += AddPieces;
        }

        private static void AddPieces()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= AddPieces;
            Add(Start, "Rally start", "Stop next to it to arm the stage timer, then drive off to start.", StartBases);
            Add(Checkpoint, "Rally checkpoint", "Pass within a few metres, in order from the start.", CheckpointBases);
            Add(Finish, "Rally finish", "Ends the stage after the last checkpoint.", FinishBases);
        }

        private static void Add(string name, string label, string description, string[] bases)
        {
            string baseName = bases.FirstOrDefault(b => PrefabManager.Instance.GetPrefab(b) != null);
            if (baseName == null)
            {
                Jotunn.Logger.LogWarning($"No vanilla piece found to copy for {label} (tried {string.Join(", ", bases)})");
                return;
            }

            var config = new PieceConfig
            {
                Name = label,
                Description = description,
                PieceTable = PieceTables.Hammer,
                Category = PieceCategories.Misc,
            };
            config.AddRequirement(new RequirementConfig("Wood", 1, 0, true));
            var piece = new CustomPiece(name, baseName, config);
            if (!piece.IsValid())
            {
                Jotunn.Logger.LogWarning($"Could not create {label} from {baseName}");
                return;
            }
            PieceManager.Instance.AddPiece(piece);
            Jotunn.Logger.LogInfo($"Added piece {label} (copy of {baseName})");
        }
    }
}
