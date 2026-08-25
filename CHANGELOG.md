# Changelog

## 1.1.0 - 2026-08-25

- **A third pod, the AGR-31 Tenpin x18.** A slimmer 18 shot drum for fast jets. The
  Shrike, King Viper, Compass and Vagrant carry it.
- **The AI flies with the pod.** Give a flight the pod and it closes to a range it can
  reach, lofts the shot, fires a salvo sized to the target, and stays for another pass or
  two. It aims at a convoy as a column instead of walking vehicle to vehicle, and commits
  a real volume against SAM sites, ships and buildings. It works on both sides.
- **Moving targets are led.** Lock a ship or a convoy and the cue sits ahead of it, and
  the rockets go where the cue is rather than where the target was when you fired.
  Previously a moving target was shot at directly and the salvo landed behind it.
- **Firing as a client works in multiplayer.** Before this, a player who was not the host
  used up the ammo and got no rocket, no launch sound and no tube flash, and it stuttered
  the game for everyone. Client shots are now sent to the host, which launches them, so
  everyone sees the same rockets. You will not hear your own launch report as a client;
  the rockets, their flight sound and their impacts are all normal.
- **The HUD no longer stutters when you look sideways** in artillery mode.
- **The rockets use the game's own exhaust by default.** The cyan exhaust made for this
  weapon is still there, now under **`Silly effects`**. It was called `Fun effects`;
  **delete any `Fun effects` line in your config**, it no longer does anything.
- **New setting: `Motor effect donor`.** Picks which stock missile's exhaust the rockets
  borrow. It takes effect on the next shot, so you can try a few without restarting.

## 1.0.0 - 2026-08-12

- **Release assist**, on by default, and **U** switches it off and on in flight. The HUD
  reads AUTO while it is holding the trigger for you. Designate a point, hold the trigger,
  and the pod fires itself when the rockets will land there, then stops when you drift off.
  Holding the trigger is the permission - nothing fires unless you are already holding it,
  and letting go stops it. With nothing designated the trigger behaves exactly as before,
  so gun runs are unaffected.
- **Tilt assist**, off by default. You fly the heading and the pod flies the elevation,
  adding a little pitch to bring the rockets onto the range you need. Enable it in the
  settings and arm it in flight with **Y**; the HUD shows TILT ARMED when it is on and
  TILT while it is working. It only acts once you are pointed roughly at the target, and
  it does nothing while the map is open. Your own stick is added on top and is never
  limited. `Tilt assist authority` sets how much it may use.
- Carrying both pod sizes now gives you one weapon instead of two. They ripple together
  across everything you are carrying rather than having to be selected separately. They
  are still listed and priced separately in the loadout.
- Aiming at anything on high ground is fixed, both the cue and the rockets. The cue used
  to predict where the rockets reach sea level and correct once for the ground under that
  point, which on a hill is the valley behind it - so the mark sat at the foot of the
  mountain and the shot went well past anything on top. The rockets had their own copy of
  the same shortcut, which is why they still flew over a target the cue had settled on.
  Both now follow the trajectory until it actually meets the ground. Locking a unit also
  aims at its own height rather than at a guess.
- Targets on a hilltop are worth firing a little longer on. See the README: the ground
  under a peak moves a long way for a small change of attitude, so ride the cue down
  rather than hunting for one perfect release.
- The smoke trail is much shorter, thinner, fainter and clears much faster. It was borrowed from a
  battlefield rocket, whose trail is meant to hang in the air and mark where a barrage came
  from, and on a rocket that flies for half a minute that filled the sky.

## 0.9.2 - 2026-08-12

- Fixed the pods and rockets rendering bright pink for some players. The mod was shipping
  its own copy of the shader the pod is drawn with, built for one machine's graphics
  settings, and anyone whose machine differed got no usable version of it. The pod now
  uses the game's own copy, which every machine already has. Reported on Linux and Steam
  Deck in particular.

