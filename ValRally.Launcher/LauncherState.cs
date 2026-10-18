using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace ValRally.Launcher
{
    /// <summary>A file the launcher put in the Valheim folder, so uninstall can take it out again.</summary>
    public sealed class GameFile
    {
        public string Name { get; set; }
        /// <summary>SHA-256 of what the launcher wrote; the file is only removed if it still matches.</summary>
        public string Sha256 { get; set; }
        /// <summary>True if the launcher's copy replaced an existing file, which was saved to the backup folder.</summary>
        public bool BackedUp { get; set; }
        /// <summary>True if an identical file was already there, so uninstall leaves it.</summary>
        public bool WasThere { get; set; }
    }

    /// <summary>state.json: what's installed in the profile.</summary>
    public sealed class LauncherState
    {
        /// <summary>Valheim folder picked by hand; empty means find it through Steam.</summary>
        public string ValheimPath { get; set; }
        /// <summary>Installed ValRally version, or empty.</summary>
        public string ModVersion { get; set; }
        /// <summary>Thunderstore dependencies installed, "namespace-name" to version.</summary>
        public Dictionary<string, string> Packages { get; set; } = new Dictionary<string, string>();
        /// <summary>Valheim folder the game files below were put in.</summary>
        public string GameFilesDir { get; set; }
        public List<GameFile> GameFiles { get; set; } = new List<GameFile>();

        [ScriptIgnore]
        public bool IsInstalled => !string.IsNullOrEmpty(ModVersion);

        public static LauncherState Load(string path)
        {
            if (!File.Exists(path)) return new LauncherState();
            try
            {
                var state = Json.Serializer.Deserialize<LauncherState>(File.ReadAllText(path));
                state.Packages ??= new Dictionary<string, string>();
                state.GameFiles ??= new List<GameFile>();
                return state;
            }
            catch
            {
                return new LauncherState();
            }
        }

        public void Save(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, Json.Serializer.Serialize(this), new UTF8Encoding(false));
        }
    }

    internal static class Json
    {
        public static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer { MaxJsonLength = 64 * 1024 * 1024 };

        public static Dictionary<string, object> Parse(string text)
        {
            return Serializer.Deserialize<Dictionary<string, object>>(text.TrimStart('﻿'));
        }

        public static string Str(this Dictionary<string, object> obj, string key)
        {
            return obj != null && obj.TryGetValue(key, out var value) ? value as string : null;
        }
    }
}
