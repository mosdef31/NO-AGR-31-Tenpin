# AGR-31 Tenpin

Air-launched saturation rocket artillery for Nuclear Option, in two pods and with its
own weapon HUD.

> **Status: 0.9.0.** Works end to end on 0.34. Both pods mount, ripple, guide and
> detonate, and the asset audit passes. Not 1.0.0 because the balance has been flown by
> one person and multiplayer is untested. See *Known issues*.

## What it adds

An unguided 90 mm rocket in a chamfered hexagonal pod, light and heavy, listed as
separate stores.

| Pod | Rounds | Loaded | Empty | Price |
|---|---|---|---|---|
| AGR-31 Tenpin x7 | 7 | 225 kg | 50 kg | $175k |
| AGR-31 Tenpin x19 | 19 | 580 kg | 105 kg | $475k |

| Stat | Value |
|---|---|
| Range | 15 to 20 km lofted, 19.5 km at the reference |
| Round | 90 mm, 2.0 m, inertial seeker |
| Motor | 5522 N for 1.8 s |
| Warhead | 22.5 blast / 800 AP |
| Dispersion | 1.5 mrad, floored at 12 m |
| Ripple | 0.08 s between rounds |
| Cost | $25k a round |

Six pods on an A-19 is 42 rounds of the light in about 3.3 s, or 114 of the heavy in
about 9.

**Range is not one number.** The round inherits the aircraft's velocity and thin air
costs it less, so it reaches about 18 km launched at 500 m and about 35 km at 10 km.
The figure above is a reference condition (1500 m, 170 m/s), not a promise. It also
varies by roughly 6% between maps.

## The role

Counter-SHORAD saturation from standoff, with light anti-armour on a direct hit. Not a
replacement for the AGR-24 against hard point targets: half the Kingpin's per-round
kill probability and less penetration, buying its effect through placement and volume
instead.

It is close to uninterceptable on purpose. The round is IR-dark and carries the stock
rocket family's 0.01 radar cross-section. What a defence gets instead is a damage
tolerance high enough that it commits two interceptors per round, so a salvo drains a
magazine even when every shot connects.

Background and doctrine: [`LORE.md`](./LORE.md).

## The HUD

The pod draws its own, because the stock missile UI has no impact cue and an unguided
rocket needs one.

| | |
|---|---|
| **Artillery (CCRP)** | Designate a ground point with **T** while the map is maximised. Draws predicted impact, salvo footprint, a maximum-reach arc, and a RELEASE cue tolerant to the round's own accuracy at that range. |
| **Direct (CCIP)** | Continuously computed impact point for the gun pass. |
| **Switch** | **B**. A keypress, not an automatic range threshold. |
| **Magnifier** | Appears near release. A lofted shot puts the cues low over the canopy rail exactly when they need reading precisely. |

A unit lock always overrides a ground designation.

## Carriers

The A-19's six stations from the bundle, plus four aircraft at runtime because the
AGR-24 already lives on those pylons: SAH-46 Chicane, UH-90 Ibis, T/A-30 Compass,
VT-7 Vagrant.

Aiming from the helicopters is improved in 0.9.0 but still the weakest case. A hovering
launch inherits no velocity and cannot loft, so it throws away most of the standoff.

## Requirements

| | |
|---|---|
| Game | Nuclear Option 0.34 |
| Loader | BepInEx 5.4.x |
| **Blueprinter** | `com.nikkorap.blueprinter` 1.8.21, **required** |

Blueprinter is a hard dependency. Without it the plugin does not load and nothing from
this mod appears in the log.

## Install

Copy **one file** into `BepInEx/plugins/`:

- `TenpinMod.dll`

The asset bundle ships inside the DLL. There is no separate `.nobp`.

> **Upgrading from a pre-release build:** delete any loose `tenpin.nobp` from
> `BepInEx/plugins/`. Loose bundles are scanned before embedded ones, so an old file
> masks the new assets and you get the new code against the old pod.

## Configuration

`BepInEx/config/com.tenpin.cfg`, written on first run. Fifteen keys, eight of them the
HUD. Everything under `[Advanced]` is a diagnostic dump, defaults to off, and changes
nothing about how the weapon flies.

`[Effects] Fun effects` is the one taste key. Off, the pod uses the game's own rocket
effects. On, it uses the set authored for this weapon - the cyan plume, the ember burst
and the flash at the tube. It is deliberately over the top, which is why it is opt-in. It
changes the look only; the pod sounds the same either way.

Full table: [`CONFIG.md`](./CONFIG.md).

Balance is fixed in the build. Dispersion, blast, price, signature, guidance and the
aimpoint corrections were config keys during development and are now settled values.

If you tested a pre-release build, delete the old `.cfg` and let it regenerate.

## Known issues

- **Helicopters are the weakest carrier.** 0.9.0 scales the guidance budget and the
  pipper smoothing by launch speed, which fixes the part that read as broken. A hover
  still has very little reach.
- **The detonation effect is borrowed** from a stock AGR at runtime and is sized for that
  warhead rather than this one, so the impact reads weaker than the round is. The motor
  plume, the smoke trail and the launch flash have an authored version behind
  `Fun effects`; the warhead has none either way.
- **Multiplayer is untested.** `PrefabHash` consistency across clients has never been
  checked. Largest unknown in the mod.
- **The heavy's silhouette is stubby**, 4.18:1 against the light's 6.45, because both
  share a length driven by the 2 m round.
- **Blast may be too weak.** 22.5 rests on judgement rather than a stock reference, and it
  cannot be raised far while the detonation effect is borrowed - see above.

## Attribution

The rocket body derives from a third-party CC-BY asset. **The credit is a licence
condition, not a courtesy**, and must travel with any copy of this mod:

> "Roketsan Missiles" (https://skfb.ly/pq7qp) by **sakigakefuruzawa**, licensed under
> Creative Commons Attribution (http://creativecommons.org/licenses/by/4.0/). Changes
> were made.

Everything else, the pod mesh, its skin and all of the C#, is original and offered under
the same CC Attribution 4.0 licence. Detail in [`ATTRIBUTION.md`](./ATTRIBUTION.md).

## Documentation

[`TECHNICAL.md`](./TECHNICAL.md) covers the asset contracts, registration timing, and the
failure modes this mod has hit. Read it before changing the C# or the Unity assets.
