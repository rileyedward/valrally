using System.Collections.Generic;
using BifrostRally.Core;
using UnityEngine;

namespace BifrostRally.Valheim
{
    /// <summary>Thresholds for reading Valheim ground. Bound to [Surfaces] in config.</summary>
    internal class SurfaceRules
    {
        public bool Enabled = true;

        // A paint channel above this counts (same as the game's own "cleared" check).
        public float PaintThreshold = 0.5f;

        // Sand guess: terrain this far below or above sea level counts as beach.
        public float SandBelowSea = 4f;
        public float SandAboveSea = 1.5f;

        // Deep North stores snow depth in the paint mask's green channel.
        public float DeepSnowThreshold = 0.12f;

        // Steeper mountain ground is bare rock, as in the game's footsteps.
        public float RockSlopeDeg = 40f;

        // Wheel contact this far under the water surface counts as driving through water.
        public float WaterDepth = 0.3f;
    }

    /// <summary>
    /// Answers "what is this wheel on" from Valheim's ground data: hoe paint first (paved,
    /// dirt, cultivated), then biome and slope, then a sea-level guess for sand. Buildings use
    /// their piece material.
    /// </summary>
    internal class ValheimSurfaceProvider : ISurfaceProvider
    {
        public SurfaceRules Rules = new SurfaceRules();

        private readonly Dictionary<Collider, Heightmap> _heightmaps = new Dictionary<Collider, Heightmap>();

        public Surface Query(RaycastHit hit) => Describe(hit, out _);

        public Surface Describe(RaycastHit hit, out string why)
        {
            Vector3 p = hit.point;
            float sea = ZoneSystem.instance != null ? ZoneSystem.instance.m_waterLevel : 30f;

            // Water volumes are checked only near sea level; the lookup is a physics overlap.
            if (p.y < sea + 2f && Floating.GetLiquidLevel(p) > p.y + Rules.WaterDepth)
            {
                why = "under water";
                return Surface.Water;
            }

            Collider c = hit.collider;
            Heightmap hm = TerrainOf(c);
            if (hm != null)
            {
                return Terrain(hm, hit, sea, out why);
            }

            FootStepCollider footstep = c != null ? c.GetComponentInParent<FootStepCollider>() : null;
            if (footstep != null)
            {
                why = "footstep collider " + footstep.m_material;
                return FromGroundMaterial(footstep.m_material);
            }

            WearNTear piece = c != null ? c.GetComponentInParent<WearNTear>() : null;
            if (piece != null)
            {
                why = "piece " + piece.m_materialType;
                switch (piece.m_materialType)
                {
                    case WearNTear.MaterialType.Wood:
                    case WearNTear.MaterialType.HardWood:
                    case WearNTear.MaterialType.Timberwood:
                        return Surface.Wood;
                    case WearNTear.MaterialType.Ice:
                        return Surface.Ice;
                    default:
                        return Surface.Stone;
                }
            }

            why = "static collider " + (c != null ? c.name : "none");
            return Surface.Stone;
        }

        private Surface Terrain(Heightmap hm, RaycastHit hit, float sea, out string why)
        {
            Vector3 p = hit.point;
            Color paint = hm.GetPaintMask(p);
            Heightmap.Biome biome = hm.GetBiome(p);
            bool deepNorth = biome == Heightmap.Biome.DeepNorth;

            // Hoe and cultivator paint wins; the strongest channel decides.
            float green = deepNorth ? 0f : paint.g;
            float strongest = Mathf.Max(paint.r, Mathf.Max(green, paint.b));
            if (strongest > Rules.PaintThreshold)
            {
                why = $"paint r{paint.r:F2} g{paint.g:F2} b{paint.b:F2}";
                if (paint.b >= strongest) return Surface.Tarmac;
                if (paint.r >= strongest) return Surface.Gravel;
                return Surface.SoftDirt;
            }

            float slope = Vector3.Angle(hit.normal, Vector3.up);
            why = $"{biome}, slope {slope:F0}, h {p.y - sea:F1} above sea";

            // No sand flag exists: beaches are guessed from height near sea level.
            bool sandyBiome = biome == Heightmap.Biome.Meadows || biome == Heightmap.Biome.BlackForest ||
                biome == Heightmap.Biome.Plains || biome == Heightmap.Biome.Ocean || biome == Heightmap.Biome.Mistlands;
            if (biome == Heightmap.Biome.Ocean || (sandyBiome && p.y <= sea + Rules.SandAboveSea && p.y >= sea - Rules.SandBelowSea))
            {
                return Surface.LooseSand;
            }

            switch (biome)
            {
                case Heightmap.Biome.Swamp:
                    return Surface.Mud;
                case Heightmap.Biome.Mountain:
                    return slope > Rules.RockSlopeDeg ? Surface.Stone : Surface.Snow;
                case Heightmap.Biome.DeepNorth:
                    return paint.g > Rules.DeepSnowThreshold ? Surface.DeepSnow : Surface.Snow;
                case Heightmap.Biome.AshLands:
                    return Surface.Ash;
                default:
                    return Surface.OffRoad;
            }
        }

        private Heightmap TerrainOf(Collider c)
        {
            if (c == null)
            {
                return null;
            }
            if (!_heightmaps.TryGetValue(c, out Heightmap hm))
            {
                // Zones load and unload as you drive; a periodic clear drops stale entries.
                if (_heightmaps.Count > 256)
                {
                    _heightmaps.Clear();
                }
                hm = c.GetComponentInParent<Heightmap>();
                _heightmaps[c] = hm;
            }
            return hm;
        }

        public static Surface FromGroundMaterial(FootStep.GroundMaterial material)
        {
            switch (material)
            {
                case FootStep.GroundMaterial.Water: return Surface.Water;
                case FootStep.GroundMaterial.Wood: return Surface.Wood;
                case FootStep.GroundMaterial.Snow: return Surface.Snow;
                case FootStep.GroundMaterial.SnowDeep:
                case FootStep.GroundMaterial.SnowVeryDeep: return Surface.DeepSnow;
                case FootStep.GroundMaterial.Mud:
                case FootStep.GroundMaterial.Tar: return Surface.Mud;
                case FootStep.GroundMaterial.Grass: return Surface.OffRoad;
                case FootStep.GroundMaterial.GenericGround: return Surface.Gravel;
                case FootStep.GroundMaterial.Ashlands:
                case FootStep.GroundMaterial.Lava: return Surface.Ash;
                case FootStep.GroundMaterial.Ice: return Surface.Ice;
                default: return Surface.Stone;
            }
        }

        /// <summary>The footstep material whose dust and sound fit a surface.</summary>
        public static FootStep.GroundMaterial ToGroundMaterial(Surface surface)
        {
            switch (surface)
            {
                case Surface.Tarmac:
                case Surface.Stone: return FootStep.GroundMaterial.Stone;
                case Surface.OffRoad: return FootStep.GroundMaterial.Grass;
                case Surface.Mud: return FootStep.GroundMaterial.Mud;
                case Surface.Snow: return FootStep.GroundMaterial.Snow;
                case Surface.DeepSnow: return FootStep.GroundMaterial.SnowDeep;
                case Surface.Ash: return FootStep.GroundMaterial.Ashlands;
                case Surface.Wood: return FootStep.GroundMaterial.Wood;
                case Surface.Ice: return FootStep.GroundMaterial.Ice;
                case Surface.Water: return FootStep.GroundMaterial.Water;
                default: return FootStep.GroundMaterial.GenericGround;
            }
        }
    }
}
