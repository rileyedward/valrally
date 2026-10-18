# Releasing Bifrost Rally

Releases are built locally, because the plugin compiles against Valheim's own DLLs, which can't be put in a repo or a CI runner.

1. Bump the version in `BifrostRally/BifrostRallyPlugin.cs` (`PluginVersion`) and `BifrostRally/Package/manifest.json` (`version_number`), and add an entry to `BifrostRally/CHANGELOG.md`. The script stops if the two versions differ.
2. `powershell -ExecutionPolicy Bypass -File tools\release\build-release.ps1`
   - builds the solution in Release and runs the offline sim (`BifrostRally.Sim`), stopping if any check fails;
   - writes `dist\BifrostRally-<version>.zip` in Thunderstore's layout, with a `.sha256` next to it.
3. Smoke test: import the zip into an empty r2modman profile (Settings → Import local mod), start Valheim from it in an offline world, run `rally_spawn`, press `F8` and drive. `BepInEx\LogOutput.log` should show `Bifrost Rally <version> loaded`.
4. Publish:
   - GitHub: `gh release create v<version> dist\BifrostRally-<version>.zip dist\BifrostRally-<version>.zip.sha256 --title "Bifrost Rally <version>" --notes "..."`
   - Thunderstore: upload the same zip at <https://thunderstore.io/package/create/> under the Valheim community. Fill in `website_url` in the manifest first once the GitHub repo exists.

## What's in the zip, and what isn't

| in the zip | not in the zip |
|---|---|
| `manifest.json`, `README.md`, `CHANGELOG.md`, `icon.png`, `plugins\BifrostRally.dll`, `plugins\BifrostRally.Core.dll` | BepInExPack_Valheim and Jotunn (dependencies the mod manager installs), Valheim itself |

Never in the zip or the repo: game DLLs, publicized assemblies, decompiled code, or anything from Art of Rally.
