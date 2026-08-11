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

### Sound, and a salvo budget

- The pod had no sound at all. It now has a launch report per shot, played at the tube
  that fired, layered across eight voices so an 0.08 s ripple reads as a roar instead of
  one clipped click repeated forty-two times.
- Rounds carry a motor loop in flight, capped at six at once. Sound does not thin out
  gracefully the way particles do: a hundred copies of one clip is comb filtering, not a
  louder rocket.
- Plume and smoke emission taper once more than eight rounds are up, with a floor, so the
  heavy pod's 114-round salvo costs less than 114 times one round.

Every part of this fills a slot the bundle authors empty, and stops running by itself the
moment real effects are authored in Unity.

### The plume, behind `Fun effects`

The AGR-31 can burn cyan. The Combine modernised exactly one thing about an eighty-year-old
rocket, and it was the motor, so the plume is where that shows: a near-white core through
cyan to a violet fringe, cooling into cool grey smoke. Every other rocket in the game is
orange, which is the point - a Tenpin salvo is identifiable as one at fifteen kilometres.

That set is authored rather than borrowed: a short hot plume, an ignition ember burst, a
light at the nozzle, a smoke trail that lingers after the round is gone, and a separate
flash and puff at each tube as it fires.

**It ships off.** `[Effects] Fun effects` turns it on. The default is the game's own rocket
effects, borrowed at spawn, because the authored set is deliberately over the top and that
is not what the weapon is. The sound is the same either way. Off does real work rather than
skipping a branch - the effects live on the prefab, so the plugin takes them back out
before borrowing.

The borrow itself got two fixes it needed. Donors are now scored by whether they carry
FIRE, not by how many particle systems they have: the preferred donor was a stock MLRS
rocket whose only non-trail system is a one-second white smoke puff, so the default mode
flew with smoke and nothing else. The flash at the tube had the same fault for the same
reason. The detonation borrows from the rocket family rather than from a heavy anti-ship
missile, which overshot in the other direction.

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
- The config went from 57 keys to 15. Balance values are settled and compiled in; what is
  left is the HUD, the diagnostics, and `Fun effects`.
