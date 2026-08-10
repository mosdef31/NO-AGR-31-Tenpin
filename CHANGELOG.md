# Changelog

## 0.9.0 - 2026-08-11

First release.

### The weapon

- Two pods, x7 at 225 kg and x19 at 580 kg, sharing one round and listed separately.
- Ripple fire across every station at 0.08 s. The game never wraps the weapon index for
  a `MissileLauncher` station, which no stock weapon uses, so a multi-pod loadout
  otherwise went silent after one rocket per pod.
- The whole pod fires at one target when several are locked, instead of the game's
  one-rocket-per-target burst.
- Angular dispersion, 1.5 mrad with a 12 m floor, replacing the engine's fixed CEP.
- A guidance budget on locked shots, so a lock no longer removes the need to aim.
- Both pods hang flush, each at half its own across-flats width.

### The HUD

- Artillery mode: ground designation, predicted impact, salvo footprint, maximum-reach
  arc, and a release cue tolerant to the round's accuracy and the target's size.
- Direct mode on the same key.
- A magnifier near release.
- Hides with the landing gear and on the external cameras.

### Aimpoint corrections

Three stock seeker assumptions that are wrong for a powered rocket:

- It extrapolates in free fall with no thrust term, aiming a 20 km rocket at a point
  under 2 km away and steering it there.
- Its time-to-target collapses in a dive, sending close shots climbing away.
- It assumes sea level, landing the pattern short of high ground.

### Low-speed launches

Aiming from the helicopters was structurally wrong rather than badly tuned. The guidance
budget was measured from a ballistic impact point a hovering launch cannot reach, so the
clamp fired on essentially every locked shot, and the pipper was smoothed at a rate sized
for a jet. Both now scale with launch speed, and both are unchanged at and above
170 m/s, so no fixed-wing behaviour moves.

### Packaging

- **The asset bundle ships inside the DLL.** One file instead of two. Blueprinter loads
  embedded bundles through the same path as loose ones, so PrefabHash assignment and the
  patch manifest are unaffected.
- The config went from 57 keys to 14. Balance values are settled and compiled in.
