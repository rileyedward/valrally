using ValRally.Core;
using Jotunn.Entities;
using UnityEngine;

namespace ValRally.Valheim
{
    /// <summary>rally_surface: what each wheel is on and why. "hud" keeps it on screen.</summary>
    internal class SurfaceCommand : ConsoleCommand
    {
        private static readonly string[] WheelNames = { "FL", "FR", "RL", "RR" };

        public static bool ShowOnHud { get; private set; }

        public override string Name => "rally_surface";

        public override string Help => "rally_surface [hud] - print the surface under each wheel; 'hud' toggles a live readout";

        public override void Run(string[] args)
        {
            if (args.Length > 0 && args[0] == "hud")
            {
                ShowOnHud = !ShowOnHud;
                RallyHud.DebugText = null;
                LiveTuning.Print("Surface HUD " + (ShowOnHud ? "on" : "off"));
                return;
            }

            RaycastCar car = RallyDriver.Car;
            Vector3 probe = car != null ? car.transform.position : Player.m_localPlayer != null ? Player.m_localPlayer.transform.position : Vector3.zero;
            if (car == null)
            {
                Report("here", probe);
                return;
            }
            for (int i = 0; i < car.WheelCount; i++)
            {
                RaycastCar.WheelInfo wheel = car.GetWheel(i);
                Report(i < WheelNames.Length ? WheelNames[i] : "W" + i, wheel.Contact);
            }
            LiveTuning.Print(RallyDriver.Surfaces.Rules.Enabled ? "Surfaces are on" : "Surfaces are OFF (rally_set Surfaces.Enabled true)");
        }

        private static void Report(string label, Vector3 point)
        {
            int mask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain");
            if (Physics.Raycast(point + Vector3.up * 1f, Vector3.down, out RaycastHit hit, 5f, mask, QueryTriggerInteraction.Ignore))
            {
                Surface surface = RallyDriver.Surfaces.Describe(hit, out string why);
                LiveTuning.Print($"{label}: {surface} ({why})");
            }
            else
            {
                LiveTuning.Print($"{label}: no ground found");
            }
        }

        /// <summary>Called each frame; keeps the HUD line current when enabled.</summary>
        public static void UpdateHud()
        {
            RaycastCar car = RallyDriver.Car;
            if (!ShowOnHud || car == null)
            {
                return;
            }
            var text = new System.Text.StringBuilder();
            for (int i = 0; i < car.WheelCount; i++)
            {
                RaycastCar.WheelInfo wheel = car.GetWheel(i);
                text.Append(i < WheelNames.Length ? WheelNames[i] : "W" + i).Append(' ')
                    .Append(wheel.Grounded ? wheel.Surface.ToString() : "air").Append("   ");
            }
            RallyHud.DebugText = text.ToString();
        }
    }
}
