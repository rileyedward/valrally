# Where things stand

Targets Valheim 1.0.20, BepInExPack 5.4.2351, Jotunn 2.30.2 (see README).
Nothing since M2 has been driven in game yet. Everything below is checked by builds and the offline sim (`BifrostRally.Sim`: handling checks per surface plus stage-logic tests) only.

## Milestones

- M0–M2: done and played.
- M3 (tag `m3`):
  - Pacejka tyres, wheel spin, drivetrain, traction control.
  - Feel layer and HUD.
  - Tuning console (`rally_set`, `rally_get`, `rally_save`, `rally_load`).
  - `rally_test [compare]`.
  - Notes are in `bifrost-research/findings/m3-tuning.md`.
- M4 surfaces (tag `m4`):
  - Valheim ground → surface, per wheel: hoe paint first (paved → Tarmac, dirt → Gravel, cultivated → SoftDirt), then biome and slope, then a sea-level sand guess. Buildings use their piece material.
  - 13 surface profiles in config `[Surface <Name>]`, each with grip, rolling drag, peak-slip scale and curve shape.
  - Dust, mud and snow puffs plus crunch sounds, borrowed from the Viking's footstep effects. Toggles in `[Effects]`.
  - `rally_surface` prints what each wheel is on and why; `rally_surface hud` shows a live readout.
  - Turn surfaces off with `rally_set Surfaces.Enabled false`.
- M5 rally pieces (tag `m5`):
  - Hammer → Misc tab: Rally start, Rally checkpoint, Rally finish. Each costs 1 wood and is a copy of a vanilla banner, falling back to a pole or torch.
  - Stop within 8 m of a start to arm; the clock starts when you roll.
  - Checkpoints count in order (8 m radius, height ignored), then the finish.
  - The route is chained automatically, nearest first, from the start.
  - Timer and splits on screen, with deltas against your best.
  - Best time is saved per world and start position in `BepInEx/config/bifrostrally-stages/`.
  - `rally_stage` and `rally_stage forget`. Settings are in `[Stages]` and `[Stage Timing]`.
- M6, partial (in the `m5` build too):
  - Ghost of your best run as a translucent box (`rally_set Stages.Ghost false` to hide it).
  - Text pace notes from checkpoint geometry, e.g. "Right 3, 100" or "Hairpin left, 50". They're called about 3 s before each checkpoint (`rally_set Stages.PaceNotes false`).
  - Engine sound: skipped. Valheim has no engine-like loop to borrow.
  - Real car model: skipped (needs new art).
- M7 multiplayer: not started.

## Decisions made without Riley (change any of them)

- CoM raised so weight transfer is real (see m3-tuning.md).
- Traction control on by default (`rally_set TractionControl false`).
- Slide help is off while the handbrake is held.
- Old config files get changed defaults once (`[Internal] DefaultsVersion`).
- Steering yaw assist reaches full strength at 45 km/h (I read AoR's constant as km/h), and fades out as the slide angle nears 25°, so it starts slides without spinning you.
- Sand has no game signal: terrain 4 m below to 1.5 m above sea level in Meadows, Black Forest, Plains or Mistlands counts as sand (`[Surfaces] SandBelowSea / SandAboveSea`).
- Natural rocks and other non-terrain colliders count as Stone.
- Gates are radius checks around the piece, not lines, so piece rotation doesn't matter.
- Stage order is nearest-first. Two stages need to be far enough apart that one's checkpoints aren't nearer than its own finish.
- Pace notes treat each checkpoint as one corner. More checkpoints means finer notes.

## Unverified guesses that could break in game

- Vanilla prefab names `piece_banner01/02/03`, `wood_pole2`, `wood_pole` and `piece_groundtorch_wood`. If none exist, the log says "No vanilla piece found".
- The terrain collider sitting on or under the `Heightmap` object. If not, `rally_surface` reports "static collider …".
- The `Sprites/Default` shader being available for the ghost. If not, it falls back to the cart material and isn't see-through.

## Next

1. Riley playtest (script in the latest message), plus `rally_test compare`.
2. Tuning round from the notes and the log.
3. M6 rest (engine audio, if a source turns up) and M7 multiplayer.
