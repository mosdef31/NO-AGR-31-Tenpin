using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RocketPod.Ballistics;
using UnityEngine;

namespace RocketPod
{

    internal static class TuningReadout
    {
        private const BindingFlags Inst =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static bool _done;

        internal static void RunOnce()
        {
            if (_done) return;
            _done = true;

            try
            {
                Range();
                Damage();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[Tenpin] Tuning readout failed: {ex.Message}");
            }
        }

        private static void Range()
        {
            Missile? ours = EncyclopediaRegistration.ResolvedMissile?.unitPrefab != null
                ? EncyclopediaRegistration.ResolvedMissile.unitPrefab.GetComponent<Missile>()
                : null;
            if (ours == null) return;

            TrajectorySolver.RoundSpec? spec = RoundSpecFactory.FromMissile(ours, Plugin.Log);
            if (spec == null)
            {
                Plugin.Log.LogWarning(
                    "[Tenpin] Range readout skipped: the round's flight model could not be read.");
                return;
            }

            float alt = Plugin.TuningLaunchAltitude.Value;
            float speed = Plugin.TuningLaunchSpeed.Value;

            var position = new Vector3(0f, alt, 0f);
            Vector3 heading = Vector3.forward;

            float max = TrajectorySolver.MaxRange(spec, position, heading, speed, groundY: 0f);

            Plugin.Log.LogInfo(
                $"[Tenpin] -- Range readout: {max / 1000f:0.00} km at the reference condition " +
                $"({alt:0} m, {speed:0} m/s), best of an elevation sweep to 70 deg. " +
                "No flight needed: change the motor in Unity, re-export, read this line.");

            Plugin.Log.LogInfo("[Tenpin]    across launch speed, at the same altitude:");
            foreach (float v in new[] { 140f, 170f, 220f, 280f, 340f })
            {
                float r = TrajectorySolver.MaxRange(spec, position, heading, v, groundY: 0f);
                string mark = Mathf.Abs(v - speed) < 0.5f ? "  <- reference" : "";
                Plugin.Log.LogInfo(
                    $"[Tenpin]      {v,4:0} m/s ({v * 3.6f,4:0} km/h) -> {r / 1000f,6:0.00} km{mark}");
            }

            Plugin.Log.LogInfo("[Tenpin]    across launch altitude, at the reference speed:");
            float prev = -1f;
            foreach (float a in new[] { 500f, 1500f, 3000f, 6000f, 10000f })
            {
                float r = TrajectorySolver.MaxRange(
                    spec, new Vector3(0f, a, 0f), heading, speed, groundY: 0f);
                string delta = prev < 0f ? "" : $"  ({(r - prev) / 1000f:+0.00;-0.00} km)";
                string mark = Mathf.Abs(a - alt) < 0.5f ? "  <- reference" : "";
                Plugin.Log.LogInfo(
                    $"[Tenpin]      {a,6:0} m -> {r / 1000f,6:0.00} km{delta}{mark}");
                prev = r;
            }

            float lo = Plugin.TargetRangeMinKm.Value * 1000f;
            float hi = Plugin.TargetRangeMaxKm.Value * 1000f;
            string verdict =
                max < lo ? $"SHORT of the {Plugin.TargetRangeMinKm.Value:0.#}-{Plugin.TargetRangeMaxKm.Value:0.#} km band by {(lo - max) / 1000f:0.00} km"
              : max > hi ? $"LONG of the {Plugin.TargetRangeMinKm.Value:0.#}-{Plugin.TargetRangeMaxKm.Value:0.#} km band by {(max - hi) / 1000f:0.00} km"
              : $"INSIDE the agreed {Plugin.TargetRangeMinKm.Value:0.#}-{Plugin.TargetRangeMaxKm.Value:0.#} km band";
            Plugin.Log.LogInfo($"[Tenpin]    verdict: {verdict}.");

            var motors = typeof(Missile).GetField("motors", Inst)?.GetValue(ours) as Array;
            if (motors != null)
            {
                for (int i = 0; i < motors.Length; i++)
                {
                    object? st = motors.GetValue(i);
                    if (st == null) continue;
                    Plugin.Log.LogInfo(
                        $"[Tenpin]    motor {i}: thrust={MotorFloat(st, "thrust"):0} N " +
                        $"burnTime={MotorFloat(st, "burnTime"):0.##} s " +
                        $"fuelMass={MotorFloat(st, "fuelMass"):0.##} kg");
                }
            }

            SolveMotor(spec, position, heading, speed, max);
        }

