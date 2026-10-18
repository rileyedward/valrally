using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace ValRally.Launcher
{
    /// <summary>The newest ValRally release on GitHub.</summary>
    public sealed class ModRelease
    {
        public Version Version { get; set; }
        public string Tag { get; set; }
        public string PageUrl { get; set; }
        public string ZipName { get; set; }
        public string ZipUrl { get; set; }
        /// <summary>The .sha256 file next to the zip, if the release has one.</summary>
        public string Sha256Url { get; set; }
        /// <summary>The launcher exe attached to the same release, if any.</summary>
        public string LauncherUrl { get; set; }
    }

    /// <summary>A Thunderstore dependency string, e.g. denikson-BepInExPack_Valheim-5.4.2351.</summary>
    public sealed class PackageRef
    {
        public string Namespace { get; private set; }
        public string Name { get; private set; }
        public string Version { get; private set; }
        public string Id => $"{Namespace}-{Name}";
        public string FullName => $"{Namespace}-{Name}-{Version}";

        public static PackageRef Parse(string dependency)
        {
            // Names can't contain '-', so namespace is before the first and version after the last.
            var first = dependency.IndexOf('-');
            var last = dependency.LastIndexOf('-');
            if (first <= 0 || last <= first) throw new FormatException($"Not a Thunderstore dependency: {dependency}");
            return new PackageRef
            {
                Namespace = dependency.Substring(0, first),
                Name = dependency.Substring(first + 1, last - first - 1),
                Version = dependency.Substring(last + 1),
            };
        }

        public override string ToString() => FullName;
    }

    /// <summary>GitHub Releases and Thunderstore downloads.</summary>
    public sealed class Sources : IDisposable
    {
        private readonly HttpClient http;
        private readonly LauncherPaths paths;
        private readonly Action<string> log;

        public Sources(LauncherPaths paths, Action<string> log)
        {
            this.paths = paths;
            this.log = log;
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            http = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate });
            http.DefaultRequestHeaders.UserAgent.ParseAdd($"ValRally-Launcher/{LauncherConfig.LauncherVersion.ToString(3)}");
            http.Timeout = TimeSpan.FromMinutes(5);
        }

        public void Dispose() => http.Dispose();

        /// <summary>The latest (non-prerelease) GitHub release with a ValRally zip, or null if there's none yet.</summary>
        public async Task<ModRelease> LatestModRelease()
        {
            var url = $"https://api.github.com/repos/{LauncherConfig.GitHubRepo}/releases/latest";
            using var response = await http.GetAsync(url);
            if (response.StatusCode == HttpStatusCode.NotFound) return null;
            response.EnsureSuccessStatusCode();
            var json = Json.Parse(await response.Content.ReadAsStringAsync());

            var release = new ModRelease { Tag = json.Str("tag_name"), PageUrl = json.Str("html_url") };
            release.Version = ParseVersion(release.Tag);
            foreach (var asset in ((IEnumerable)json["assets"]).Cast<Dictionary<string, object>>())
            {
                var name = asset.Str("name") ?? "";
                var download = asset.Str("browser_download_url");
                if (name.Equals(LauncherConfig.LauncherAssetName, StringComparison.OrdinalIgnoreCase)) release.LauncherUrl = download;
                else if (name.StartsWith(LauncherConfig.ModZipPrefix, StringComparison.OrdinalIgnoreCase) && name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    release.ZipName = name;
                    release.ZipUrl = download;
                }
            }
            if (release.ZipUrl == null) return null;
            foreach (var asset in ((IEnumerable)json["assets"]).Cast<Dictionary<string, object>>())
            {
                if (string.Equals(asset.Str("name"), release.ZipName + ".sha256", StringComparison.OrdinalIgnoreCase))
                    release.Sha256Url = asset.Str("browser_download_url");
            }
            if (release.Version == null)
            {
                var fromName = Path.GetFileNameWithoutExtension(release.ZipName).Substring(LauncherConfig.ModZipPrefix.Length);
                release.Version = ParseVersion(fromName);
            }
            return release;
        }

        /// <summary>Downloads the release zip into the cache (checking its .sha256 if there is one) and returns its path.</summary>
        public async Task<string> DownloadModZip(ModRelease release)
        {
            var target = Path.Combine(paths.Cache, release.ZipName);
            string expected = null;
            if (release.Sha256Url != null)
            {
                var text = await http.GetStringAsync(release.Sha256Url);
                expected = text.Trim().Split(' ', '\t')[0].ToLowerInvariant();
            }
            if (File.Exists(target) && (expected == null || Sha256(target) == expected))
            {
                log($"Using cached {release.ZipName}");
                return target;
            }
            await Download(release.ZipUrl, target);
            if (expected != null && Sha256(target) != expected)
            {
                File.Delete(target);
                throw new InvalidDataException($"{release.ZipName} doesn't match its .sha256 file. Try again, or download it from {release.PageUrl}.");
            }
            return target;
        }

        /// <summary>Downloads a Thunderstore package at an exact version into the cache and returns its path.</summary>
        public async Task<string> DownloadPackage(PackageRef package)
        {
            var target = Path.Combine(paths.Cache, package.FullName + ".zip");
            if (File.Exists(target))
            {
                log($"Using cached {package.FullName}");
                return target;
            }
            var api = $"https://thunderstore.io/api/experimental/package/{package.Namespace}/{package.Name}/{package.Version}/";
            string url;
            try
            {
                url = Json.Parse(await http.GetStringAsync(api)).Str("download_url");
            }
            catch (HttpRequestException)
            {
                url = null;
            }
            url ??= $"https://thunderstore.io/package/download/{package.Namespace}/{package.Name}/{package.Version}/";
            await Download(url, target);
            return target;
        }

        private async Task Download(string url, string target)
        {
            log($"Downloading {Path.GetFileName(target)}");
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            var partial = target + ".part";
            using (var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                using var input = await response.Content.ReadAsStreamAsync();
                using var output = File.Create(partial);
                await input.CopyToAsync(output);
            }
            if (File.Exists(target)) File.Delete(target);
            File.Move(partial, target);
        }

        public static string Sha256(string file)
        {
            using var sha = SHA256.Create();
            using var stream = File.OpenRead(file);
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>"v0.2.0", "0.2.0" or "0.2" as a Version, or null.</summary>
        public static Version ParseVersion(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            text = text.Trim().TrimStart('v', 'V');
            var dash = text.IndexOfAny(new[] { '-', '+' });
            if (dash >= 0) text = text.Substring(0, dash);
            return Version.TryParse(text, out var version) ? version : null;
        }
    }
}
