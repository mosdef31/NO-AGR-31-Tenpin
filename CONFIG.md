# Configuration

`BepInEx/config/com.tenpin.cfg`, written on first run.

**Fifteen keys, eight of them the HUD.** During development this file had 57, covering
dispersion, blast, signature, price, guidance and the aimpoint corrections. Those are
settled and compiled into the build. The reasoning for each value sits next to the value
in `Plugin.cs`.

`[Effects]` holds one key, and it is taste rather than balance: the pod ships with the
game's own borrowed rocket effects, and `Fun effects` swaps in the set authored for this
weapon - a cyan plume and a flash at the tube. It is deliberately over the top. It changes
the look only; the pod sounds the same either way.

Everything under `[Advanced]` is a diagnostic dump. All off by default, all writing to
the log or a file under `BepInEx/`, none of it changing how the weapon flies. With
BepInEx's ConfigurationManager installed the section sits behind its "Advanced settings"
toggle.

**If you tested a pre-release build, delete the file and let it regenerate.** BepInEx
does not remove keys a new version stopped binding, so an old file keeps 43 dead entries.


## HUD

| Key | Default | | Effect |
|---|---|---|---|
| `Enabled` | true |  | Draw the AGR-31's own weapon HUD while the pod is the selected store. Off falls back to the stock missile UI, which has no impact cue at all. |
| `ModeKey` | KeyCode.B |  | Switch between direct (CCIP) and artillery (CCRP) presentation. A keypress rather than an automatic range threshold, deliberately: a HUD that changes itself mid-attack feels broken even when the switch is correct. |
| `DesignateKey` | KeyCode.T |  | In artillery mode, designate the ground point under the cursor while the map is MAXIMIZED. Clearing is not on this key - it is on the game's own untarget button, so one button means 'forget that target' whatever kind it is. A unit lock always wins over a ground designation. |
| `PanelAnchor` | Hud.HudAnchor.RightBelowWeapons |  | Where the text block and the magnifier sit. Presets rather than raw coordinates, because the useful positions are decided by what else is on screen: the chat log and kill feed own the top left, the stock weapon panel owns the top right, and the bottom edge carries the gear and flap cues. RightBelowWeapons clears all of them. |
| `MaxRangeArc` | true |  | Draw the maximum-reach arc on the ground in artillery mode. It answers 'can I touch that from here' at a glance instead of by comparing two numbers. |
| `Magnifier` | true |  | Draw a magnified inset of the area around the designated point. Lofting a long shot puts the cues low on the screen over the canopy rail, exactly when you are trying to lay one mark on another to within a few pixels. The inset magnifies the same marks rather than re-rendering them, so it cannot disagree with them, and it still works when the pipper has gone off the bottom of the screen. |
| `HideWithGear` | true |  | Hide the weapon HUD while the landing gear is down, which is what every stock weapon HUD does. |
| `CockpitOnly` | true |  | Hide the HUD on the external cameras. Ours is drawn to a screen-space overlay canvas of our own, so unlike the stock HUD - which lives on the cockpit glass and simply is not in shot from outside - nothing hides it for us, and it would otherwise float over an orbit or chase view. |

## Effects

| Key | Default | | Effect |
|---|---|---|---|
| `Fun effects` | false |  | Play the AGR-31's own authored effects: a cyan motor plume, an ignition ember burst, a nozzle light, a smoke trail and a flash at the tube. It is a deliberately gamey look and it is not what the weapon is supposed to be, which is why it ships OFF - off borrows the game's own rocket effects. The sound is the same either way. Takes effect on the next round fired. |

## Advanced

| Key | Default | | Effect |
|---|---|---|---|
| `TuningReadout` | false |  | Print the round's MAXIMUM range and the stock damage table at the missions menu. The range comes from an elevation sweep to 70 degrees, so it reports what the round is capable of rather than whatever loft was flown - and it needs no flight at all. |
| `DumpPrefabRenderers` | false |  | Dump the mounted prefab's transforms, meshes, materials, shaders, layers and scales at the missions menu, next to a stock pod's. This is what diagnoses an invisible or mis-shaded weapon from a log instead of by guessing. |
| `DumpFlightModels` | false |  | Dump every stock round's drag curve, lift curve, torque, PID and fin area next to ours, sorted by drag per unit mass. These are serialized ASSET values and cannot be read from a decompile, which matters because Tenpin once shipped with empty aero curves and zero torque and nothing noticed. |
| `CheckBallistics` | false |  | Predict every launch with the trajectory solver and score it at impact. NOT FREE: each launch runs three full integrations to ground impact plus a per-tick sample of every live round, and a seven-tube pod on six stations is 42 launches in a few seconds, which is visibly laggy. Turn it on for a measurement run and fire ONE pod, ideally with CheckOnlyWeapon set. |
| `CheckOnlyWeapon` |  |  | Only score rounds whose unit name contains this text, e.g. \"MLRS\". Empty scores every launch, which gets noisy fast in a real mission. |
| `CheckFileName` | rocketpod-ballistics.txt |  | Written under the BepInEx folder. A file rather than the log because LogOutput.log is truncated on the next launch, and this is exactly the output that gets read one launch too late. |

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
