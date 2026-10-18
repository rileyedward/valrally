using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using BifrostRally.Core;
using UnityEngine;

namespace BifrostRally.Valheim
{
    /// <summary>
    /// Exposes the float fields of the core settings classes as BepInEx config entries:
    /// [Car] for CarSettings, [Camera] for ChaseCameraSettings, [Filter] for ColorFilterSettings.
    /// </summary>
    internal class CarConfig
    {
        private readonly ConfigFile _config;
        private readonly Dictionary<Type, Dictionary<FieldInfo, ConfigEntry<float>>> _entries =
            new Dictionary<Type, Dictionary<FieldInfo, ConfigEntry<float>>>();

        public ConfigEntry<KeyCode> DriveKey { get; }
        public ConfigEntry<float> BlendTime { get; }

        public CarConfig(ConfigFile config)
        {
            _config = config;
            DriveKey = config.Bind("Controls", "DriveKey", KeyCode.F8, "Get in or out of the nearest rally car.");
            BlendTime = config.Bind("Camera", "BlendTime", 1.2f, "Seconds to blend the camera and colour filter in or out.");
            BindFields<CarSettings>("Car");
            BindFields<ChaseCameraSettings>("Camera");
            BindFields<ColorFilterSettings>("Filter");
        }

        /// <summary>Re-reads the config file, so edits apply on the next rally_spawn.</summary>
        public void Reload()
        {
            _config.Reload();
        }

        public T Get<T>() where T : new()
        {
            var settings = new T();
            foreach (KeyValuePair<FieldInfo, ConfigEntry<float>> entry in _entries[typeof(T)])
            {
                entry.Key.SetValue(settings, entry.Value.Value);
            }
            return settings;
        }

        private void BindFields<T>(string section) where T : new()
        {
            var defaults = new T();
            var entries = new Dictionary<FieldInfo, ConfigEntry<float>>();
            foreach (FieldInfo field in typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (field.FieldType == typeof(float))
                {
                    entries[field] = _config.Bind(section, field.Name, (float)field.GetValue(defaults));
                }
            }
            _entries[typeof(T)] = entries;
        }
    }
}
