# ValRally

Rally driving in Valheim, inspired by Art of Rally.

## Overview

### What is ValRally?

ValRally is a Valheim mod that brings Art of Rally style driving into Valheim's world. Spawn a rally car, walk up to it and get in: the camera swings into a tilted chase view, the world fades into a softer colour grade, and the car drives with weight transfer, grip that changes with the ground under each wheel, and handbrake slides. Build a start, checkpoints and a finish with the hammer and race your own stages against a timer, your best splits and a ghost of your best run, with pace notes called out as you go.

The car physics, camera, surfaces and stage timing live in a rally core that doesn't depend on Valheim, so the driving can be tuned and tested on its own.

ValRally is not produced, approved or endorsed by Funselektor Labs or Iron Gate. It ships no code or assets from Art of Rally or Valheim.

> **Status: early test build.** Getting in and out, the chase camera and the colour filter (M0 to M2) have been played in game. Handling, surfaces, rally pieces, ghosts and pace notes (M3 to M6) build and pass the offline sim but haven't been driven in game yet. See [docs/status.md](docs/status.md).

### Key Features

- **Rally Car**: Raycast suspension, Pacejka tyres with a friction ellipse, wheel spin, an automatic gearbox, traction control and a speed, gear and revs HUD.
- **Art of Rally View**: A tilted chase camera with a close and a high preset, and a colour filter that blends in when you get in and out when you leave.
- **Surfaces From the Ground**: Paved road → tarmac, dirt → gravel, meadow → off-road, swamp → mud, mountain → snow, beach → sand, plus stone, wood and more for buildings. Each has its own grip and drag, with dust and crunch borrowed from Valheim's footstep effects.
- **Rally Stages**: Buildable start, checkpoint and finish pieces, a timer with splits against your best, a ghost of your best run and generated pace notes.
- **Live Tuning**: Change any handling setting from the console while driving, and save and load named tunes.

## Getting Started

### Prerequisites

1. **Valheim** on Steam (Windows). ValRally targets Valheim **1.0.20** (network version 41). After a big Valheim update it may break until it's updated.
2. **BepInExPack_Valheim** 5.4.2351 and **Jotunn** 2.30.2. A mod manager installs both for you.

### Installation

**With the launcher (easiest):**