        private static void SolveMotor(TrajectorySolver.RoundSpec spec, Vector3 position,
                                       Vector3 heading, float speed, float currentMax)
        {
            if (spec.Motors.Length == 0) return;

            float targetKm = Plugin.TuningTargetRangeKm.Value;
            float target = targetKm * 1000f;

            float bestScale = 1f;
            float bestRange = currentMax;
            float bestErr = Mathf.Abs(currentMax - target);

            for (float f = 0.20f; f <= 1.60f; f += 0.02f)
            {
                var scaled = new TrajectorySolver.RoundSpec
                {
                    LaunchMass = spec.LaunchMass,
                    FinArea = spec.FinArea,
                    SupersonicDrag = spec.SupersonicDrag,
                    TopSpeed = spec.TopSpeed,
                    DragCurve = spec.DragCurve,
                    AirDensity = spec.AirDensity,
                    SpeedOfSound = spec.SpeedOfSound,
                    Motors = spec.Motors
                        .Select(m => new TrajectorySolver.MotorStage(
                            m.Thrust * f, m.FuelMass * f, m.BurnTime))
                        .ToArray(),
                };

                float r = TrajectorySolver.MaxRange(scaled, position, heading, speed, groundY: 0f);
                float err = Mathf.Abs(r - target);
                if (err < bestErr) { bestErr = err; bestScale = f; bestRange = r; }
            }

            if (Mathf.Abs(bestScale - 1f) < 0.001f)
            {
                Plugin.Log.LogInfo(
                    $"[Tenpin]    motor solve: the current motor is already the closest to " +
                    $"{targetKm:0.#} km of anything in the sweep. Leave it alone.");
                return;
            }

            Plugin.Log.LogInfo(
                $"[Tenpin]    motor solve: scale total impulse to {bestScale:0.00}x for " +
                $"{bestRange / 1000f:0.00} km, against a band midpoint of {targetKm:0.#} km. " +
                "Set these on Tenpin_Rocket in Unity:");
            for (int i = 0; i < spec.Motors.Length; i++)
            {
                TrajectorySolver.MotorStage m = spec.Motors[i];
                Plugin.Log.LogInfo(
                    $"[Tenpin]      motor {i}: thrust={m.Thrust * bestScale:0} N " +
                    $"burnTime={m.BurnTime:0.##} s fuelMass={m.FuelMass * bestScale:0.##} kg");
            }
        }

        private static void Damage()
        {
            Encyclopedia? enc = null;
            enc = GameData.EncyclopediaOrNull();
            if (enc?.missiles == null) return;

            var rows = new List<(string Key, float Mass, float Blast, float Pierce)>();
            (string Key, float Mass, float Blast, float Pierce)? ours = null;

            foreach (MissileDefinition md in enc.missiles)
            {
                if (md?.unitPrefab == null) continue;
                Missile? m = md.unitPrefab.GetComponent<Missile>();
                if (m == null) continue;

                float blast = Float(m, "blastYield");
                float pierce = Float(m, "pierceDamage");
                float mass = Float(m, "mass");

                var row = (md.jsonKey ?? md.name, mass, blast, pierce);
                if (md.jsonKey == PluginInfo.MissileKey) ours = row;
                else if (blast > 0f || pierce > 0f) rows.Add(row);
            }

            if (rows.Count == 0) return;

            Plugin.Log.LogInfo(
                "[Tenpin] -- Damage, stock rounds vs ours, by yield. Mass is alongside because a " +
                "yield reasonable on a 600 kg anti-ship missile is not reasonable on a 25 kg " +
                "rocket. --");
            Plugin.Log.LogInfo(
                "[Tenpin]    HARD CEILING: keep blastYield under 200 while warhead effects are " +
                "borrowed. Above it Missile+Warhead.Detonate calls GetComponentInChildren<Shockwave>() " +
                "with no null check, and a borrowed AGR-18 effect has none - the whole detonation " +
                "aborts and does NOTHING, which reads as far worse than a weak warhead.");

            foreach (var r in rows.OrderByDescending(r => r.Blast).Take(26))
                Plugin.Log.LogInfo(Line(r));

            string[] family = (Plugin.FlightModelDonors.Value ?? string.Empty)
                .Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
            var familyRows = rows
                .Where(r => family.Any(f => string.Equals(f, r.Key, StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(r => r.Blast)
                .ToList();
            if (familyRows.Count > 0)
            {
                Plugin.Log.LogInfo("[Tenpin]   ---- the unguided rocket family, our actual peers ----");
                foreach (var r in familyRows) Plugin.Log.LogInfo(Line(r));
            }

            if (ours != null)
            {
                Plugin.Log.LogInfo("[Tenpin]   ---- ours ----");
                Plugin.Log.LogInfo(Line(ours.Value));
            }
        }

        private static string Line((string Key, float Mass, float Blast, float Pierce) r) =>
            $"[Tenpin]   {r.Key,-28} mass={r.Mass,7:0.#} blastYield={r.Blast,8:0.#} " +
            $"pierceDamage={r.Pierce,8:0.#} " +
            $"blastPerKg={(r.Mass > 0f ? r.Blast / r.Mass : 0f),7:0.##}";

        private static float Float(Missile m, string field)
        {
            object? v = typeof(Missile).GetField(field, Inst)?.GetValue(m);
            return v is float f ? f : 0f;
        }

        private static float MotorFloat(object stage, string field)
        {
            object? v = stage.GetType().GetField(field, Inst)?.GetValue(stage);
            return v is float f ? f : 0f;
        }
    }
}
