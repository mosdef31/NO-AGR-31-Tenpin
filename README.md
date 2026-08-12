# AGR-31 Tenpin

**Rocket pods for Nuclear Option, with a HUD that tells you where they will land.**

[![Latest release](https://img.shields.io/github/v/release/mosdef31/NO-AGR-31-Tenpin?style=for-the-badge&label=download&color=2ea043)](https://github.com/mosdef31/NO-AGR-31-Tenpin/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/mosdef31/NO-AGR-31-Tenpin/total?style=for-the-badge&color=blue)](https://github.com/mosdef31/NO-AGR-31-Tenpin/releases)
[![Game version](https://img.shields.io/badge/Nuclear%20Option-0.34%2B-orange?style=for-the-badge)](https://store.steampowered.com/app/2168680/Nuclear_Option/)
[![License](https://img.shields.io/badge/license-CC%20BY%204.0-lightgrey?style=for-the-badge)](./ATTRIBUTION.md)

📥 **[Download the latest release](https://github.com/mosdef31/NO-AGR-31-Tenpin/releases/latest)** &nbsp;·&nbsp;
📝 **[What's new](./CHANGELOG.md)** &nbsp;·&nbsp;
⚙️ **[Settings](./CONFIG.md)** &nbsp;·&nbsp;
🐛 **[Report a bug](https://github.com/mosdef31/NO-AGR-31-Tenpin/issues)**

---

## What it is

Two rocket pods, a 7 shot and a 19 shot, firing an unguided 90 mm rocket that reaches
much further than the stock ones. Load six pods on an A-19 and you put 42 rockets in the
air in about three seconds, or 114 in about nine.

They are not precision weapons. You fire them at an area from a long way out and let
volume do the work, which makes them good against SAM and AAA sites and poor against a
single hard target. The AGR-24 is still the better answer to a tank.

| Pod | Rockets | Loaded | Empty | Price |
|---|---|---|---|---|
| AGR-31 Tenpin x7 | 7 | 225 kg | 50 kg | $175k |
| AGR-31 Tenpin x19 | 19 | 580 kg | 105 kg | $475k |

| | |
|---|---|
| Range | 15 to 20 km when lofted |
| Rocket | 90 mm, 2 m, unguided |
| Motor | 1.8 second burn |
| Warhead | 22.5 blast, 800 AP |
| Accuracy | 1.5 mrad, so the spread grows with range |
| Rate | one rocket every 0.08 s |
| Cost | $25k a rocket |

Range depends a lot on how you launch. Fired from 500 m you get about 18 km; from 10 km
you get about 35 km, because the rocket keeps your speed and thin air slows it less. The
number above is what you get in normal use, not a maximum.

Hard to shoot down, on purpose. A defence can hit these, but it burns two interceptors
per rocket doing it, so a full salvo empties a launcher whether or not it connects.

## What it looks like

![A-19 carrying a full six station load](./images/a19-loadout-sunflare.jpg)

| | |
|---|---|
| ![A salvo head on](./images/salvo-head-on.jpg) | ![A firing run against terrain](./images/firing-run-mountain.jpg) |
| ![A rocket leaving the tube](./images/wing-salvo-firing.jpg) | ![Climbing with the motor lit](./images/climb-motor-lit.jpg) |

Two flights are recorded in [`video/`](./video): one firing on a map designation, one
against a target picked out of the cockpit.

## The HUD

The stock missile UI does not show you where an unguided rocket lands, so the pod draws
its own.

**Artillery mode** is for lobbing them at something you can see on the map. Open the map,
press **T** on a spot - it is marked on the map so you can see it took - and the HUD
shows where the salvo will land, how big the spread
will be, how far you can reach, and turns the cue green when you should fire.

**Direct mode** is for the gun run. It shows a moving impact point you fly onto.

**B** switches between them. A magnifier pops up as you close on the release point,
because a lofted shot puts the marks low on the screen right when you need to read them
carefully. Locking a unit overrides whatever you designated on the ground.

**Targets on a hilltop are worth one extra habit.** The cue is honest about where the
rockets meet the ground, and on a peak that answer moves a long way for a small change of
attitude - a shot that clears the summit lands in the valley behind it. Rather than
hunting for the perfect release, fire on the cue and keep firing as the nose comes down
through it. The salvo walks up the slope onto the target and something in it connects.
That is what a saturation weapon is for.

## Help with the shot

Two settings that make a long shot easier to fly. They are meant to be used together.

**Release assist**, on by default. Designate a point, hold the trigger, and the pod fires
itself the moment the rockets will actually land there, instead of you judging it by eye.
Holding the trigger is what gives permission: nothing fires unless you are already holding
it, and letting go stops it at once. With nothing designated, or a target out of reach, the
trigger works exactly as it always has, so a gun run is unaffected. **U** turns it off and
on in flight, and the HUD reads AUTO while it is holding the trigger for you.

**Tilt assist**, off by default. You fly the heading, the pod flies the elevation. It adds
a small amount of pitch to bring the rockets onto the range you need, so lining up at 15 km
is a matter of pointing at the target rather than hunting for the right loft. Your own
stick is added on top and is never limited, so you can override it whenever you like.

Enable it in the settings, then arm it in flight with **Y**. The HUD reads TILT ARMED when
it is on and TILT while it is actually flying the shot. It only works once you are already
pointed roughly at what you designated, within about 30 degrees, so turning onto a target
is your own turn and it does not fight you on the way round. Raise `Tilt assist authority`
if it settles too slowly for you.

## What carries it

The A-19 out of the box, plus the SAH-46 Chicane, UH-90 Ibis, T/A-30 Compass and VT-7
Vagrant and the CI-22 Cricket, which already carry the AGR-24. Aryx's MiG-15, RAH-72
Knockout and F-16M King Viper too if you have them. You can add other aircraft in the
config by name.

Helicopters work but are the worst platform for it. Hovering, you have no speed to give
the rocket and cannot point the nose up, so you throw away most of the range.

## Install

You need **Nuclear Option 0.34**, **BepInEx 5.4.x**, and **Blueprinter 1.8.21**.
Blueprinter is not optional; without it the mod does not load at all and you will see
nothing from it in the log.

Then copy one file into `BepInEx/plugins/`:

- `TenpinMod.dll`

That is the whole install. The models and textures are packed inside the DLL, so there is
no separate `.nobp` to place.

**If you tested an early build**, delete any loose `tenpin.nobp` from `BepInEx/plugins/`
and delete the old `com.tenpin.cfg`. A leftover bundle wins over the packed one, so you
end up flying new code against old assets.

## Settings

`BepInEx/config/com.tenpin.cfg`, created the first time you run it. Twenty-two settings:
eleven are the HUD, four are the aiming help above, and the rest are diagnostics. The `[Advanced]` block is diagnostic logging, off by default,
and none of it changes how the weapon flies.

One worth knowing about: **`Fun effects`**. Off, the rockets use the game's own effects.
On, they get the ones made for this weapon, which is a bright cyan exhaust, sparks at
ignition and a flash at each tube. It looks great and it looks nothing like the rest of
the game, so it is off unless you ask for it. Sound is the same either way.

Everything else about how the weapon performs is fixed in the build.

Full list: [`CONFIG.md`](./CONFIG.md).

## Known issues

- **Helicopters are the weakest platform.** A hover has almost no reach.
- **Multiplayer is untested.** Nobody has flown this with another player. It is the
  biggest unknown in the mod.

## Background

There is a fictional history for the weapon in [`LORE.md`](./LORE.md) if you like that
sort of thing.

## Credit

The rocket model is based on someone else's work, and crediting them is a condition of
the licence rather than a nicety. It has to stay with any copy of this mod:

> "Roketsan Missiles" (https://skfb.ly/pq7qp) by **sakigakefuruzawa**, licensed under
> Creative Commons Attribution (http://creativecommons.org/licenses/by/4.0/). Changes
> were made.

Everything else, the pods, their textures and all of the code, is mine and is offered
under the same CC BY 4.0 licence. Details in [`ATTRIBUTION.md`](./ATTRIBUTION.md).
