using BepInEx;
using HarmonyLib;
using Jotunn.Managers;
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

        private CarConfig _carConfig;

        private void Awake()
        {
            Jotunn.Logger.LogInfo($"{PluginName} {PluginVersion} loaded. Unity {Application.unityVersion}");
            _carConfig = new CarConfig(Config);
            RallyView.Configure(_carConfig.GetCamera(_carConfig.Preset.Value), _carConfig.Get<Core.ColorFilterSettings>(CarConfig.FilterSection));
            LiveTuning.ApplySurfaces(_carConfig);
            LiveTuning.ApplyStages(_carConfig);
            if (StageRunner.Options.Enabled)
            {
                RallyPieces.Register();
            }
            CommandManager.Instance.AddConsoleCommand(new StageCommand());
            CommandManager.Instance.AddConsoleCommand(new SpawnCarCommand(_carConfig));
            CommandManager.Instance.AddConsoleCommand(new SurfaceCommand());
            CommandManager.Instance.AddConsoleCommand(new RemoveCarCommand());
            CommandManager.Instance.AddConsoleCommand(new TyresCommand(_carConfig));
            CommandManager.Instance.AddConsoleCommand(new SetCommand(_carConfig));
            CommandManager.Instance.AddConsoleCommand(new GetCommand(_carConfig));
            CommandManager.Instance.AddConsoleCommand(new SaveCommand(_carConfig));
            CommandManager.Instance.AddConsoleCommand(new LoadCommand(_carConfig));
            CommandManager.Instance.AddConsoleCommand(new TestCommand(this));
            new Harmony(PluginGUID).PatchAll();
        }

        private void OnGUI()
        {
            RallyHud.Draw(_carConfig.ShowHud.Value);
        }

        private void Update()
        {
            RallyView.BlendTime = _carConfig.BlendTime.Value;
            RallyDriver.Update(_carConfig.DriveKey.Value);
            SurfaceCommand.UpdateHud();
            StageRunner.Update();

            if (RallyDriver.IsDriving && !RallyDriver.IsTyping() && ZInput.GetKeyDown(_carConfig.CameraKey.Value))
            {
                // Saved straight away, so the next drive starts on the same preset.
                CameraPreset next = _carConfig.Preset.Value == CameraPreset.Close ? CameraPreset.High : CameraPreset.Close;
                _carConfig.Preset.Value = next;
                RallyView.SetCamera(_carConfig.GetCamera(next), smooth: true);
                Jotunn.Logger.LogInfo($"Camera preset: {next}");
            }
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

    /// <summary>
    /// While driving, WASD and space go to the car, so the Viking gets no movement.
    /// Moving would also detach the player from the seat.
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.SetControls))]
    internal static class PlayerSetControlsPatch
    {
        private static void Prefix(Player __instance, ref Vector3 movedir, ref bool attack, ref bool attackHold,
            ref bool secondaryAttack, ref bool secondaryAttackHold, ref bool block, ref bool blockHold,
            ref bool jump, ref bool crouch, ref bool autoRun, ref bool dodge)
        {
            if (!RallyDriver.IsDriving || __instance != Player.m_localPlayer)
            {
                return;
            }
            movedir = Vector3.zero;
            attack = attackHold = secondaryAttack = secondaryAttackHold = false;
            block = blockHold = jump = crouch = autoRun = dodge = false;
        }
    }
}
