using BifrostRally.Core;
using HarmonyLib;
using UnityEngine;
using UnityEngine.PostProcessing;

namespace BifrostRally.Valheim
{
    /// <summary>
    /// Blends Valheim's camera into the rally chase view while driving, and fades the
    /// rally colour filter in with it. Valheim keeps computing its own pose underneath,
    /// so blending back out on exit is just the same blend in reverse.
    /// </summary>
    internal static class RallyView
    {
        private const float CameraRadius = 0.3f;
        private const float WaterClearance = 0.3f;

        public static float BlendTime = 1.2f;

        private static readonly ChaseCamera Chase = new ChaseCamera();
        private static ColorFilterSettings _filterSettings = new ColorFilterSettings();
        private static Texture2D _lut;
        private static PostProcessingBehaviour _post;
        private static bool _warnedNoProfile;
        private static float _weight;

        private const float PresetSwitchTime = 0.6f;

        private static ChaseCameraSettings _switchFrom;
        private static ChaseCameraSettings _switchTo;
        private static float _switchT = 1f;

        // Terrain and buildings only: trees and bushes would make a close camera jump around.
        private static int BlockMask => LayerMask.GetMask("terrain", "piece");

        /// <summary>Change the camera preset; blends over a moment when already driving.</summary>
        public static void SetCamera(ChaseCameraSettings camera, bool smooth)
        {
            if (smooth && _weight > 0f)
            {
                _switchFrom = Chase.Settings;
                _switchTo = camera;
                _switchT = 0f;
            }
            else
            {
                Chase.Settings = camera;
                _switchT = 1f;
            }
        }

        public static void Configure(ChaseCameraSettings camera, ColorFilterSettings filter)
        {
            SetCamera(camera, smooth: false);
            _filterSettings = filter;
            if (_lut != null)
            {
                Object.Destroy(_lut);
            }
            _lut = ColorFilter.BuildLut(_filterSettings);
        }

        public static void Apply(GameCamera gameCamera, float dt)
        {
            Player player = Player.m_localPlayer;
            RaycastCar car = RallyDriver.Car;
            bool freeFly = gameCamera.m_freeFly;
            bool wanted = RallyDriver.IsDriving && car != null && player != null && !player.IsDead() && !freeFly;

            if (wanted && _weight <= 0f)
            {
                Chase.Reset(car.transform.position, car.transform.forward);
            }
            _weight = Mathf.MoveTowards(_weight, wanted ? 1f : 0f, dt / Mathf.Max(0.01f, BlendTime));
            float blend = Mathf.SmoothStep(0f, 1f, _weight);

            SetFilter(gameCamera, blend);

            if (_weight <= 0f || car == null || freeFly)
            {
                return;
            }

            if (_switchT < 1f)
            {
                _switchT = Mathf.Min(1f, _switchT + dt / PresetSwitchTime);
                Chase.Settings = ChaseCameraSettings.Lerp(_switchFrom, _switchTo, Mathf.SmoothStep(0f, 1f, _switchT));
            }

            int blockMask = BlockMask;
            CameraPose chase = Chase.Update(car.transform.position, car.transform.forward, car.Velocity, car.YawRate, dt,
                (origin, dir, length) => Physics.SphereCast(origin, CameraRadius, dir, out RaycastHit hit, length, blockMask)
                    ? hit.distance
                    : -1f);

            float water = Floating.GetLiquidLevel(chase.Position);
            if (chase.Position.y < water + WaterClearance)
            {
                chase.Position.y = water + WaterClearance;
            }

            Transform t = gameCamera.transform;
            var vanilla = new CameraPose { Position = t.position, Rotation = t.rotation, Fov = gameCamera.m_camera.fieldOfView };
            CameraPose pose = CameraPose.Lerp(vanilla, chase, blend);
            t.SetPositionAndRotation(pose.Position, pose.Rotation);
            gameCamera.m_camera.fieldOfView = pose.Fov;
            gameCamera.m_skyCamera.fieldOfView = pose.Fov;
        }

        /// <summary>
        /// Valheim runs Post Processing Stack v1. Its user LUT is applied on top of the
        /// game's own grading, and its contribution does the fade in the shader.
        /// </summary>
        private static void SetFilter(GameCamera gameCamera, float amount)
        {
            if (_lut == null)
            {
                return;
            }

            if (_post == null || _post.gameObject != gameCamera.gameObject)
            {
                _post = gameCamera.GetComponent<PostProcessingBehaviour>();
                if (_post == null || _post.profile == null)
                {
                    _post = null;
                    if (!_warnedNoProfile)
                    {
                        Jotunn.Logger.LogWarning("No post-processing profile on the camera, colour filter disabled");
                        _warnedNoProfile = true;
                    }
                    return;
                }
                // Work on a copy so the shared profile asset is never changed.
                _post.profile = Object.Instantiate(_post.profile);
            }

            UserLutModel model = _post.profile.userLut;
            if (amount <= 0f)
            {
                model.enabled = false;
                return;
            }
            model.enabled = true;
            UserLutModel.Settings settings = model.settings;
            settings.lut = _lut;
            settings.contribution = amount;
            model.settings = settings;
        }
    }

    [HarmonyPatch(typeof(GameCamera), "UpdateCamera")]
    internal static class GameCameraUpdatePatch
    {
        private static void Postfix(GameCamera __instance, float dt)
        {
            RallyView.Apply(__instance, dt);
        }
    }
}
