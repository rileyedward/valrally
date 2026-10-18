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

        public override string Help => "Spawn the rally car in front of you (re-reads [Car] settings from the config file)";

        public override void Run(string[] args)
        {
            RallyDriver.Spawn(_config.Load());
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
