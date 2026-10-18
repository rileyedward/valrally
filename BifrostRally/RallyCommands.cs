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
            RallyDriver.Spawn(_config.Get<CarSettings>());
            RallyView.Configure(_config.Get<ChaseCameraSettings>(), _config.Get<ColorFilterSettings>());
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
