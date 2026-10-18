using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ValRally.Launcher
{
    /// <summary>The bits of a Thunderstore manifest.json the launcher needs.</summary>
    public sealed class PackageManifest
    {
        public string Name { get; set; }
        public Version Version { get; set; }
        public List<PackageRef> Dependencies { get; set; } = new List<PackageRef>();

        public static PackageManifest Read(string zipPath)
        {
            using var zip = ZipFile.OpenRead(zipPath);
            var entry = zip.Entries.FirstOrDefault(e => e.FullName.Equals("manifest.json", StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidDataException($"{Path.GetFileName(zipPath)} has no manifest.json, so it isn't a ValRally release zip.");
            using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
            var json = Json.Parse(reader.ReadToEnd());
            var manifest = new PackageManifest { Name = json.Str("name"), Version = Sources.ParseVersion(json.Str("version_number")) };
            if (json.TryGetValue("dependencies", out var deps) && deps is IEnumerable list)
                manifest.Dependencies.AddRange(list.Cast<object>().Select(d => PackageRef.Parse((string)d)));
            return manifest;
        }
    }

    /// <summary>
    /// Installs BepInEx, Jotunn and ValRally into the launcher's profile, the same layout r2modman
    /// uses, and prepares the Valheim folder for launching from it. Vanilla Valheim stays as it was:
    /// the only files added to the game folder are doorstop's loader (winhttp.dll) and a
    /// doorstop_config.ini that keeps it switched off unless the launcher's Play turns it on.
    /// </summary>
    public sealed class Installer
    {
        private readonly LauncherPaths paths;
        private readonly Action<string> log;

        public Installer(LauncherPaths paths, Action<string> log)
        {
            this.paths = paths;
            this.log = log;
        }

        public LauncherState State { get; private set; }

        public void LoadState() => State = LauncherState.Load(paths.StateFile);

        private void SaveState() => State.Save(paths.StateFile);

        /// <summary>
        /// Installs or updates from a ValRally release zip. Dependencies come from Thunderstore at the
        /// exact versions the zip's manifest pins (from the cache when already downloaded).
        /// </summary>
        public async Task Install(string modZip, Sources sources)
        {
            LoadState();
            var manifest = PackageManifest.Read(modZip);
            if (manifest.Version == null) throw new InvalidDataException("The zip's manifest.json has no version_number.");
            log($"Installing ValRally {manifest.Version.ToString(3)} into {paths.Profile}");
            Directory.CreateDirectory(paths.Profile);

            // BepInEx pack first, so its folders exist before plugins go in.
            foreach (var dep in manifest.Dependencies.OrderBy(d => IsBepInExPack(d) ? 0 : 1))
            {
                var hasFiles = IsBepInExPack(dep) ? File.Exists(paths.Preloader) : Directory.Exists(Path.Combine(paths.Plugins, dep.Id));
                if (State.Packages.TryGetValue(dep.Id, out var installed) && installed == dep.Version && hasFiles)
                {
                    log($"{dep.FullName} already installed");
                    continue;
                }
                var zip = await sources.DownloadPackage(dep);
                if (IsBepInExPack(dep)) InstallBepInExPack(zip);
                else InstallPlugin(zip, dep.Id);
                State.Packages[dep.Id] = dep.Version;
                SaveState();
            }

            InstallPlugin(modZip, "ValRally");
            State.ModVersion = manifest.Version.ToString(3);
            SaveState();
            log($"ValRally {State.ModVersion} installed");
        }

        private static bool IsBepInExPack(PackageRef package) => package.Name.StartsWith("BepInExPack", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// The pack's zip has the game-folder files under a top folder (BepInExPack_Valheim/), next to
        /// its own manifest and icon. That folder's contents go to the profile root. The user's
        /// BepInEx config files are kept on an update.
        /// </summary>
        private void InstallBepInExPack(string zipPath)
        {
            log($"Extracting {Path.GetFileName(zipPath)}");
            using var zip = ZipFile.OpenRead(zipPath);
            var preloader = zip.Entries.FirstOrDefault(e => e.FullName.Replace('\\', '/').EndsWith("BepInEx/core/BepInEx.Preloader.dll", StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidDataException($"{Path.GetFileName(zipPath)} doesn't contain BepInEx/core/BepInEx.Preloader.dll.");
            var path = preloader.FullName.Replace('\\', '/');
            var prefix = path.Substring(0, path.Length - "BepInEx/core/BepInEx.Preloader.dll".Length);
            foreach (var entry in zip.Entries)
            {
                var name = entry.FullName.Replace('\\', '/');
                if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || name.EndsWith("/")) continue;
                var relative = name.Substring(prefix.Length);
                var keep = relative.StartsWith("BepInEx/config/", StringComparison.OrdinalIgnoreCase);
                Extract(entry, paths.Profile, relative, overwrite: !keep);
            }
        }

        /// <summary>
        /// A mod package, laid out the way r2modman does it: plugins\ and loose files go to
        /// BepInEx\plugins\&lt;folder&gt;, patchers\ to BepInEx\patchers\&lt;folder&gt;, config\ to
        /// BepInEx\config (never overwriting), and anything already under BepInEx\ stays where it is.
        /// The plugin's old folder is cleared first, so an update leaves no stale files.
        /// </summary>
        private void InstallPlugin(string zipPath, string folder)
        {
            log($"Extracting {Path.GetFileName(zipPath)}");
            var pluginDir = Path.Combine(paths.Plugins, folder);
            var patcherDir = Path.Combine(paths.BepInEx, "patchers", folder);
            DeleteDirectory(pluginDir);
            DeleteDirectory(patcherDir);
            using var zip = ZipFile.OpenRead(zipPath);
            foreach (var entry in zip.Entries)
            {
                var name = entry.FullName.Replace('\\', '/');
                if (name.EndsWith("/")) continue;
                if (StartsWith(name, "BepInEx/", out var underBepInEx)) Extract(entry, paths.BepInEx, underBepInEx, overwrite: true);
                else if (StartsWith(name, "config/", out var config)) Extract(entry, paths.Config, config, overwrite: false);
                else if (StartsWith(name, "patchers/", out var patcher)) Extract(entry, patcherDir, patcher, overwrite: true);
                else if (StartsWith(name, "plugins/", out var plugin)) Extract(entry, pluginDir, plugin, overwrite: true);
                else Extract(entry, pluginDir, name, overwrite: true);
            }
        }

        private static bool StartsWith(string name, string prefix, out string rest)
        {
            var match = name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
            rest = match ? name.Substring(prefix.Length) : null;
            return match;
        }

        private static void Extract(ZipArchiveEntry entry, string root, string relative, bool overwrite)
        {
            var rootFull = Path.GetFullPath(root).TrimEnd('\\') + "\\";
            var target = Path.GetFullPath(Path.Combine(rootFull, relative.Replace('/', '\\')));
            if (!target.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Zip entry {entry.FullName} points outside the install folder.");
            if (!overwrite && File.Exists(target)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            entry.ExtractToFile(target, overwrite: true);
        }

        /// <summary>
        /// Puts doorstop's loader in the Valheim folder (switched off by its config file, so plain
        /// Steam launches stay vanilla) and returns the Steam launch arguments that switch it on
        /// and point it at the profile's BepInEx.
        /// </summary>
        public string PrepareGameFolder(string valheimDir)
        {
            LoadState();
            if (!File.Exists(paths.Preloader)) throw new InvalidOperationException("ValRally isn't installed yet.");
            var packIni = Path.Combine(paths.Profile, "doorstop_config.ini");
            var packIniText = File.Exists(packIni) ? File.ReadAllText(packIni) : "";
            // Doorstop 4 writes target_assembly in its config and takes --doorstop-enabled;
            // doorstop 3 writes targetAssembly and takes --doorstop-enable.
            var doorstop4 = packIniText.IndexOf("target_assembly", StringComparison.OrdinalIgnoreCase) >= 0 || packIniText.Length == 0;

            if (State.GameFilesDir != null && !string.Equals(State.GameFilesDir, valheimDir, StringComparison.OrdinalIgnoreCase))
                RemoveGameFiles();
            State.GameFilesDir = valheimDir;

            // A BepInEx installed straight into the game folder uses the same two files; leave them
            // alone and rely on the launch arguments instead.
            var manualBepInEx = Directory.Exists(Path.Combine(valheimDir, "BepInEx", "core"));
            if (manualBepInEx) log("Valheim's folder has its own BepInEx; leaving its doorstop files alone.");
            else
            {
                PlaceGameFile(valheimDir, "winhttp.dll", File.ReadAllBytes(Path.Combine(paths.Profile, "winhttp.dll")));
                PlaceGameFile(valheimDir, "doorstop_config.ini", Encoding.UTF8.GetBytes(GameFolderIni(packIniText)));
            }
            SaveState();

            var args = doorstop4
                ? $"--doorstop-enabled true --doorstop-target-assembly \"{paths.Preloader}\""
                : $"--doorstop-enable true --doorstop-target \"{paths.Preloader}\"";
            var corlib = Path.Combine(paths.Profile, "unstripped_corlib");
            if (Directory.Exists(corlib))
                args += doorstop4 ? $" --doorstop-mono-dll-search-path-override \"{corlib}\"" : $" --mono-dll-search-path-override \"{corlib}\"";
            return args;
        }

        /// <summary>The pack's doorstop config, switched off, with its paths made absolute into the profile.</summary>
        private string GameFolderIni(string packIni)
        {
            if (packIni.Length == 0)
                return $"[General]\r\nenabled = false\r\ntarget_assembly = {paths.Preloader}\r\n";
            var lines = Regex.Split(packIni, "\r?\n").Select(line =>
            {
                var match = Regex.Match(line, @"^(\s*)([A-Za-z_]+)(\s*=\s*)(.*)$");
                if (!match.Success) return line;
                var key = match.Groups[2].Value;
                var value = match.Groups[4].Value.Trim();
                if (key.Equals("enabled", StringComparison.OrdinalIgnoreCase)) value = "false";
                else if (IsPathKey(key) && value.Length > 0 && !Path.IsPathRooted(value))
                    value = Path.GetFullPath(Path.Combine(paths.Profile, value));
                return match.Groups[1].Value + key + match.Groups[3].Value + value;
            });
            return string.Join("\r\n", lines);
        }

        private static bool IsPathKey(string key)
        {
            var k = key.Replace("_", "").ToLowerInvariant();
            return k == "targetassembly" || k == "dllsearchpathoverride" || k == "bootconfigoverride";
        }

        private void PlaceGameFile(string valheimDir, string name, byte[] content)
        {
            var target = Path.Combine(valheimDir, name);
            var hash = Sha256(content);
            var record = State.GameFiles.FirstOrDefault(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (File.Exists(target) && Sources.Sha256(target) == hash)
            {
                // Already there before the launcher (an earlier r2modman launch, say): leave it in place on uninstall.
                if (record == null) State.GameFiles.Add(new GameFile { Name = name, Sha256 = hash, WasThere = true });
                return;
            }
            var backedUp = record?.BackedUp ?? false;
            // Someone else's file (an earlier r2modman launch, say): keep a copy to put back on uninstall.
            if (File.Exists(target) && (record == null || Sources.Sha256(target) != record.Sha256))
            {
                Directory.CreateDirectory(paths.Backup);
                File.Copy(target, Path.Combine(paths.Backup, name), overwrite: true);
                backedUp = true;
                log($"Saved Valheim's existing {name} to {paths.Backup}");
            }
            File.WriteAllBytes(target, content);
            State.GameFiles.RemoveAll(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            State.GameFiles.Add(new GameFile { Name = name, Sha256 = hash, BackedUp = backedUp });
            log($"Wrote {name} to the Valheim folder");
        }

        /// <summary>Takes the launcher's files back out of the Valheim folder, restoring any it replaced.</summary>
        public void RemoveGameFiles()
        {
            if (State.GameFilesDir == null) return;
            foreach (var file in State.GameFiles)
            {
                var target = Path.Combine(State.GameFilesDir, file.Name);
                var backup = Path.Combine(paths.Backup, file.Name);
                // Changed since (r2modman relinked it, a manual install): no longer ours to touch.
                if (file.WasThere || (File.Exists(target) && Sources.Sha256(target) != file.Sha256)) continue;
                if (file.BackedUp && File.Exists(backup))
                {
                    File.Copy(backup, target, overwrite: true);
                    log($"Restored Valheim's original {file.Name}");
                }
                else if (File.Exists(target))
                {
                    File.Delete(target);
                    log($"Removed {file.Name} from the Valheim folder");
                }
            }
            State.GameFiles.Clear();
            State.GameFilesDir = null;
            DeleteDirectory(paths.Backup);
        }

        /// <summary>
        /// Removes the profile, cache and the launcher's files in the Valheim folder. With keepConfig,
        /// BepInEx\config stays, which holds ValRally's settings, saved stages and tunes.
        /// </summary>
        public void Uninstall(bool keepConfig)
        {
            LoadState();
            RemoveGameFiles();
            if (Directory.Exists(paths.Profile))
            {
                if (!keepConfig) DeleteDirectory(paths.Profile);
                else
                {
                    foreach (var dir in Directory.GetDirectories(paths.Profile))
                        if (!dir.Equals(paths.BepInEx, StringComparison.OrdinalIgnoreCase)) DeleteDirectory(dir);
                    foreach (var file in Directory.GetFiles(paths.Profile)) File.Delete(file);
                    if (Directory.Exists(paths.BepInEx))
                    {
                        foreach (var dir in Directory.GetDirectories(paths.BepInEx))
                            if (!dir.Equals(paths.Config, StringComparison.OrdinalIgnoreCase)) DeleteDirectory(dir);
                        foreach (var file in Directory.GetFiles(paths.BepInEx)) File.Delete(file);
                    }
                }
            }
            DeleteDirectory(paths.Cache);
            var valheimPath = State.ValheimPath;
            State = new LauncherState { ValheimPath = valheimPath };
            if (File.Exists(paths.StateFile)) File.Delete(paths.StateFile);
            if (!string.IsNullOrEmpty(valheimPath)) SaveState();
            log(keepConfig ? "Uninstalled. Settings, stages and tunes were kept." : "Uninstalled.");
        }

        public void SetValheimPath(string path)
        {
            LoadState();
            State.ValheimPath = path;
            SaveState();
        }

        private static string Sha256(byte[] content)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(content)).Replace("-", "").ToLowerInvariant();
        }

        private static void DeleteDirectory(string dir)
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }
}
