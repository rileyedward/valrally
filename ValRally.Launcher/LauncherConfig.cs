using System;
using System.IO;

namespace ValRally.Launcher
{
    /// <summary>Fixed settings and folder layout.</summary>
    public static class LauncherConfig
    {
        /// <summary>
        /// GitHub "owner/repo" the launcher downloads ValRally releases from. This is the only
        /// place it's set; change it here if the repo moves.
        /// </summary>
        public const string GitHubRepo = "rileyedward/valrally";

        /// <summary>Valheim's Steam app id.</summary>
        public const string ValheimAppId = "892970";

        /// <summary>Release asset names. The zip is ValRally-&lt;version&gt;.zip.</summary>
        public const string ModZipPrefix = "ValRally-";
        public const string LauncherAssetName = "ValRally-Launcher.exe";

        public static string ReleasesPage => $"https://github.com/{GitHubRepo}/releases";

        public static Version LauncherVersion =>
            typeof(LauncherConfig).Assembly.GetName().Version;
    }

    /// <summary>
    /// Where the launcher keeps things. Home is %APPDATA%\ValRally unless --home overrides it:
    ///   profile\   BepInEx, its doorstop files and the plugins (what r2modman calls a profile)
    ///   cache\     downloaded zips, so a reinstall doesn't download again
    ///   state.json what's installed, and which files were added to the Valheim folder
    ///   launcher.log
    /// </summary>
    public sealed class LauncherPaths
    {
        public LauncherPaths(string home)
        {
            Home = Path.GetFullPath(home);
        }

        public static string DefaultHome =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ValRally");

        public string Home { get; }
        public string Profile => Path.Combine(Home, "profile");
        public string Cache => Path.Combine(Home, "cache");
        public string Backup => Path.Combine(Home, "valheim-backup");
        public string StateFile => Path.Combine(Home, "state.json");
        public string LogFile => Path.Combine(Home, "launcher.log");

        public string BepInEx => Path.Combine(Profile, "BepInEx");
        public string Preloader => Path.Combine(BepInEx, "core", "BepInEx.Preloader.dll");
        public string Config => Path.Combine(BepInEx, "config");
        public string Plugins => Path.Combine(BepInEx, "plugins");
    }
}
