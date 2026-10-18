# ValRally

Rally driving in Valheim, inspired by Art of Rally.

> **Early test build.** Getting in and out, the chase camera and the colour filter have been played in game. Handling, surfaces and rally stages are built and checked offline but haven't been driven in game yet, so expect rough edges.

## Features

- **Drivable rally car**: Raycast suspension, Pacejka tyres, weight transfer, handbrake turns and an automatic gearbox, with a speed, gear and revs HUD.
- **Art of Rally style view**: Getting in swings the camera into a tilted chase view and fades the world into a softer colour grade. Press `B` for a higher, wider view.
- **Surfaces from the ground**: Paved roads drive like tarmac, dirt like gravel, meadows like rough off-road, swamps like mud, mountains like snow and beaches like sand, with dust and crunch from Valheim's own footstep effects.
- **Rally stages you build**: Place a start, checkpoints and a finish with the hammer, then race them with a timer, splits, your best time, a ghost of your best run and pace notes.
- **Live tuning**: Change any handling setting from the console while you drive.

## Installation

### With the launcher

Download `ValRally-Launcher.exe` from the [GitHub releases](https://github.com/rileyedward/valrally/releases), run it and press **Install**, then **Play**. It installs BepInExPack_Valheim, Jotunn and ValRally into its own folder, so Valheim started from Steam stays vanilla.

### With a mod manager

Install **ValRally** with r2modman or the Thunderstore Mod Manager. BepInExPack_Valheim and Jotunn are installed for you.

### Manually

1. Install [BepInExPack_Valheim](https://valheim.thunderstore.io/package/denikson/BepInExPack_Valheim/) and [Jotunn](https://valheim.thunderstore.io/package/ValheimModding/Jotunn/).
2. Copy `ValRally.dll` and `ValRally.Core.dll` from the zip's `plugins` folder into `<Valheim>\BepInEx\plugins\ValRally\`.

## How to Play

1. Open the console with `F5` (if it doesn't open, add `-console` to Valheim's launch options in Steam) and run `rally_spawn` to put a car in front of you. The car isn't saved with the world, so spawn it again after loading.
2. Walk up to it and press `F8` to get in. Press `F8` again to get out.

| Key | Action |
|---|---|
| `F8` | Get in or out of the car (within 6 m) |
| `W` / `S` | Throttle / brake, and reverse once stopped |
| `A` / `D` | Steer |
| `Space` | Handbrake |
| `B` | Switch camera preset while driving |

To race, build a **Rally start**, some **Rally checkpoints** and a **Rally finish** from the hammer's Misc tab (1 wood each). Stop within 8 m of the start, then drive off to start the clock.

## Console Commands

| Command | What it does |
|---|---|
| `rally_spawn` / `rally_remove` | Spawn or remove the car |
| `rally_set <key> <value>` | Change a setting on the live car and save it, e.g. `rally_set Grip 0.8` |
| `rally_get [text]` | List settings whose name contains the text |
| `rally_save <name>` / `rally_load <name>` | Save or load a named tune |
| `rally_tyres pacejka\|simple` | Switch the tyre model |
| `rally_surface [hud]` | Show what each wheel is on and why |
| `rally_stage [forget]` | Show the current stage, its pace notes and best time, or forget the best time and ghost |
| `rally_test [compare]` | Run a scripted handling test on a flat platform |

## Configuration

Settings live in `BepInEx\config\com.riley.valrally.cfg`. The most useful switches:

| Setting | Default | What it does |
|---|---|---|
| `[Controls] DriveKey` | `F8` | Key to get in or out |
| `[Controls] CameraKey` | `B` | Key to switch camera preset |
| `[Controls] ShowHud` | `true` | Speed, gear and revs on screen |
| `[Camera] Preset` | `Close` | `Close` or `High` |
| `[Car] TractionControl` | `true` | Traction control |
| `[Surfaces] Enabled` | `true` | Grip changes with the ground |
| `[Effects] Particles`, `Sounds` | `true` | Dust and crunch at the wheels |
| `[Stages] Enabled` | `true` | Rally pieces in the hammer menu |
| `[Stages] Ghost`, `PaceNotes` | `true` | Ghost of your best run, pace note calls |

Best times and ghosts are saved per world in `BepInEx\config\valrally-stages\`.

## Known Issues

- Single player only for now. The car isn't synced, so other players won't see it.
- No engine sound and no real car model yet; the car is a box.

## Credits

Built with [BepInEx](https://github.com/BepInEx/BepInEx), [Harmony](https://github.com/pardeike/Harmony) and [Jotunn](https://github.com/Valheim-Modding/Jotunn). Inspired by Art of Rally by Funselektor Labs. ValRally is not affiliated with or endorsed by Funselektor Labs or Iron Gate, and contains no code or assets from either game.
