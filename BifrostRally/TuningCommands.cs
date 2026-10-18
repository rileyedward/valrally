using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using BifrostRally.Core;
using Jotunn.Entities;

namespace BifrostRally.Valheim
{
    /// <summary>Applies config values to the live car, camera and filter after a change.</summary>
    internal static class LiveTuning
    {
        public static void Apply(CarConfig config, string section, FieldInfo field, ConfigEntryBase entry)
        {
            RaycastCar car = RallyDriver.Car;
            if (section == CarConfig.CarSection && car != null)
            {
                field.SetValue(car.Settings, entry.BoxedValue);
                car.RefreshMassProperties();
            }
            else if (section == CarConfig.FeelSection && car != null)
            {
                field.SetValue(car.Feel.Settings, entry.BoxedValue);
                car.RefreshMassProperties();
            }
            else if (section == CarConfig.StagesSection || section == CarConfig.StageTimingSection)
            {
                ApplyStages(config);
            }
            else if (section == CarConfig.SurfaceRulesSection || section == CarConfig.EffectsSection || section.StartsWith(CarConfig.SurfacePrefix))
            {
                ApplySurfaces(config);
            }
            else if (section == CarConfig.SectionFor(config.Preset.Value) || section == CarConfig.FilterSection)
            {
                RallyView.Configure(config.GetCamera(config.Preset.Value), config.Get<ColorFilterSettings>(CarConfig.FilterSection));
            }
        }

        public static void ApplySurfaces(CarConfig config)
        {
            RallyDriver.ConfigureSurfaces(config.Get<SurfaceRules>(CarConfig.SurfaceRulesSection), config.GetSurfaceProfiles());
            SurfaceEffects.Settings = config.Get<EffectSettings>(CarConfig.EffectsSection);
        }

        public static void ApplyStages(CarConfig config)
        {
            StageRunner.Options = config.Get<StageOptions>(CarConfig.StagesSection);
            StageRunner.Timer.Settings = config.Get<StageSettings>(CarConfig.StageTimingSection);
        }

        public static void Print(string text)
        {
            global::Console.instance.Print(text);
            Jotunn.Logger.LogInfo(text);
        }
    }

    internal class SetCommand : ConsoleCommand
    {
        private readonly CarConfig _config;

        public SetCommand(CarConfig config)
        {
            _config = config;
        }

        public override string Name => "rally_set";

        public override string Help => "rally_set <key> <value> - change a setting on the live car and save it (e.g. rally_set Grip 0.8, rally_set Feel.SteerYawTorque 3000)";

        public override void Run(string[] args)
        {
            if (args.Length < 2)
            {
                LiveTuning.Print("Usage: rally_set <key> <value>");
                return;
            }
            if (!_config.TryFind(args[0], out string section, out FieldInfo field, out ConfigEntryBase entry))
            {
                if (_config.TryFindOther(args[0], out ConfigEntryBase other))
                {
                    string old = other.GetSerializedValue();
                    other.SetSerializedValue(args[1]);
                    LiveTuning.Print($"{other.Definition.Section}.{other.Definition.Key}: {old} -> {other.GetSerializedValue()}");
                    return;
                }
                LiveTuning.Print($"No setting called {args[0]}. Try rally_get {args[0]}");
                return;
            }

            string before = entry.GetSerializedValue();
            entry.SetSerializedValue(args[1]);
            LiveTuning.Apply(_config, section, field, entry);
            // Wheel positions and size are baked into the car when it's built.
            bool geometry = section == CarConfig.CarSection && (field.Name == nameof(CarSettings.Wheelbase) ||
                field.Name == nameof(CarSettings.TrackWidth) || field.Name == nameof(CarSettings.MountHeight) ||
                field.Name == nameof(CarSettings.WheelRadius));
            string note = geometry ? " (takes effect on the next rally_spawn)" : "";
            LiveTuning.Print($"{section}.{field.Name}: {before} -> {entry.GetSerializedValue()}{note}");
        }
    }

    internal class GetCommand : ConsoleCommand
    {
        private readonly CarConfig _config;

        public GetCommand(CarConfig config)
        {
            _config = config;
        }

        public override string Name => "rally_get";

        public override string Help => "rally_get [text] - list settings whose name contains the text";

        public override void Run(string[] args)
        {
            string filter = args.Length > 0 ? args[0] : "";
            string[] lines = _config.Describe(filter).ToArray();
            foreach (string line in lines.Take(40))
            {
                LiveTuning.Print(line);
            }
            if (lines.Length > 40)
            {
                LiveTuning.Print($"... {lines.Length - 40} more, narrow it down (e.g. rally_get Feel.)");
            }
        }
    }

    /// <summary>
    /// Every rally_set already saves. These keep named snapshots of the whole config,
    /// so two tunes can be swapped back and forth.
    /// </summary>
    internal class SaveCommand : ConsoleCommand
    {
        private readonly CarConfig _config;

        public SaveCommand(CarConfig config)
        {
            _config = config;
        }

        public override string Name => "rally_save";

        public override string Help => "rally_save <name> - save the current settings as a named tune";

        public override void Run(string[] args)
        {
            if (args.Length < 1)
            {
                LiveTuning.Print("Usage: rally_save <name>");
                return;
            }
            string path = TunePaths.For(_config, args[0]);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.Copy(_config.FilePath, path, true);
            LiveTuning.Print($"Saved tune '{args[0]}' to {path}");
        }
    }

    internal class LoadCommand : ConsoleCommand
    {
        private readonly CarConfig _config;

        public LoadCommand(CarConfig config)
        {
            _config = config;
        }

        public override string Name => "rally_load";

        public override string Help => "rally_load <name> - load a saved tune (run rally_spawn to apply it fully)";

        public override void Run(string[] args)
        {
            if (args.Length < 1)
            {
                string dir = TunePaths.Directory(_config);
                string names = System.IO.Directory.Exists(dir)
                    ? string.Join(", ", System.IO.Directory.GetFiles(dir, "*.cfg").Select(Path.GetFileNameWithoutExtension))
                    : "none yet";
                LiveTuning.Print("Usage: rally_load <name>. Saved tunes: " + names);
                return;
            }
            string path = TunePaths.For(_config, args[0]);
            if (!File.Exists(path))
            {
                LiveTuning.Print($"No tune called '{args[0]}'");
                return;
            }
            File.Copy(path, _config.FilePath, true);
            _config.Reload();
            LiveTuning.Print($"Loaded tune '{args[0]}'. Run rally_spawn to apply it.");
        }
    }

    internal static class TunePaths
    {
        public static string Directory(CarConfig config) => Path.Combine(Path.GetDirectoryName(config.FilePath), "bifrostrally-tunes");

        public static string For(CarConfig config, string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            return Path.Combine(Directory(config), name + ".cfg");
        }
    }
}
