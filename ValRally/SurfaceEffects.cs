using System.Collections.Generic;
using ValRally.Core;
using UnityEngine;

namespace ValRally.Valheim
{
    internal class EffectSettings
    {
        public bool Particles = true;
        public bool Sounds = true;

        // Seconds between puffs per wheel: slow when rolling gently, fast when sliding hard.
        public float SlowInterval = 0.35f;
        public float FastInterval = 0.06f;
        public float SoundInterval = 0.22f;
        public float MinSpeed = 3f;
    }

    /// <summary>
    /// Dust, mud, snow and crunch at the wheels, borrowed from the Viking's own footstep
    /// effects for each ground material, so it matches the game with no new assets.
    /// </summary>
    internal static class SurfaceEffects
    {
        public static EffectSettings Settings = new EffectSettings();

        private class Pair
        {
            public readonly List<GameObject> Visuals = new List<GameObject>();
            public readonly List<GameObject> Sounds = new List<GameObject>();
        }

        private static Dictionary<FootStep.GroundMaterial, Pair> _effects;
        private static float[] _visualTimers = new float[4];
        private static float[] _soundTimers = new float[4];

        public static void Update(RaycastCar car, float dt)
        {
            if (car == null || (!Settings.Particles && !Settings.Sounds) || !LoadEffects())
            {
                return;
            }
            if (_visualTimers.Length != car.WheelCount)
            {
                _visualTimers = new float[car.WheelCount];
                _soundTimers = new float[car.WheelCount];
            }

            float speed = car.Velocity.magnitude;
            for (int i = 0; i < car.WheelCount; i++)
            {
                RaycastCar.WheelInfo wheel = car.GetWheel(i);
                float slide = Mathf.Clamp01(Mathf.Abs(wheel.SlipAngleDeg) / 15f + Mathf.Abs(wheel.SlipRatio) / 0.3f);
                if (!wheel.Grounded || (speed < Settings.MinSpeed && slide < 0.3f))
                {
                    continue;
                }

                // Rear wheels throw most; fronts only when sliding.
                if (wheel.Front && slide < 0.5f)
                {
                    continue;
                }

                float intensity = Mathf.Clamp01(slide + speed / 40f);
                if (!_effects.TryGetValue(ValheimSurfaceProvider.ToGroundMaterial(wheel.Surface), out Pair pair))
                {
                    continue;
                }

                _visualTimers[i] -= dt;
                if (Settings.Particles && _visualTimers[i] <= 0f && pair.Visuals.Count > 0)
                {
                    _visualTimers[i] = Mathf.Lerp(Settings.SlowInterval, Settings.FastInterval, intensity);
                    Spawn(pair.Visuals, wheel.Contact, car.transform.rotation);
                }

                _soundTimers[i] -= dt;
                if (Settings.Sounds && _soundTimers[i] <= 0f && pair.Sounds.Count > 0)
                {
                    _soundTimers[i] = Settings.SoundInterval * Mathf.Lerp(1.5f, 0.6f, intensity);
                    Spawn(pair.Sounds, wheel.Contact, Quaternion.identity);
                }
            }
        }

        private static void Spawn(List<GameObject> prefabs, Vector3 at, Quaternion rotation)
        {
            GameObject prefab = prefabs[Random.Range(0, prefabs.Count)];
            if (prefab == null)
            {
                return;
            }
            GameObject instance = Object.Instantiate(prefab, at, rotation);
            // Footstep effects normally clean themselves up; this is a safety net.
            Object.Destroy(instance, 5f);
        }

        /// <summary>Reads the Player prefab's footstep effect table once.</summary>
        private static bool LoadEffects()
        {
            if (_effects != null)
            {
                return _effects.Count > 0;
            }
            GameObject player = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab("Player") : null;
            FootStep footstep = player != null ? player.GetComponent<FootStep>() : null;
            if (footstep == null)
            {
                return false;
            }

            _effects = new Dictionary<FootStep.GroundMaterial, Pair>();
            foreach (FootStep.StepEffect step in footstep.m_effects)
            {
                // Running steps are the closest match to wheels at speed.
                if ((step.m_motionType & (FootStep.MotionType.Run | FootStep.MotionType.Jog)) == 0)
                {
                    continue;
                }
                foreach (FootStep.GroundMaterial material in System.Enum.GetValues(typeof(FootStep.GroundMaterial)))
                {
                    if (material == FootStep.GroundMaterial.None || material == FootStep.GroundMaterial.Everything ||
                        (step.m_material & material) == 0)
                    {
                        continue;
                    }
                    if (!_effects.TryGetValue(material, out Pair pair))
                    {
                        pair = new Pair();
                        _effects[material] = pair;
                    }
                    foreach (GameObject prefab in step.m_effectPrefabs)
                    {
                        if (prefab == null)
                        {
                            continue;
                        }
                        (prefab.GetComponentInChildren<ZSFX>(true) != null ? pair.Sounds : pair.Visuals).Add(prefab);
                    }
                }
            }
            Jotunn.Logger.LogInfo($"Surface effects: borrowed footstep effects for {_effects.Count} ground materials");
            return _effects.Count > 0;
        }
    }
}
