using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using BifrostRally.Core;
using UnityEngine;

namespace BifrostRally.Valheim
{
    /// <summary>
    /// Exposes every CarSettings field as a BepInEx config entry under [Car].
    /// </summary>
    internal class CarConfig
    {
        private readonly ConfigFile _config;
        private readonly Dictionary<FieldInfo, ConfigEntry<float>> _entries = new Dictionary<FieldInfo, ConfigEntry<float>>();

        public ConfigEntry<KeyCode> DriveKey { get; }

        public CarConfig(ConfigFile config)
        {
            _config = config;
            DriveKey = config.Bind("Controls", "DriveKey", KeyCode.F8, "Get in or out of the nearest rally car.");

            var defaults = new CarSettings();
            foreach (FieldInfo field in typeof(CarSettings).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (field.FieldType == typeof(float))
                {
                    _entries[field] = config.Bind("Car", field.Name, (float)field.GetValue(defaults));
                }
            }
        }

        /// <summary>Re-reads the config file and returns fresh settings.</summary>
        public CarSettings Load()
        {
            _config.Reload();
            var settings = new CarSettings();
            foreach (KeyValuePair<FieldInfo, ConfigEntry<float>> entry in _entries)
            {
                entry.Key.SetValue(settings, entry.Value.Value);
            }
            return settings;
        }
    }
}
