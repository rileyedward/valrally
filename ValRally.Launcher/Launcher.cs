using System;
using System.IO;
using System.Threading.Tasks;

namespace ValRally.Launcher
{
    /// <summary>What the window and the command line both do: find Valheim, install, update, play, uninstall.</summary>
    public sealed class Launcher : IDisposable
    {
        private readonly Action<string> log;

        public Launcher(LauncherPaths paths, Action<string> log, string valheimOverride = null)
        {
            Paths = paths;
            this.log = message =>
            {
                try
                {
                    Directory.CreateDirectory(paths.Home);
                    File.AppendAllText(paths.LogFile, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}\r\n");
                }
                catch (IOException) { }
                log(message);
            };
            Installer = new Installer(paths, this.log);
            Sources = new Sources(paths, this.log);
            Installer.LoadState();
            if (valheimOverride != null) ValheimOverride = Path.GetFullPath(valheimOverride);
        }

        public LauncherPaths Paths { get; }
        public Installer Installer { get; }
        public Sources Sources { get; }
        public LauncherState State => Installer.State;
        public string SteamPath { get; private set; }
        public string ValheimPath { get; private set; }
        /// <summary>From --valheim: used for this run only, never saved.</summary>
        public string ValheimOverride { get; }
        /// <summary>The newest GitHub release, after CheckForUpdates; null if offline or there's none.</summary>
        public ModRelease Latest { get; private set; }
        public string UpdateCheckError { get; private set; }

        public void Dispose() => Sources.Dispose();

        public void FindValheim()
        {
            Installer.LoadState();
            SteamPath = Steam.FindSteam();
            if (Steam.IsValheimFolder(ValheimOverride)) ValheimPath = ValheimOverride;
            else if (Steam.IsValheimFolder(State.ValheimPath)) ValheimPath = State.ValheimPath;
            else ValheimPath = Steam.FindValheim(SteamPath);
        }

        public async Task CheckForUpdates()
        {
            try
            {
                Latest = await Sources.LatestModRelease();
                UpdateCheckError = Latest == null ? $"No ValRally release on GitHub yet ({LauncherConfig.GitHubRepo})." : null;
            }
            catch (Exception e)
            {
                Latest = null;
                UpdateCheckError = $"Couldn't check GitHub for updates: {e.GetBaseException().Message}";
            }
            if (UpdateCheckError != null) log(UpdateCheckError);
        }

        public Version InstalledVersion => Sources.ParseVersion(State.ModVersion);

        public bool UpdateAvailable => Latest != null && (InstalledVersion == null || Latest.Version > InstalledVersion);

        public bool LauncherUpdateAvailable =>
            Latest?.LauncherUrl != null && Latest.Version > LauncherConfig.LauncherVersion;

        /// <summary>Installs or updates to the latest GitHub release.</summary>
        public async Task InstallLatest()
        {
            EnsureGameClosed();
            if (Latest == null) await CheckForUpdates();
            if (Latest == null) throw new InvalidOperationException(UpdateCheckError ?? "No ValRally release found.");
            var zip = await Sources.DownloadModZip(Latest);
            await Installer.Install(zip, Sources);
        }

        /// <summary>Installs from a local ValRally zip, e.g. one the release script just built.</summary>
        public async Task InstallFromZip(string zip)
        {
            EnsureGameClosed();
            await Installer.Install(Path.GetFullPath(zip), Sources);
        }

        /// <summary>Starts Valheim with ValRally. A dry run prepares the game folder and logs the arguments without starting it.</summary>
        public void Play(bool dryRun = false)
        {
            if (ValheimPath == null) throw new InvalidOperationException("Valheim wasn't found.");
            EnsureGameClosed();
            var args = Installer.PrepareGameFolder(ValheimPath);
            if (dryRun)
            {
                log($"Dry run, not starting Valheim. Steam arguments: -applaunch {LauncherConfig.ValheimAppId} {args}");
                return;
            }
            log($"Starting Valheim through Steam: {args}");
            Steam.LaunchValheim(SteamPath, args);
        }

        public void Uninstall(bool keepConfig)
        {
            EnsureGameClosed();
            Installer.Uninstall(keepConfig);
        }

        public void SetValheimPath(string path)
        {
            Installer.SetValheimPath(path);
            FindValheim();
        }

        private static void EnsureGameClosed()
        {
            if (Steam.IsValheimRunning()) throw new InvalidOperationException("Valheim is running. Close it first.");
        }
    }
}
