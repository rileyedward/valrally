# The launcher

`ValRally-Launcher.exe` installs and starts ValRally without a mod manager. It's one small Windows exe (.NET Framework 4.8, which comes with Windows 10 and 11) built from `ValRally.Launcher/`, which doesn't reference the mod or the rally core.

## What it does

- **Finds Valheim** through Steam: Steam's folder from the registry, then every library in `steamapps\libraryfolders.vdf`, looking for app 892970. If Valheim isn't installed, the main button opens its Steam store page. **Change Valheim folder…** picks it by hand instead.
- **Install** downloads the latest ValRally release from GitHub (checking its `.sha256`), reads the zip's `manifest.json`, and downloads the exact BepInExPack_Valheim and Jotunn versions it pins from Thunderstore. Everything goes into its own profile, laid out the way r2modman does it.
- **Play** starts Valheim through Steam (`steam.exe -applaunch 892970 …`) with doorstop arguments that point at the profile's BepInEx, the same way r2modman starts a modded game.
- **Update** is enabled when GitHub has a newer ValRally than the installed one. It replaces the ValRally plugin folder and only downloads BepInEx or Jotunn again if the pinned versions changed. If the release also has a newer `ValRally-Launcher.exe`, the window says so (it doesn't replace itself).
- **Uninstall** removes the profile, the download cache and the launcher's files in the Valheim folder. It asks whether to keep `BepInEx\config`, which holds ValRally's settings, saved stages and tunes.

The GitHub repo it downloads from is `LauncherConfig.GitHubRepo` in `ValRally.Launcher/LauncherConfig.cs`, currently `rileyedward/valrally`. It only looks at the latest full release (not pre-releases), and expects assets named `ValRally-<version>.zip`, `ValRally-<version>.zip.sha256` and `ValRally-Launcher.exe`.

## Where things go

| Path | What |
|---|---|
| `%APPDATA%\ValRally\profile\` | BepInEx, `winhttp.dll`, `doorstop_config.ini`, and `BepInEx\plugins\ValRally` and `BepInEx\plugins\ValheimModding-Jotunn`. BepInEx's log is `profile\BepInEx\LogOutput.log` |
| `%APPDATA%\ValRally\cache\` | Downloaded zips |
| `%APPDATA%\ValRally\state.json` | Installed versions, a Valheim folder picked by hand, and the files added to the Valheim folder |
| `%APPDATA%\ValRally\launcher.log` | Everything the launcher did |
| `<Valheim>\winhttp.dll`, `<Valheim>\doorstop_config.ini` | Doorstop's loader and its config. The config has `enabled = false`, so starting Valheim from Steam is vanilla; only the launcher's Play switches it on |

Doorstop has to sit in the game folder to load at all, which is why those two files are the exception. If they were already there (from an r2modman launch, say), the launcher saves a copy in `%APPDATA%\ValRally\valheim-backup\` and Uninstall puts it back. If Valheim's folder has its own BepInEx (`<Valheim>\BepInEx\core`), the launcher leaves both files alone and relies on the launch arguments.

## Command line

The window covers normal use. The same steps also run without it, printing to the console and `launcher.log`:

```
ValRally-Launcher.exe status
ValRally-Launcher.exe install
ValRally-Launcher.exe update
ValRally-Launcher.exe play [--dry-run]
ValRally-Launcher.exe uninstall [--keep-config]
```

Options, for the window or a command:

| Option | What it does |
|---|---|
| `--zip <file>` | Install this ValRally zip instead of the GitHub release |
| `--home <folder>` | Use this folder instead of `%APPDATA%\ValRally` |
| `--valheim <folder>` | Use this Valheim folder for this run instead of finding it through Steam |
| `--dry-run` | With `play`: prepare the Valheim folder and print the Steam arguments without starting the game |

In PowerShell, run it with `Start-Process -Wait -NoNewWindow` so the prompt waits for it, e.g. `Start-Process .\ValRally-Launcher.exe -ArgumentList 'status' -Wait -NoNewWindow`.

## Testing a build before it's released

The launcher doesn't need a GitHub release to test: point it at the zip the release script just built.

1. Build: `powershell -ExecutionPolicy Bypass -File tools\release\build-release.ps1`. This writes `dist\ValRally-<version>.zip` and `dist\ValRally-Launcher.exe`.
2. Try it without touching anything real, using a scratch home and a fake Valheim folder:

    ```powershell
    mkdir $env:TEMP\vr-test\valheim; Set-Content $env:TEMP\vr-test\valheim\valheim.exe fake
    $o = "--home $env:TEMP\vr-test\home --valheim $env:TEMP\vr-test\valheim"
    Start-Process dist\ValRally-Launcher.exe -ArgumentList "install --zip dist\ValRally-0.2.0.zip $o" -Wait -NoNewWindow
    Start-Process dist\ValRally-Launcher.exe -ArgumentList "play --dry-run $o" -Wait -NoNewWindow
    Start-Process dist\ValRally-Launcher.exe -ArgumentList "uninstall $o" -Wait -NoNewWindow
    ```

    This downloads the real BepInEx and Jotunn from Thunderstore. Check `$env:TEMP\vr-test\home\profile` and the fake Valheim folder between steps.
3. For real, with Valheim closed: run `dist\ValRally-Launcher.exe --zip dist\ValRally-0.2.0.zip`, press **Install**, then **Play**. Load an offline world, run `rally_spawn` and press `F8`. `%APPDATA%\ValRally\profile\BepInEx\LogOutput.log` should show `ValRally 0.2.0 loaded`.
4. Close Valheim and start it from Steam: it should be vanilla, with no BepInEx console or log.
5. Run the launcher without `--zip`: it should show the latest GitHub release and offer no update while that's older than what's installed.

## If Play starts plain Valheim

Check `%APPDATA%\ValRally\launcher.log` for the Steam arguments, and whether `profile\BepInEx\LogOutput.log` was written. The doorstop arguments depend on the doorstop version in BepInExPack_Valheim: version 4 (in 5.4.2351) takes `--doorstop-enabled` and `--doorstop-target-assembly`, version 3 took `--doorstop-enable` and `--doorstop-target`. The launcher picks between them from the pack's `doorstop_config.ini`.
