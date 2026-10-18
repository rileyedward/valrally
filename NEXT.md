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
- M4 surfaces (tag `m4`):
  - Valheim ground → surface, per wheel: hoe paint first (paved → Tarmac, dirt → Gravel, cultivated → SoftDirt), then biome and slope, then a sea-level sand guess. Buildings use their piece material.
  - 13 surface profiles in config `[Surface <Name>]`, each with grip, rolling drag, peak-slip scale and curve shape.
  - Dust, mud and snow puffs plus crunch sounds, borrowed from the Viking's footstep effects. Toggles in `[Effects]`.
  - `rally_surface` prints what each wheel is on and why; `rally_surface hud` shows a live readout.
  - Turn surfaces off with `rally_set Surfaces.Enabled false`.
- M5 rally pieces: not started.
- M6 ghost and pace notes: not started.

## Decisions made without Riley (change any of them)

- CoM raised so weight transfer is real (see m3-tuning.md).
- Traction control on by default (`rally_set TractionControl false`).
- Slide help is off while the handbrake is held.
- Old config files get changed defaults once (`[Internal] DefaultsVersion`).
- Steering yaw assist reaches full strength at 45 km/h (I read AoR's constant as km/h), and fades out as the slide angle nears 25°, so it starts slides without spinning you.
- Sand has no game signal: terrain 4 m below to 1.5 m above sea level in Meadows, Black Forest, Plains or Mistlands counts as sand (`[Surfaces] SandBelowSea / SandAboveSea`).
- Natural rocks and other non-terrain colliders count as Stone.

## Next

1. M4 surfaces.
2. M5 pieces and timing.
3. M6 ghost and pace notes.
4. Riley playtest.
