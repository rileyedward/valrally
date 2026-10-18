using BifrostRally.Core;
using Jotunn.Entities;

namespace BifrostRally.Valheim
{
    internal class SpawnCarCommand : ConsoleCommand
    {
        private readonly CarConfig _config;

        public SpawnCarCommand(CarConfig config)
        {
            _config = config;
        }

        public override string Name => "rally_spawn";

        public override string Help => "Spawn the rally car in front of you (re-reads the config file)";

        public override void Run(string[] args)
        {
            _config.Reload();
            RallyDriver.Spawn(_config.Get<CarSettings>(CarConfig.CarSection), _config.Get<FeelSettings>(CarConfig.FeelSection));
            RallyView.Configure(_config.GetCamera(_config.Preset.Value), _config.Get<ColorFilterSettings>(CarConfig.FilterSection));
            LiveTuning.ApplySurfaces(_config);
        }
    }

    /// <summary>Switch tyre model on the current car without respawning, for back-to-back comparison.</summary>
    internal class TyresCommand : ConsoleCommand
    {
        private readonly CarConfig _config;

        public TyresCommand(CarConfig config)
        {
            _config = config;
        }

        public override string Name => "rally_tyres";

        public override string Help => "rally_tyres pacejka|simple - switch tyre model on the current car (saved to config)";

        public override void Run(string[] args)
        {
            bool? pacejka = args.Length == 0 ? null
                : args[0] == "pacejka" ? true
                : args[0] == "simple" ? (bool?)false
                : null;
            if (pacejka == null)
            {
                global::Console.instance.Print("Usage: rally_tyres pacejka|simple");
                return;
            }

            _config.Set(CarConfig.CarSection, nameof(CarSettings.PacejkaTyres), pacejka.Value);
            if (RallyDriver.Car != null)
            {
                RallyDriver.Car.Settings.PacejkaTyres = pacejka.Value;
            }
            global::Console.instance.Print("Tyres: " + (pacejka.Value ? "pacejka" : "simple"));
        }
    }

    internal class RemoveCarCommand : ConsoleCommand
    {
        public override string Name => "rally_remove";

        public override string Help => "Remove the rally car";

        public override void Run(string[] args)
        {
            RallyDriver.Despawn();
        }
    }
}
