# Where things stand

Targets Valheim 1.0.20, BepInExPack 5.4.2351, Jotunn 2.30.2 (see README).

## Done

- M0 setup, M1 drivable box, M2 toggle (chase camera with Close/High presets, colour filter).
- M3 step 1: Pacejka tyres with slip relaxation and a friction ellipse (`rally_tyres pacejka|simple`).
- M3 step 2: wheel spin and drivetrain (Core `WheelDynamics`, `Drivetrain`), on by default.
  - Every wheel has its own spin; slip ratio drives forward force through combined slip.
  - Engine torque curve, soft rev limiter, automatic 5-speed that shifts on road speed, slipping clutch for launches.
  - Brakes and handbrake lock wheels at zero spin.
  - AWD split `RearTorqueShare` (0 = FWD, 1 = RWD, default 0.65) and `DiffLock`.
  - Turn it off with `rally_set WheelSpin false` to go back to step 1.
- M3 step 3: feel layer (Core `CarFeel`, config `[Feel]`), on by default.
  - Steering yaw, slide stability, lower CoM, inertia ×1.5, air levelling, soft landings and downforce.
  - Turn it off with `rally_set Feel.Enabled false`.
- Tuning console:
  - `rally_set <key> <value>` changes the live car and saves.
  - `rally_get [text]` lists settings.
  - `rally_save <name>` and `rally_load <name>` keep named tunes.
- `rally_test [compare]`: a scripted launch, step steer @60, handbrake turn @50, brake-in-turn @70 and 3 m jump on a flat platform 150 m up. Results go to `BepInEx/LogOutput.log`.

## Not yet verified in game

Everything in steps 2 and 3 builds, and an offline straight-line check of the wheel and engine maths passes (stable spin, no gear hunting). The offline numbers for the defaults:

| Test | Result |
|---|---|
| 0–100 | 3.6 s |
| Top speed | about 206 km/h |
| 100–0 braking | 42 m |
| Handbrake | locks the rear |

Steering, the feel layer and `rally_test` have **not** run in Valheim yet. All feel and drivetrain values are self-tuned first guesses.

## Where I stopped

- `rally_test` is written and builds, but has never run, so its numbers haven't been used for tuning yet.
- No speed/gear/rpm HUD yet (there's a `ShowHud` config flag, but nothing draws it).

## Next 3 steps

1. Riley drives the new build. Run `rally_test compare` once in Flashy while seated in the car, then quit, so the log has numbers with the feel layer off and on.
2. Tune from those numbers and Riley's notes:
   - yaw response
   - handbrake rotation and recovery
   - landing settle
   - wheelspin on low grip, with simple traction control if needed
3. Add the HUD (km/h, gear, rpm, slip), then write `findings/m3-tuning.md` in bifrost-research with targets vs. measured values.
