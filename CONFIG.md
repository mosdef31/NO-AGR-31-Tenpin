# Configuration

`BepInEx/config/com.tenpin.cfg`, written on first run.

**34 keys: 11 for the HUD, 5 for the assists, 2 for effects, 1 for AI loadouts and 15
diagnostics.** During development this file had 57, covering dispersion, blast,
signature, price, guidance and the aimpoint corrections. Those are settled and compiled
into the build; the reasoning for each value sits next to the value in `Plugin.cs`.

The descriptions in the settings menu say what a setting does and nothing else, because
that is what fits in ConfigurationManager's panel. Where a setting exists for a reason
that is not obvious from what it does, the reason is under the table for its section
here.

**If you tested a pre-release build, delete the file and let it regenerate.** BepInEx
does not remove keys a new version stopped binding, so an old file keeps dead entries.


## HUD

| Key | Default | | Effect |
|---|---|---|---|
| `Enabled` | true |  | Draws the AGR-31's own weapon HUD while the pod is the selected store. Off leaves the stock missile UI. |
| `ModeKey` | KeyCode.B |  | Switches between direct (CCIP) and artillery (CCRP) presentation. |
| `DesignateKey` | KeyCode.T |  | Designates the ground point under the cursor in artillery mode, while the map is maximized. The game's untarget button clears it. A unit lock overrides a ground designation. |
| `PanelAnchor` | Hud.HudAnchor.RightBelowWeapons |  | Where the text block and the magnifier sit on screen. |
| `MaxRangeArc` | true |  | Draws the maximum-reach arc on the ground in artillery mode. |
| `Magnifier` | true |  | Draws a magnified inset of the area around the designated point. |
| `MapMarker` | true |  | Marks the designated ground point on the map, as a diamond in your HUD colour, while the pod is the selected weapon. |
| `NoseAimBelowKph` | 100 |  | Below this airspeed the impact point is drawn along the aircraft's nose instead of along its velocity. Horizontal only. Fades out over the 40 km/h above the threshold; 0 turns it off. |
| `TerrainCheck` | true |  | Draws a designated point that is out of sight as a dashed diamond instead of a solid one. |
| `HideWithGear` | true |  | Hides the weapon HUD while the landing gear is down. |
| `CockpitOnly` | true |  | Hides the HUD on the external cameras. |

**Why the mode is a keypress and not a range threshold.** A HUD that switches itself
mid-attack reads as broken even when the switch is correct.

**Why the magnifier exists.** Lofting a long shot puts the cues low on the screen over
the canopy rail, exactly when you are laying one mark on another to within a few pixels.

**Why `MapMarker` exists.** The designation is made on the map and used to be drawn only
in the cockpit, so the one screen where you set it gave no confirmation that it took.

**Why `NoseAimBelowKph` exists.** Rockets keep whatever sideways speed you hand them, so
at low airspeed a truthful cue wanders under you and cannot be laid on a target. This one
answers where the rounds go if you stop drifting, which is a question you can fly to.

**Why `TerrainCheck` exists.** The HUD is drawn over the world rather than in it, so a
mark standing on a ridge and a mark three kilometres behind it appear in the same place
at the same size.

**Why `CockpitOnly` defaults on.** The stock HUD lives on the cockpit glass and is not in
shot from outside. Ours is an overlay canvas, so without this it floats over an
orbit or chase view.


## Assist

| Key | Default | | Effect |
|---|---|---|---|
| `Release assist` | true |  | Fires the pod while you hold the trigger, at the moment the rockets will land on the designated point. Releasing the trigger stops it. With no designation, a target out of reach, or the HUD hidden, the trigger fires normally. |
| `Tilt assist` | false |  | Adds pitch to bring the rockets onto the range you need while a point is designated. Your stick input is added on top and is never capped. |
| `ReleaseAssistKey` | KeyCode.U |  | Turns the release assist on and off in flight. The HUD shows AUTO while it is armed. |
| `TiltAssistKey` | KeyCode.Y |  | Arms and disarms the tilt assist in flight. It starts every sortie disarmed. The HUD shows TILT ARMED when armed and TILT while it is flying the shot. |
| `Tilt assist authority` | 0.30 |  | Fraction of the pitch axis the tilt assist may use. Your own stick is added on top and is never limited. |

**They were designed as one package**, and chosen instead of widening dispersion, which would have made the shot easier by making each
rocket weaker.

**The release assist can only ever delay a shot you aimed at something.** It suppresses
the trigger calls the game already makes while you hold the button; it never makes one.
Once a salvo starts it stays open for the whole ripple, and it closes again if you stray
off the point.

**The tilt authority was 0.10 and was too weak to be useful.** Raise it past 0.30 if a
long shot still settles too slowly for you.


## Effects