1. Download [`ValRally-Launcher.exe`](https://github.com/rileyedward/valrally/releases/latest/download/ValRally-Launcher.exe) from the [Releases](../../releases) page and run it. Windows may warn that it's from an unknown publisher, because the exe isn't signed: choose More info, then Run anyway.
2. Press **Install**. The launcher finds Valheim through Steam (or opens its Steam page if you don't have it), then downloads BepInExPack_Valheim and Jotunn from Thunderstore and the latest ValRally from GitHub.
3. Press **Play**.

The launcher installs everything into its own folder, `%APPDATA%\ValRally`, so Valheim started from Steam stays vanilla. **Update** appears when there's a newer ValRally on GitHub, and **Uninstall** removes it all again (it asks whether to keep your settings, stages and tunes). See [docs/launcher.md](docs/launcher.md) for how it works.

**With a mod manager:**

1. Install [r2modman](https://thunderstore.io/package/ebkr/r2modman/) and pick Valheim.
2. Download `ValRally-<version>.zip` from the [Releases](../../releases) page.
3. In r2modman go to Settings → Import local mod and pick the zip. BepInExPack_Valheim and Jotunn are installed as dependencies.
4. Press **Start modded**.

**Manually:**

1. Install [BepInExPack_Valheim](https://valheim.thunderstore.io/package/denikson/BepInExPack_Valheim/) into your Valheim folder and start the game once.
2. Install [Jotunn](https://valheim.thunderstore.io/package/ValheimModding/Jotunn/) by copying `Jotunn.dll` into `<Valheim>\BepInEx\plugins\`.
3. Extract the release zip and copy `ValRally.dll` and `ValRally.Core.dll` from its `plugins` folder into `<Valheim>\BepInEx\plugins\ValRally\`.

To play plain Valheim again with the launcher, start Valheim from Steam as usual. With r2modman, disable the mod; with a manual install, delete the `ValRally` folder.

**Coming from Bifrost Rally** (the mod's name before 0.1.0): delete the old `<Valheim>\BepInEx\plugins\BifrostRally\` folder so only ValRally loads. On first start ValRally copies your settings from `com.riley.bifrostrally.cfg` and moves your saved stages and tunes to the new folders. Rally pieces placed with the old build won't load, so place them again.

Use ValRally in single player or on your own server. The car isn't synced over the network yet, so other players won't see it.

### Controls

Open the console with `F5` (if it doesn't open, add `-console` to Valheim's launch options in Steam) and run `rally_spawn` to put a car in front of you. The car isn't saved with the world, so spawn it again after loading.

| Key | Action |
|---|---|
| `F8` | Get in or out of the car (within 6 m) |
| `W` / `S` | Throttle / brake, and reverse once stopped |
| `A` / `D` | Steer |
| `Space` | Handbrake |
| `B` | Switch between the close and high camera while driving |

Steering, throttle and brake follow Valheim's own movement bindings, and the handbrake follows Jump.

To race a stage, build a **Rally start**, some **Rally checkpoints** and a **Rally finish** from the hammer's Misc tab (1 wood each). The route chains from the start to the nearest checkpoint, then the next nearest, and ends at the finish. Stop within 8 m of the start to arm it; the clock starts when you roll.

### Console Commands

| Command | What it does |
|---|---|
| `rally_spawn` / `rally_remove` | Spawn the car in front of you (re-reading the config) or remove it |
| `rally_set <key> <value>` | Change a setting on the live car and save it, e.g. `rally_set Grip 0.8` or `rally_set Feel.SteerYawTorque 3000` |
| `rally_get [text]` | List settings whose name contains the text |
| `rally_save <name>` / `rally_load <name>` | Save the current settings as a named tune, or load one |
| `rally_tyres pacejka\|simple` | Switch the tyre model on the current car |
| `rally_surface [hud]` | Print the surface under each wheel and why; `hud` toggles a live readout |
| `rally_stage [forget]` | Show the current stage, its pace notes and best time; `forget` deletes the best time and ghost |
| `rally_test [compare]` | Scripted handling test on a flat platform; `compare` runs it with the feel layer off, then on |

### Configuration

Settings are in `BepInEx\config\com.riley.valrally.cfg`, created on first run. Every value can also be changed in game with `rally_set`. The main switches:

| Setting | Default | What it does |
|---|---|---|
| `[Controls] DriveKey` | `F8` | Get in or out |
| `[Controls] CameraKey` | `B` | Switch camera preset |
| `[Controls] ShowHud` | `true` | Speed, gear and revs on screen |
| `[Camera] Preset` | `Close` | Camera used when you get in (`Close` or `High`); saved when you switch |
| `[Camera] BlendTime` | `1.2` | Seconds to blend the camera and colour filter in or out |
| `[Car] TractionControl` | `true` | Traction control |
| `[Car] PacejkaTyres` | `true` | Pacejka tyre model; `false` uses the simple one |
| `[Feel] Enabled` | `true` | Arcade helpers on top of the physics (steering yaw, slide help, air levelling) |
| `[Surfaces] Enabled` | `true` | Grip changes with the ground |
| `[Effects] Particles` / `Sounds` | `true` | Dust and crunch at the wheels |
| `[Stages] Enabled` | `true` | Rally pieces in the hammer menu |
| `[Stages] Ghost` / `PaceNotes` | `true` | Ghost of your best run, pace note calls |

The rest of the file holds the car's physics (`[Car]`, `[Feel]`), both camera presets (`[Camera Close]`, `[Camera High]`), the colour filter (`[Filter]`), each surface's grip (`[Surface <Name>]`) and stage timing (`[Stage Timing]`). Best times and ghosts are saved per world in `BepInEx\config\valrally-stages\`.

## Development

### Prerequisites

1. **.NET SDK** (8+) builds the mod. Check it by running:

    ```bash
    dotnet --version
    ```

2. **Valheim** with **BepInExPack_Valheim** installed in the game folder: the mod compiles against the game's own assemblies, which are never copied into the repository. [Jotunn](https://github.com/Valheim-Modding/Jotunn) comes from NuGet (`JotunnLib`) and generates publicized Valheim assemblies on the first build.

### Installation

1. Point the build at your Valheim install. Copy the example file and fix the path if your Steam library is elsewhere:

    ```bash
    copy Environment.props.example Environment.props
    ```

2. Build in Debug. This copies `ValRally.dll` and `ValRally.Core.dll` into `<Valheim>\BepInEx\plugins\ValRally` (or `MOD_DEPLOYPATH` from `Environment.props`). Close Valheim first:

    ```bash
    dotnet build -c Debug
    ```

3. Start Valheim, load an offline world and run `rally_spawn`.

### Project Structure

| Folder | What it is |
|---|---|
| `ValRally.Core/` | The rally core: raycast car, tyres, drivetrain, feel layer, chase camera, colour filter, surfaces, stages, ghost and pace notes. Only references UnityEngine |
| `ValRally/` | The Valheim plugin (BepInEx, Jotunn, Harmony): driving, input, ground to surface, effects, rally pieces, HUD and console commands. `Package/` holds the Thunderstore manifest and icon |
| `ValRally.Sim/` | Offline handling checks and stage-logic tests for the core. Runs outside the game |
| `ValRally.Launcher/` | The Windows launcher (`ValRally-Launcher.exe`): finds Valheim, installs BepInEx, Jotunn and ValRally into its own profile, and plays, updates and uninstalls. Standalone; doesn't reference the mod or the core |
| `scripts/` | Post-build deploy scripts (from the Jotunn mod stub) |
| `libraries/` | `pdb2mdb.exe`, used to make Mono debug symbols for Debug builds |
| `tools/release/` | The release build script |
| `docs/` | [Status and decisions](docs/status.md), [releasing](docs/releasing.md) and [the launcher](docs/launcher.md) |

## How It Works

The car is a Unity rigidbody with four raycast wheels. Each physics step the plugin fills in `CarInput` from Valheim's controls and asks a surface provider what each wheel is on; the core turns that into suspension, tyre and drivetrain forces:

- **Suspension**: Each wheel casts a ray down, and a spring and damper push the body up. A raised centre of mass gives real weight transfer under braking, throttle and cornering.
- **Tyres**: A Pacejka curve with relaxation length and a friction ellipse, scaled per surface.
- **Feel layer**: Arcade helpers on top of the physics, in the spirit of Art of Rally: steering yaw that fades out in big slides, slide help, air levelling and soft landings. Each can be switched off.
- **Surfaces**: Valheim's terrain paint (paved, dirt, cultivated) comes first, then biome and slope, then a sea-level guess for sand. Built pieces use their material.
- **Stages**: The start, checkpoints and finish are radius gates around each piece. Best times and ghosts are saved per world and start position.

The core never touches Valheim. Only the plugin knows about Valheim's player, input, terrain and pieces, so the core can be tested in the sim.

## Releasing

Releases are built locally, because the plugin compiles against Valheim's assemblies:

```bash
powershell -ExecutionPolicy Bypass -File tools\release\build-release.ps1
```

This builds the solution, runs the sim and writes `dist\ValRally-<version>.zip` (Thunderstore layout) and `dist\ValRally-Launcher.exe`, each with a `.sha256` file next to it, then copies all four into your Downloads folder so you can drag them onto a GitHub release. See [docs/releasing.md](docs/releasing.md) for the smoke test and how to publish.

## Testing

Build everything and run the offline sim, which checks launch, step steer, handbrake turns and braking in a turn on each surface, plus the stage timer, splits, pace notes and ghosts:

```bash
dotnet build -c Release
ValRally.Sim\bin\Release\net48\ValRally.Sim.exe
```

In game, `rally_test compare` runs a scripted handling test on a flat platform and logs the results to `BepInEx\LogOutput.log`.

## Data and Licensing

ValRally is released under the [MIT License](LICENSE).

It contains no Art of Rally or Valheim code or assets. Handling was tuned by studying how Art of Rally drives and how its car code is put together. None of that code, and no decompiled output, is in this repository or the release, and nothing from Art of Rally is loaded at runtime. Particles and sounds are Valheim's own, used in the running game through its footstep effects; nothing is copied out.

Built with [BepInEx](https://github.com/BepInEx/BepInEx) (LGPL-2.1), [Harmony](https://github.com/pardeike/Harmony) (MIT) and [Jotunn](https://github.com/Valheim-Modding/Jotunn) (MIT), which are installed separately and not included in the release. The build scripts and `pdb2mdb.exe` come from the [Jotunn mod stub](https://github.com/Valheim-Modding/JotunnModStub) (MIT No Attribution).

Art of Rally is a trademark of Funselektor Labs. Valheim is a trademark of Iron Gate AB.
