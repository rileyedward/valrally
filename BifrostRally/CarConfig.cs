using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using BifrostRally.Core;
using UnityEngine;

namespace BifrostRally.Valheim
{
    internal enum CameraPreset
    {
        Close,
        High,
    }

    /// <summary>
    /// Exposes the float and bool fields of the core settings classes as BepInEx config entries:
    /// [Car], [Camera Close], [Camera High] and [Filter].
    /// </summary>
    internal class CarConfig
    {
        public const string CarSection = "Car";
        public const string FilterSection = "Filter";
        public const string FeelSection = "Feel";

        private readonly ConfigFile _config;
        private readonly Dictionary<string, Dictionary<FieldInfo, ConfigEntryBase>> _sections =
            new Dictionary<string, Dictionary<FieldInfo, ConfigEntryBase>>();

        public ConfigEntry<KeyCode> DriveKey { get; }
        public ConfigEntry<KeyCode> CameraKey { get; }
        public ConfigEntry<CameraPreset> Preset { get; }
        public ConfigEntry<float> BlendTime { get; }
        public ConfigEntry<bool> ShowHud { get; }

        public string FilePath => _config.ConfigFilePath;

        public CarConfig(ConfigFile config)
        {
            _config = config;
            DriveKey = config.Bind("Controls", "DriveKey", KeyCode.F8, "Get in or out of the nearest rally car.");
            CameraKey = config.Bind("Controls", "CameraKey", KeyCode.B, "Switch camera preset while driving.");
            Preset = config.Bind("Camera", "Preset", CameraPreset.Close, "Camera preset used when you get in. Saved when you switch.");
            BlendTime = config.Bind("Camera", "BlendTime", 1.2f, "Seconds to blend the camera and colour filter in or out.");
            ShowHud = config.Bind("Controls", "ShowHud", true, "Show speed, gear and revs while driving.");
            BindFields(CarSection, new CarSettings());
            BindFields(FeelSection, new FeelSettings());
            BindFields(SectionFor(CameraPreset.Close), ChaseCameraSettings.Close());
            BindFields(SectionFor(CameraPreset.High), ChaseCameraSettings.High());
            BindFields(FilterSection, new ColorFilterSettings());
        }

        public static string SectionFor(CameraPreset preset) => "Camera " + preset;

        /// <summary>Re-reads the config file, so edits apply on the next rally_spawn.</summary>
        public void Reload()
        {
            _config.Reload();
        }

        public T Get<T>(string section) where T : new()
        {
            var settings = new T();
            foreach (KeyValuePair<FieldInfo, ConfigEntryBase> entry in _sections[section])
            {
                entry.Key.SetValue(settings, entry.Value.BoxedValue);
            }
            return settings;
        }

        /// <summary>Changes one setting and saves it to the config file.</summary>
        public void Set(string section, string name, object value)
        {
            foreach (KeyValuePair<FieldInfo, ConfigEntryBase> entry in _sections[section])
            {
                if (entry.Key.Name == name)
                {
                    entry.Value.BoxedValue = value;
                }
            }
        }

        /// <summary>
        /// Finds a setting by "Section.Key" or just "Key". A bare key is looked up in [Car],
        /// [Feel], the current camera preset, then [Filter].
        /// </summary>
        public bool TryFind(string key, out string section, out FieldInfo field, out ConfigEntryBase entry)
        {
            string wantedSection = null;
            int dot = key.LastIndexOf('.');
            if (dot > 0)
            {
                wantedSection = key.Substring(0, dot);
                key = key.Substring(dot + 1);
            }

            var order = new List<string> { CarSection, FeelSection, SectionFor(Preset.Value), FilterSection };
            foreach (string name in _sections.Keys)
            {
                if (!order.Contains(name))
                {
                    order.Add(name);
                }
            }

            foreach (string name in order)
            {
                if (wantedSection != null && !string.Equals(name, wantedSection, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                foreach (KeyValuePair<FieldInfo, ConfigEntryBase> pair in _sections[name])
                {
                    if (string.Equals(pair.Key.Name, key, System.StringComparison.OrdinalIgnoreCase))
                    {
                        section = name;
                        field = pair.Key;
                        entry = pair.Value;
                        return true;
                    }
                }
            }
            section = null;
            field = null;
            entry = null;
            return false;
        }

        /// <summary>All settings whose name contains the text, for rally_get.</summary>
        public IEnumerable<string> Describe(string contains)
        {
            foreach (KeyValuePair<string, Dictionary<FieldInfo, ConfigEntryBase>> section in _sections)
            {
                foreach (KeyValuePair<FieldInfo, ConfigEntryBase> pair in section.Value)
                {
                    string name = section.Key + "." + pair.Key.Name;
                    if (string.IsNullOrEmpty(contains) || name.IndexOf(contains, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        yield return name + " = " + pair.Value.GetSerializedValue();
                    }
                }
            }
        }

        public ChaseCameraSettings GetCamera(CameraPreset preset) => Get<ChaseCameraSettings>(SectionFor(preset));

        private void BindFields(string section, object defaults)
        {
            var entries = new Dictionary<FieldInfo, ConfigEntryBase>();
            foreach (FieldInfo field in defaults.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (field.FieldType == typeof(float))
                {
                    entries[field] = _config.Bind(section, field.Name, (float)field.GetValue(defaults));
                }
                else if (field.FieldType == typeof(bool))
                {
                    entries[field] = _config.Bind(section, field.Name, (bool)field.GetValue(defaults));
                }
            }
            _sections[section] = entries;
        }
    }
}
