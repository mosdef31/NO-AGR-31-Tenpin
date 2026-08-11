# Changelog

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