## 0.9.1 - 2026-08-12

- Rockets in flight had no map icon, which the game drew as a big filled square - so a
  salvo covered the map in them. They are small darts now, pointing the way they are
  going.
- A designated point you cannot actually see is drawn as a dashed diamond, so a mark on a
  ridge and a mark behind it stop looking identical. `[HUD] TerrainCheck` turns it off.
- The pod is offered on the F-99 Shrike.
- Fixed aiming at low speed, which was worst on helicopters: the impact point used to be
  drawn behind you or off to one side and could not be laid on a target. Below
  `[HUD] NoseAimBelowKph` (100 km/h) the cue is drawn as though you were tracking straight
  along your nose, which is the question you can fly to. Only the horizontal is
  substituted, so a descent is still shown truthfully, and above the threshold nothing
  changes at all.
- Rockets keep whatever sideways speed you hand them, so fly in balance before firing.
- The pod is now offered on the CI-22 Cricket, the RAH-72 Knockout and the F-16M King
  Viper, and on the MiG-15 it is on the wing pylons only - it was being offered in place
  of the cannons and the tail hook.
- The point you designate with T is now marked on the map, in your own HUD colour, while
  the pod is selected. It was drawn only in the cockpit before, so the screen you set it
  from gave no sign it had taken. `[HUD] MapMarker` turns it off.

## 0.9.0 - 2026-08-11

First release.

### The weapon

- Two pods, a 7 shot at 225 kg and a 19 shot at 580 kg, firing the same rocket and listed
  as separate stores.
- Ripple fire across every pod you are carrying, one rocket every 0.08 s.
- The whole pod goes at one target when you have several locked, rather than splitting one
  rocket per target.
- Spread grows with range instead of being a fixed distance, so a close shot is tight and
  a long one is not.
- Locking a target no longer aims for you. It helps, within a limit, and you still have to
  point the aircraft.
- Both pods hang flush against the pylon.

### The HUD

- Artillery mode: designate a point on the map and see where the salvo lands, how wide it
  spreads, how far you can reach, and when to fire.
- Direct mode for gun runs, on the same key.
- A magnifier that appears as you close on the release point.
- Hides with the landing gear and on the external cameras, like the stock HUD.

### Sound

The pod was completely silent before. It now has a launch crack per rocket, played at the
tube that fired, and a motor sound in flight. Both are built to survive a full salvo:
forty-two overlapping copies of one clip is a buzz, not a louder rocket, so the launch
crack is spread across eight voices and the flight sound is capped at six rockets at once.

Smoke and exhaust also thin out once you have a lot in the air, which is what keeps a
114 rocket salvo from costing 114 times one rocket.

### Fun effects

Turn on `Fun effects` and the rockets get their own look: a bright cyan exhaust, sparks at
ignition, a light at the nozzle, a smoke trail that hangs around after the rocket is gone,
and a flash at each tube as it empties. Every other rocket in the game is orange, so a
salvo of these is recognisable from a long way off.

It is off by default. It looks great and it looks nothing like the rest of the game, which
is a fine thing to opt into and a bad thing to have forced on you. Sound is the same either
way.

With it off you get the game's own rocket effects, and two things there needed fixing:
the exhaust had smoke but no fire, and the explosion was borrowed from a heavy anti-ship
missile and was far too big. Both now come from weapons in the same class as this one.

### Aiming

The game's own guidance makes three assumptions that are wrong for a powered rocket:
it ignores the motor and aims at a point far too close, it gets confused in a dive, and it
assumes everything is at sea level, which lands the salvo short of high ground. All three
are corrected.

Aiming from helicopters was properly broken rather than badly tuned, because everything
was sized for a jet's speed. It now scales with how fast you are going when you fire, and
nothing changes at fixed-wing speeds.

### Packaging

- Everything ships inside the DLL, so the install is one file instead of two.
- The config went from 57 settings to 15. The rest are decided and built in.
