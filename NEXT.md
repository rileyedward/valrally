# Where things stand

Targets Valheim 1.0.20, BepInExPack 5.4.2351, Jotunn 2.30.2 (see README).
Nothing since M2 has been driven in game yet. Everything below is checked by builds and the offline sim (`BifrostRally.Sim`) only.

## Milestones

- M0–M2: done and played.
- M3 (tag `m3`):
  - Pacejka tyres, wheel spin, drivetrain, traction control.
  - Feel layer and HUD.
  - Tuning console (`rally_set`, `rally_get`, `rally_save`, `rally_load`).
  - `rally_test [compare]`.
  - Offline sim passes every check. Notes are in `bifrost-research/findings/m3-tuning.md`.
- M4 surfaces: in progress.
- M5 rally pieces: not started.
- M6 ghost and pace notes: not started.

## Decisions made without Riley (change any of them)

- CoM raised so weight transfer is real (see m3-tuning.md).
- Traction control on by default (`rally_set TractionControl false`).
- Slide help is off while the handbrake is held.
- Old config files get changed defaults once (`[Internal] DefaultsVersion`).

## Next

1. M4 surfaces.
2. M5 pieces and timing.
3. M6 ghost and pace notes.
4. Riley playtest.
