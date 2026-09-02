# Changelog

## 1.2.3

- **The heat haze is visible.** The shimmer behind a burning motor was capped so
  low that it only ever covered a short stub at the tail. It now reaches a few
  hundred metres back and holds its strength before it dissipates.
- **Smoke hangs around longer.** The column reaches further behind the round, at
  the same thin opacity.
- **A new Strike rocket, with a new fin deployment.** The AGR-51's round and its four
  blades are re-modelled. The fins now open to 45 degrees rather than 90, so the
  deployed span is about half what it was while the blade itself is bigger.
- **The exhaust fire goes out shortly after launch.** It used to burn for the whole
  motor run. The smoke trail and the heat shimmer are unaffected and still run to
  burnout. How long the fire lasts is under Effects if you want a different look.
- **The heat shimmer is a stream instead of a string of puffs.** It was emitted on a
  timer, so at rocket speeds it left a gap of about thirteen metres between one puff
  and the next. It is spaced by distance now, sits at the width of the rocket's own
  body rather than sprawling a metre wide, and opens out and fades behind the round
  instead of hanging in the air.

- **The Strike pod fires one rocket at a time.** A tap of the trigger now sends a single
  AGR-51 instead of two or three, and holding the trigger walks the four rounds out over
  about a second. The AGR-31 pods are unchanged and still ripple as fast as they did.
- **A shorter log.** Every message the mod writes is now a single readable line. The
  reasoning behind each one moved into the code, where it belongs.
- **The rockets leave a proper trail.** The smoke was a line of separate blobs because
  the game emits one trail particle every thirty metres of flight. It is now a
  continuous column of smaller particles, and it thins itself out automatically when a
  lot of rounds are in the air.
- **A hot exhaust instead of a flame.** A round under power now shows a short jet at the
  nozzle and distorts the air behind it, the way the aircraft engines do, rather than
  trailing a flame. Turn it off under Effects if large salvos cost you frames.
- **The AI flies each rocket the way that rocket is meant to be flown.** It used to
  treat both as the AGR-31, so it would run a four round Strike pod in to eight
  kilometres and empty it at one truck. The Strike is now used as the standoff weapon
  it is: it stays further out, waits for a better aiming solution before it shoots, and
  sends one or two rounds at a time instead of a salvo. It will also now attack a single
  moving vehicle, which it refused to do before. The AGR-31 is unchanged.

## 1.2.2 - 2026-09-01

- **The OA-27 Cavalier can carry the pods.** The AGR-51 Strike and the seven shot AGR-31
  are now offered on its inner and outer wing pylons. The larger 18 and 19 shot pods are
  not; they are too much pod for the aircraft.
- **The pods reach modded aircraft again.** On some launches they were offered only on the
  stock aircraft, and every modded one was missed. The mod was checking for those aircraft
  before the mods that add them had finished loading, so whether it worked came down to
  which finished first. It now keeps looking until they are all there.

## 1.2.1 - 2026-09-01

- **The pods appear again.** On some installs the weapons never showed up in the loadout
  list, even though the mod's settings page was there. The mod stopped waiting for
  Blueprinter after one minute, and on a machine with a lot of mods Blueprinter can take
  longer than that to finish loading. It now waits four minutes.
- **Fixed a clash with other mods.** The mod's asset file carried the name Unity gives a
  new one by default, so any other mod that had left the same default name could push
  this one out, and whichever lost never loaded at all. It now uses its own name.
- **A quieter log.** A mod that was still loading normally reported itself as broken over
  and over. It now reports a problem once, and only when there really is one.

## 1.2.0 - 2026-08-31

- **A second weapon, the AGR-51 Strike.** A four round pod firing a 133 mm rocket, where
  the AGR-31 fires a 90 mm one. The rounds are five times the warhead and two and a half
  times the weight, so a pod holds four instead of seven to nineteen. It goes on the same
  aircraft the 18 shot drum does.
  It reaches about 25 km from a fast pass, where the AGR-31 reaches about 20.
- **The Strike's fins fold.** The rounds sit flush inside their tubes and the fins swing
  out a moment after launch, once the round is clear of the pod.
- **Rockets that hit water are no longer left in it.** They sometimes failed to go off,
  passed through the surface and kept going, and enough of them at once stuttered the
  game badly. Any rocket that ends up under water is now made to go off, or removed if it
  will not.

## 1.1.1 - 2026-08-26

- **The pods show their rocket count in the loadout list again.** All three read
  "AGR-31 Tenpin" with nothing after it, so the 7, 18 and 19 shot pods could not be told
  apart in the dropdown.

## 1.1.0 - 2026-08-25

- **A third pod, the AGR-31 Tenpin x18.** A slimmer 18 shot drum for fast jets. The
  Shrike, King Viper, Compass and Vagrant carry it.
- **AI aircraft know how to use the pod.** They close to a range they can reach, loft the
  shot, fire a salvo sized to the target, and stay for another pass or two. They aim at a
  convoy as a column instead of walking vehicle to vehicle, and commit a real volume
  against SAM sites, ships and buildings. This applies to both sides.
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