| Key | Default | | Effect |
|---|---|---|---|
| `Motor effect donor` | AAM1 |  | Which stock missile's motor effect the rockets borrow, by its internal name. A comma separated list is allowed and the first that has fire wins; empty picks by closest burn time. Read per round. Suggested: AAM1, AGM2, AAM3. |
| `Silly effects` | false |  | Flies the AGR-31's own cyan effects instead of a borrowed stock plume. |

**The default is a borrowed plume.** The rockets take a stock missile's motor effect
rather than an authored one, and that is the shipped answer rather than a stopgap: the
stock effects are tuned against the game's own lighting and fog.

**Not every donor that lists a flame is a plume.** Some are ignition flashes with no
direction at all, which is what `AIR-2_Genie` is. Run the plume-scan tool (F9) to see
what each candidate actually draws before choosing one.

**`Motor effect donor` sits behind the Advanced toggle.** It ships at a donor chosen by
flying every candidate, and changing it without reading plume-scan's output picks a worse
plume than the default. It is still an `[Effects]` key in the config file.

**`Silly effects` was called `Fun effects` before 1.1.0.** BepInEx keeps keys a new
version stopped binding, so an old config file still carries a `Fun effects` line that
now does nothing. Delete it or regenerate the file.


## AI

| Key | Default | | Effect |
|---|---|---|---|
| `LoadoutChance` | 0.25 |  | Chance an AI flight carries the AGR-31 on a pylon that can take it, rolled per hardpoint set. 1 arms every cleared aircraft, 0 arms none. |


## Advanced

| Key | Default | | Effect |
|---|---|---|---|
| `StrikeFinHold` | 0.25 |  | Seconds the AGR-51's fins stay folded after the round leaves the pod. |
| `StrikeFinSweep` | 0.18 |  | Seconds the AGR-51's fins take to swing out once they start. |
| `WaterBackstop` | true |  | Ends a rocket that goes into the sea and is not set off by the game. Leave this on: without it such a rocket is never removed at all, and enough of them will stutter the frame rate. |
| `AiForceLoadout` | false |  | Arms every AI flight on a cleared airframe with the 'Saturation and Self Defence' loadout, ignoring LoadoutChance. |
| `TuningReadout` | false |  | Prints the round's maximum range and the stock damage table at the missions menu. |
| `DumpPrefabRenderers` | false |  | Dumps the mounted prefab's transforms, meshes, materials, shaders, layers and scales at the missions menu, next to a stock pod's. |
| `DumpFlightModels` | false |  | Dumps every stock round's drag curve, lift curve, torque, PID and fin area next to ours, sorted by drag per unit mass. |
| `LaunchTrace` | false |  | Traces the launch path and prints a verdict. Detail for the first eight shots, counters after that, one summary once the salvo goes quiet. |
| `AiShotAudit` | false |  | Prints, per AI shot, where the salvo landed against where the profile predicted, with the miss distance. |
| `AiShotAuditCount` | 12 |  | How many audited shots AiShotAudit prints before going quiet. |
| `AiReport` | false |  | Prints a line each time an AI declines to shoot, naming the reason it held fire. |
| `AiReportSeconds` | 10 |  | Seconds between repeats of the same AiReport reason. |
| `CheckBallistics` | false |  | Predicts every launch with the trajectory solver and scores it at impact. Costs three full integrations per launch plus a per- tick sample of every live round; a 42-round salvo is visibly laggy. |
| `CheckOnlyWeapon` |  |  | Only scores rounds whose unit name contains this text, e.g. \"MLRS\". Empty scores every launch. |
| `CheckFileName` | rocketpod-ballistics.txt |  | Name of the ballistics file, written under the BepInEx folder. |

Every key here is a diagnostic. All off by default, all writing to the log or to a file
under `BepInEx/`, none of them changing how the weapon flies. With ConfigurationManager
installed the section sits behind its "Advanced settings" toggle.

**They ship rather than being cut** because they are the only way to answer a bug report
from a machine that is not this one. The pod is an asset mod, and its whole class of
failure - an unset field, a missing map, a bundle that did not load - is invisible from
outside and obvious in these dumps.


## What used to be here

| Old section | Now fixed at |
|---|---|
| `[Dispersion]` | 1.5 mrad, 12 m floor, no ceiling |
| `[Aimpoint]` | all corrections on, budget 5 mrad |
| `[Signature]` | radar 0.01, IR off, damage tolerance 1.0 |
| `[Loadout]` | store card on, $25k a round, four extra aircraft |
| `[Effects]` | all on |
| `[Targeting]` | single-target salvo on |
| `[Tuning]` | 1500 m, 170 m/s, 15 to 20 km band |
| `[ControlRound]` | reachable only with `CheckBallistics` on |
| `[Diagnostics]` | moved to `[Advanced]`, defaulting to off |

**Ride height changed rather than froze.** One `MountVerticalOffset` key served both
pods, which could not work: the light is 333 mm across the flats and the heavy 514, so
their flush positions are 90 mm apart and either value left the other gapped or sunk into
the pylon. Each now hangs at half its own width, derived rather than guessed.
