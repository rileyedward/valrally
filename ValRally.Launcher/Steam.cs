using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace ValRally.Launcher
{
    /// <summary>Finds Steam and Valheim, and starts the game through Steam.</summary>
    public static class Steam
    {
        /// <summary>Steam's install folder from the registry, or null.</summary>
        public static string FindSteam()
        {
            var candidates = new[]
            {
                Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string,
                Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", null) as string,
                Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam", "InstallPath", null) as string,
            };
            foreach (var candidate in candidates)
            {
                if (string.IsNullOrEmpty(candidate)) continue;
                var path = Path.GetFullPath(candidate.Replace('/', '\\'));
                if (File.Exists(Path.Combine(path, "steam.exe"))) return path;
            }
            return null;
        }

        /// <summary>Every Steam library folder listed in steamapps\libraryfolders.vdf, starting with Steam's own.</summary>
        public static List<string> LibraryFolders(string steam)
        {
            var folders = new List<string> { steam };
            var vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
            if (File.Exists(vdf))
            {
                // "path"  "D:\\SteamLibrary" (current format); older files used "1" "D:\\SteamLibrary".
                foreach (Match match in Regex.Matches(File.ReadAllText(vdf), "^\\s*\"(?:path|\\d+)\"\\s+\"([^\"]+)\"", RegexOptions.Multiline))
                {
                    var path = match.Groups[1].Value.Replace("\\\\", "\\");
                    if (Directory.Exists(path)) folders.Add(path);
                }
            }
            return folders.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>Valheim's folder in any Steam library, or null if it isn't installed.</summary>
        public static string FindValheim(string steam)
        {
            if (steam == null) return null;
            foreach (var library in LibraryFolders(steam))
            {
                var apps = Path.Combine(library, "steamapps");
                var installDir = "Valheim";
                var manifest = Path.Combine(apps, $"appmanifest_{LauncherConfig.ValheimAppId}.acf");
                if (File.Exists(manifest))
                {
                    var match = Regex.Match(File.ReadAllText(manifest), "\"installdir\"\\s+\"([^\"]+)\"");
                    if (match.Success) installDir = match.Groups[1].Value;
                }
                var dir = Path.Combine(apps, "common", installDir);
                if (IsValheimFolder(dir)) return dir;
            }
            return null;
        }

        public static bool IsValheimFolder(string dir)
        {
            return !string.IsNullOrEmpty(dir) && File.Exists(Path.Combine(dir, "valheim.exe"));
        }

        public static bool IsValheimRunning()
        {
            return Process.GetProcessesByName("valheim").Length > 0;
        }

        public static void OpenStorePage()
        {
            Open($"steam://store/{LauncherConfig.ValheimAppId}");
        }

        /// <summary>Starts Valheim through Steam with extra command-line arguments.</summary>
        public static void LaunchValheim(string steam, string arguments)
        {
            var exe = steam == null ? null : Path.Combine(steam, "steam.exe");
            if (exe == null || !File.Exists(exe)) throw new InvalidOperationException("Steam wasn't found, so Valheim can't be started.");
            Process.Start(new ProcessStartInfo(exe, $"-applaunch {LauncherConfig.ValheimAppId} {arguments}") { UseShellExecute = false });
        }

        public static void Open(string target)
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
    }
}
