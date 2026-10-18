using BifrostRally.Core;
using UnityEngine;

namespace BifrostRally.Valheim
{
    /// <summary>
    /// Minimal on-screen readout while driving, drawn with Unity's immediate-mode GUI.
    /// Other features add lines through the static text slots.
    /// </summary>
    internal static class RallyHud
    {
        /// <summary>Stage timer line (M5), shown top centre when set.</summary>
        public static string TimerText;

        /// <summary>Last split or result, shown under the timer.</summary>
        public static string SplitText;

        /// <summary>Current pace note call (M6), shown large in the centre-top.</summary>
        public static string CallText;

        /// <summary>Debug line, e.g. the surface under the car.</summary>
        public static string DebugText;

        private static GUIStyle _big;
        private static GUIStyle _small;
        private static GUIStyle _call;

        public static void Draw(bool showGauges)
        {
            RaycastCar car = RallyDriver.Car;
            if (!RallyDriver.IsDriving || car == null)
            {
                return;
            }
            EnsureStyles();

            if (showGauges)
            {
                float x = Screen.width - 260f;
                float y = Screen.height - 150f;
                string gear = car.Drivetrain.Gear == Drivetrain.Reverse ? "R" : car.Drivetrain.Gear.ToString();
                GUI.Label(new Rect(x, y, 240f, 50f), $"{Mathf.Abs(car.SpeedKmh):F0} km/h", _big);
                GUI.Label(new Rect(x, y + 50f, 240f, 30f), $"Gear {gear}   {car.Drivetrain.Rpm:F0} rpm", _small);

                float redline = car.Settings.RedlineRpm;
                float fill = Mathf.Clamp01(car.Drivetrain.Rpm / Mathf.Max(1f, redline));
                GUI.Box(new Rect(x, y + 82f, 220f, 10f), GUIContent.none);
                Color old = GUI.color;
                GUI.color = fill > 0.9f ? new Color(1f, 0.35f, 0.25f) : new Color(1f, 0.85f, 0.4f);
                GUI.DrawTexture(new Rect(x, y + 82f, 220f * fill, 10f), Texture2D.whiteTexture);
                GUI.color = old;

                string flags = car.Traction.Active ? "TC" : "";
                GUI.Label(new Rect(x, y + 96f, 240f, 30f), flags, _small);
            }

            if (!string.IsNullOrEmpty(TimerText))
            {
                GUI.Label(new Rect(Screen.width * 0.5f - 200f, 20f, 400f, 50f), TimerText, _big);
            }
            if (!string.IsNullOrEmpty(SplitText))
            {
                GUI.Label(new Rect(Screen.width * 0.5f - 200f, 70f, 400f, 30f), SplitText, _small);
            }
            if (!string.IsNullOrEmpty(CallText))
            {
                GUI.Label(new Rect(Screen.width * 0.5f - 300f, Screen.height * 0.22f, 600f, 60f), CallText, _call);
            }
            if (!string.IsNullOrEmpty(DebugText))
            {
                GUI.Label(new Rect(20f, Screen.height - 60f, 600f, 30f), DebugText, _small);
            }
        }

        private static void EnsureStyles()
        {
            if (_big != null)
            {
                return;
            }
            _big = new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _big.normal.textColor = Color.white;
            _small = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
            _small.normal.textColor = Color.white;
            _call = new GUIStyle(GUI.skin.label) { fontSize = 40, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _call.normal.textColor = new Color(1f, 0.93f, 0.6f);
        }
    }
}
