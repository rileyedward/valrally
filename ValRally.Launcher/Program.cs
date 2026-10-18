using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ValRally.Launcher
{
    /// <summary>
    /// ValRally-Launcher.exe                      opens the launcher window
    /// ValRally-Launcher.exe &lt;command&gt;            runs one step without the window and exits:
    ///   status                                   what's installed, where Valheim is, what's on GitHub
    ///   install                                  install or update from the latest GitHub release
    ///   play                                     start Valheim with ValRally
    ///   play --dry-run                           prepare the Valheim folder and print the launch arguments, without starting it
    ///   uninstall [--keep-config]                remove it (optionally keeping settings, stages and tunes)
    /// Options, for the window or a command:
    ///   --zip &lt;file&gt;      install this ValRally zip instead of the GitHub release (testing a local build)
    ///   --home &lt;folder&gt;   keep profile, cache and state here instead of %APPDATA%\ValRally
    ///   --valheim &lt;folder&gt; use this Valheim folder instead of finding it through Steam
    /// Commands print to the console they're started from and to launcher.log in the home folder.
    /// </summary>
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            string command = null, zip = null, home = null, valheim = null;
            bool keepConfig = false, dryRun = false;
            for (var i = 0; i < args.Length; i++)
            {
                switch (args[i].ToLowerInvariant())
                {
                    case "--zip":
                    case "--home":
                    case "--valheim":
                        if (i + 1 >= args.Length) return Fail($"{args[i]} needs a value.");
                        var value = args[++i];
                        if (args[i - 1].Equals("--zip", StringComparison.OrdinalIgnoreCase)) zip = value;
                        else if (args[i - 1].Equals("--home", StringComparison.OrdinalIgnoreCase)) home = value;
                        else valheim = value;
                        break;
                    case "--keep-config": keepConfig = true; break;
                    case "--dry-run": dryRun = true; break;
                    default:
                        if (command == null && !args[i].StartsWith("-")) command = args[i].ToLowerInvariant();
                        else return Fail($"Unknown argument: {args[i]}");
                        break;
                }
            }
            var paths = new LauncherPaths(home ?? LauncherPaths.DefaultHome);

            if (command == null)
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm(paths, valheim, zip));
                return 0;
            }

            AttachConsole(-1);
            Console.WriteLine();
            using var launcher = new Launcher(paths, Console.WriteLine, valheim);
            try
            {
                Run(launcher, command, zip, keepConfig, dryRun).GetAwaiter().GetResult();
                return 0;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error: {e.GetBaseException().Message}");
                return 1;
            }
        }

        private static async Task Run(Launcher launcher, string command, string zip, bool keepConfig, bool dryRun)
        {
            launcher.FindValheim();
            switch (command)
            {
                case "status":
                    Console.WriteLine($"Launcher:  {LauncherConfig.LauncherVersion.ToString(3)} (releases from {LauncherConfig.GitHubRepo})");
                    Console.WriteLine($"Home:      {launcher.Paths.Home}");
                    Console.WriteLine($"Steam:     {launcher.SteamPath ?? "not found"}");
                    Console.WriteLine($"Valheim:   {launcher.ValheimPath ?? "not found"}");
                    Console.WriteLine($"Installed: {(launcher.State.IsInstalled ? "ValRally " + launcher.State.ModVersion : "nothing")}");
                    foreach (var package in launcher.State.Packages) Console.WriteLine($"           {package.Key} {package.Value}");
                    await launcher.CheckForUpdates();
                    if (launcher.Latest != null) Console.WriteLine($"GitHub:    ValRally {launcher.Latest.Version.ToString(3)}{(launcher.UpdateAvailable ? " (update available)" : "")}");
                    break;
                case "install":
                case "update":
                    if (zip != null) await launcher.InstallFromZip(zip);
                    else await launcher.InstallLatest();
                    break;
                case "play":
                    if (!launcher.State.IsInstalled)
                    {
                        if (zip != null) await launcher.InstallFromZip(zip);
                        else await launcher.InstallLatest();
                    }
                    launcher.Play(dryRun);
                    break;
                case "uninstall":
                    launcher.Uninstall(keepConfig);
                    break;
                default:
                    throw new ArgumentException($"Unknown command '{command}'. Use status, install, update, play or uninstall.");
            }
        }

        private static int Fail(string message)
        {
            AttachConsole(-1);
            Console.WriteLine();
            Console.WriteLine(message);
            return 2;
        }

        [DllImport("kernel32.dll")]
        private static extern bool AttachConsole(int processId);
    }
}
