# Releasing ValRally

Releases are built locally, because the plugin compiles against Valheim's own DLLs, which can't be put in a repo or a CI runner.

1. Bump the version in `ValRally/ValRallyPlugin.cs` (`PluginVersion`), `ValRally/Package/manifest.json` (`version_number`) and `ValRally.Launcher/ValRally.Launcher.csproj` (`<Version>`), and add an entry to `ValRally/CHANGELOG.md`. The script stops if the versions differ.
2. `powershell -ExecutionPolicy Bypass -File tools\release\build-release.ps1`
   - builds the solution in Release and runs the offline sim (`ValRally.Sim`), stopping if any check fails;
   - writes `dist\ValRally-<version>.zip` in Thunderstore's layout, with a `.sha256` next to it;
   - copies the launcher to `dist\ValRally-Launcher.exe`, with its own `.sha256`;
   - copies all four files into your Downloads folder.
3. Smoke test the launcher with the new zip before publishing, as in [docs/launcher.md](launcher.md#testing-a-build-before-its-released). Or import the zip into an empty r2modman profile (Settings → Import local mod), start Valheim from it in an offline world, run `rally_spawn`, press `F8` and drive. `BepInEx\LogOutput.log` should show `ValRally <version> loaded`.
4. Publish:
   - GitHub: on the repo's Releases page, Draft a new release, tag `v<version>`, and drag `ValRally-<version>.zip`, `ValRally-Launcher.exe` and both `.sha256` files from Downloads onto it. Or from the repo folder: `gh release create v<version> dist\ValRally-<version>.zip dist\ValRally-<version>.zip.sha256 dist\ValRally-Launcher.exe dist\ValRally-Launcher.exe.sha256 --title "ValRally <version>" --notes "..."`. Don't mark it as a pre-release: the launcher only looks at the latest full release. Keep the launcher's file name the same every time, so the README's download link keeps working.
   - Thunderstore: upload the same zip at <https://thunderstore.io/package/create/> under the Valheim community. The launcher isn't uploaded there.

## What's in the zip, and what isn't

| in the zip | not in the zip |
|---|---|
| `manifest.json`, `README.md`, `CHANGELOG.md`, `icon.png`, `plugins\ValRally.dll`, `plugins\ValRally.Core.dll` | BepInExPack_Valheim and Jotunn (dependencies the mod manager installs), Valheim itself |

Never in the zip or the repo: game DLLs, publicized assemblies, decompiled code, or anything from Art of Rally.

## First push to GitHub

Once, to put the repo on GitHub:

1. On <https://github.com/new>, create an empty repository named `ValRally` (no README, licence or .gitignore, since the repo already has them).
2. From the repo folder:

    ```bash
    git remote add origin https://github.com/<your-user>/ValRally.git
    git push -u origin main
    ```

   Only `main` is pushed. The local `m3`, `m4` and `m5` branches stay local unless you push them too.
3. Set `website_url` in `ValRally/Package/manifest.json` to the repo URL and commit it, then make the first release as above.

Commits use the email in your git config. If you'd rather not publish it, set `git config user.email <id>+<user>@users.noreply.github.com` (from GitHub → Settings → Emails) before pushing; existing commits keep the old one.

## The old name

The mod was called Bifrost Rally before 0.1.0. The plugin GUID changed from `com.riley.bifrostrally` to `com.riley.valrally`, so BepInEx treats it as a new mod. On first start the plugin copies `BepInEx\config\com.riley.bifrostrally.cfg` to `com.riley.valrally.cfg` (the old file is left alone) and moves `bifrostrally-stages` and `bifrostrally-tunes` to `valrally-stages` and `valrally-tunes`. Delete the old `BepInEx\plugins\BifrostRally\` folder, or both plugins load. Rally pieces were renamed too (`valrally_start` and so on), so pieces placed with the old build don't load.
