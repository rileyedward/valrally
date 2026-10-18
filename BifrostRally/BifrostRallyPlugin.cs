using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace BifrostRally.Valheim
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    internal class BifrostRallyPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "com.riley.bifrostrally";
        public const string PluginName = "Bifrost Rally";
        public const string PluginVersion = "0.0.1";

        private void Awake()
        {
            Jotunn.Logger.LogInfo($"{PluginName} {PluginVersion} loaded. Unity {Application.unityVersion}");
            new Harmony(PluginGUID).PatchAll();
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
    internal static class PlayerOnSpawnedPatch
    {
        private static void Postfix(Player __instance)
        {
            Jotunn.Logger.LogInfo($"Player spawned at {__instance.transform.position}");
        }
    }
}
