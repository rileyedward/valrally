using BifrostRally.Core;
using UnityEngine;

namespace BifrostRally.Valheim
{
    /// <summary>
    /// Valheim glue for the car: spawning, getting in and out, and feeding input.
    /// </summary>
    internal static class RallyDriver
    {
        private const float EnterDistance = 6f;

        public static RaycastCar Car { get; private set; }
        public static bool IsDriving { get; private set; }

        // The Viking and other characters are on character layers, so leaving those out
        // keeps the wheel rays from hitting the driver. The car itself sits on "vehicle".
        private static int GroundMask => LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain");

        public static void Spawn(CarSettings settings, FeelSettings feel)
        {
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                return;
            }

            Despawn();
            Vector3 forward = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up).normalized;
            Vector3 position = player.transform.position + forward * 4f + Vector3.up * 1f;
            Car = CarFactory.CreateBoxCar(position, Quaternion.LookRotation(forward), settings, feel, LayerMask.NameToLayer("vehicle"), GroundMask, FindCarMaterial());
            ApplySurfaces();
            Jotunn.Logger.LogInfo($"Spawned rally car at {position}");
        }

        public static readonly ValheimSurfaceProvider Surfaces = new ValheimSurfaceProvider();
        private static SurfaceProfile[] _profiles;

        /// <summary>Hook the ground reader and surface profiles into the car (or leave it neutral).</summary>
        public static void ConfigureSurfaces(SurfaceRules rules, SurfaceProfile[] profiles)
        {
            Surfaces.Rules = rules;
            _profiles = profiles;
            ApplySurfaces();
        }

        private static void ApplySurfaces()
        {
            if (Car == null)
            {
                return;
            }
            bool on = Surfaces.Rules.Enabled && _profiles != null;
            Car.SurfaceProvider = on ? Surfaces : null;
            if (on)
            {
                SurfaceProfile[] profiles = _profiles;
                Car.SurfaceProfiles = surface => profiles[(int)surface];
            }
        }

        /// <summary>
        /// Unity's default primitive material renders pink in Valheim, so borrow the
        /// vanilla cart's material, which uses a shader the game actually ships.
        /// </summary>
        internal static Material FindCarMaterial()
        {
            GameObject cart = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab("Cart") : null;
            MeshRenderer renderer = cart != null ? cart.GetComponentInChildren<MeshRenderer>() : null;
            if (renderer == null)
            {
                Jotunn.Logger.LogWarning("Cart material not found, the car will render without one");
                return null;
            }
            return renderer.sharedMaterial;
        }

        public static void Despawn()
        {
            StopDriving();
            if (Car != null)
            {
                Object.Destroy(Car.gameObject);
                Car = null;
            }
        }

        public static void Update(KeyCode driveKey)
        {
            Player player = Player.m_localPlayer;
            if (player == null || Car == null)
            {
                IsDriving = false;
                return;
            }

            // Something else (death, teleport) can detach the player.
            if (IsDriving && !player.IsAttached())
            {
                IsDriving = false;
            }

            bool typing = IsTyping();
            if (!typing && ZInput.GetKeyDown(driveKey))
            {
                if (IsDriving)
                {
                    StopDriving();
                }
                // Not during the Valkyrie intro, and only once the Viking stands on the ground.
                else if (!player.InIntro() && player.IsOnGround() &&
                    Vector3.Distance(player.transform.position, Car.transform.position) <= EnterDistance)
                {
                    StartDriving(player);
                }
            }

            SurfaceEffects.Update(Car, Time.deltaTime);

            if (ScriptedInput.HasValue)
            {
                Car.Input = ScriptedInput.Value;
            }
            else
            {
                Car.Input = IsDriving && !typing ? ReadInput() : new CarInput { Brake = 0f, Handbrake = !IsDriving };
            }
        }

        /// <summary>When set, the test harness drives instead of the keyboard.</summary>
        public static CarInput? ScriptedInput;

        /// <summary>True while the console, chat or menu has the keyboard.</summary>
        public static bool IsTyping()
        {
            return global::Console.IsVisible() || (Chat.instance != null && Chat.instance.HasFocus()) || Menu.IsVisible();
        }

        private static CarInput ReadInput()
        {
            return new CarInput
            {
                Steer = (ZInput.GetButton("Right") ? 1f : 0f) - (ZInput.GetButton("Left") ? 1f : 0f),
                Throttle = ZInput.GetButton("Forward") ? 1f : 0f,
                Brake = ZInput.GetButton("Backward") ? 1f : 0f,
                Handbrake = ZInput.GetButton("Jump"),
            };
        }

        private static void StartDriving(Player player)
        {
            player.AttachStart(Car.Seat, Car.gameObject, hideWeapons: false, isBed: false, onShip: false,
                "attach_chair", new Vector3(-1.6f, 0.2f, 0f));
            IsDriving = player.IsAttached();
            Jotunn.Logger.LogInfo(IsDriving ? "Driving" : "Could not get in the car");
        }

        private static void StopDriving()
        {
            if (IsDriving && Player.m_localPlayer != null)
            {
                Player.m_localPlayer.AttachStop();
            }
            IsDriving = false;
        }
    }
}
