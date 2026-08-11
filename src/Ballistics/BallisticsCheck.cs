using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using NuclearOption.Networking;
using UnityEngine;

namespace RocketPod.Ballistics
{

    internal static class BallisticsCheck
    {
        private sealed class Tracked
        {
            public Missile Missile = null!;
            public string RoundName = "?";
            public string SeekerType = "?";

            public GlobalPosition LaunchGlobal;
            public GlobalPosition PredictedImpactGlobal;
            public GlobalPosition LastKnownGlobal;

            public Vector3 LaunchVelocity;
            public float LaunchTime;
            public bool HasPrediction;
            public float PredictedTimeOfFlight;
            public float PredictedRange;
            public bool Scored;

            public bool IsControl;
            public float PredictedPeakSpeed;

            public string ModelSummary = "(not read)";

            public readonly List<Vector3> PredictedTrack = new List<Vector3>();
            public readonly List<Vector3> ActualTrack = new List<Vector3>();
            public float NextActualSample;

            public float PredictedRangeNoDrag = -1f;

            public float FuelAtLaunch = -1f;
            public float FuelAtEnd = -1f;
            public float BurnRateSeen = -1f;
            public float MassAtEnd = -1f;

            public int AlphaSamples;
            public float AlphaSum;
            public float AlphaMax;
            public float SpeedMax;
        }

        private static readonly Dictionary<int, Tracked> Live = new Dictionary<int, Tracked>();
        private static readonly List<int> Finished = new List<int>();

        private static ManualLogSource Log => Plugin.Log;

        internal static void Install(Harmony harmony)
        {
            harmony.PatchAll(typeof(BallisticsCheck));
            Log.LogInfo("[RocketPod] Ballistics check armed: every missile launch will be " +
                        "predicted at spawn and scored at impact.");
        }

        [HarmonyPatch(typeof(Spawner), nameof(Spawner.SpawnMissile),
            new[] { typeof(MissileDefinition), typeof(Vector3), typeof(Quaternion),
                    typeof(Vector3), typeof(Unit), typeof(Unit) })]
        [HarmonyPostfix]
        private static void AfterSpawnFromDefinition(
            Missile __result, Vector3 launchPosition, Vector3 velocity)
        {
            OnLaunch(__result, launchPosition, velocity);
        }

        [HarmonyPatch(typeof(Spawner), nameof(Spawner.SpawnMissile),
            new[] { typeof(GameObject), typeof(Vector3), typeof(Quaternion),
                    typeof(Vector3), typeof(Unit), typeof(Unit) })]
        [HarmonyPostfix]
        private static void AfterSpawnFromPrefab(
            Missile __result, Vector3 launchPosition, Vector3 velocity)
        {
            OnLaunch(__result, launchPosition, velocity);
        }

        private static void OnLaunch(Missile missile, Vector3 launchPosition, Vector3 velocity)
        {
            try
            {
                if (missile == null || !Plugin.CheckBallistics.Value)
                {
                    return;
                }

                string filter = Plugin.CheckOnlyWeapon.Value?.Trim() ?? string.Empty;
                string roundName = missile.definition != null
                    ? missile.definition.unitName
                    : missile.name;

                if (filter.Length > 0 &&
                    roundName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return;
                }

                var tracked = new Tracked
                {
                    Missile = missile,
                    RoundName = roundName,
                    LaunchGlobal = launchPosition.ToGlobalPosition(),
                    LastKnownGlobal = launchPosition.ToGlobalPosition(),
                    LaunchVelocity = velocity,
                    LaunchTime = Time.time,
                };

                try
                {
                    tracked.SeekerType = missile.GetSeekerType();
                }
                catch
                {
                    tracked.SeekerType = "(none)";
                }

                if (ReadLiveMotor(missile, out float fuel0, out _))
                {
                    tracked.FuelAtLaunch = fuel0;
                }

                Predict(tracked);
                Live[missile.GetInstanceID()] = tracked;
            }
            catch (Exception ex)
            {
                Log.LogError($"[RocketPod] OnLaunch failed: {ex.Message}");
            }
        }

        private static void Predict(Tracked t)
        {
            TrajectorySolver.RoundSpec? spec = RoundSpecFactory.FromMissile(t.Missile, Log);
            if (spec == null)
            {
                return;
            }

            float liveFinArea = -1f;
            float liveMass = -1f;
            try
            {
                var fCurrent = typeof(Missile).GetField("currentFinArea",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (fCurrent != null)
                {
                    liveFinArea = (float)fCurrent.GetValue(t.Missile);
                }
                if (t.Missile.rb != null)
                {
                    liveMass = t.Missile.rb.mass;
                }
            }
            catch
            {

            }

            var motors = new StringBuilder();
            foreach (TrajectorySolver.MotorStage m in spec.Motors)
            {
                motors.Append($"[thrust={m.Thrust:F0}N fuel={m.FuelMass:F1}kg " +
                              $"burn={m.BurnTime:F1}s] ");
            }

            t.ModelSummary =
                $"mass={spec.LaunchMass:F1} kg (live rb.mass={liveMass:F1})  " +
                $"finArea={spec.FinArea:F3} m2 (live currentFinArea={liveFinArea:F3})  " +
                $"Cd0={spec.DragCoefficientAtZeroAlpha:F4}  " +
                $"supersonicDrag={spec.SupersonicDrag:F3}  " +
                $"topSpeed={(spec.TopSpeed > 1e6f ? "none" : spec.TopSpeed.ToString("F0"))}  " +
                $"rho@launch={spec.AirDensity(t.LaunchGlobal.y * 0.001f):F4}  " +
                $"motors: {motors}";

            Vector3 launchGlobal = t.LaunchGlobal.AsVector3();

            float groundY = ToGlobalY(SampleGround(t.LaunchGlobal.ToLocalPosition()));
            TrajectorySolver.Result r = TrajectorySolver.Integrate(
                spec, launchGlobal, t.LaunchVelocity, groundY);

            t.PredictedTrack.Clear();
            TrajectorySolver.Integrate(
                spec, launchGlobal, t.LaunchVelocity, groundY,
                sample: (time, pos, vel) =>
                    t.PredictedTrack.Add(new Vector3(time, pos.y, vel.magnitude)));

            TrajectorySolver.Result noDrag = TrajectorySolver.Integrate(
                spec, launchGlobal, t.LaunchVelocity, groundY, dragScale: 0f);
            if (noDrag.Hit)
            {
                Vector3 dn = noDrag.ImpactPoint - launchGlobal;
                t.PredictedRangeNoDrag = new Vector2(dn.x, dn.z).magnitude;
            }

            if (r.Hit)
            {
                Vector3 impactLocal = new GlobalPosition(r.ImpactPoint).ToLocalPosition();
                float refined = ToGlobalY(SampleGround(impactLocal));
                if (Mathf.Abs(refined - groundY) > 1f)
                {
                    r = TrajectorySolver.Integrate(
                        spec, launchGlobal, t.LaunchVelocity, refined);
                }
            }

            if (!r.Hit)
            {
                return;
            }

            Vector3 d = r.ImpactPoint - launchGlobal;
            t.HasPrediction = true;
            t.PredictedImpactGlobal = new GlobalPosition(r.ImpactPoint);
            t.PredictedTimeOfFlight = r.TimeOfFlight;
            t.PredictedRange = new Vector2(d.x, d.z).magnitude;
            t.PredictedPeakSpeed = r.PeakSpeed;
        }

        internal static bool FireControlRound()
        {
            if (!NetworkManagerNuclearOption.i.Server.Active)
            {
                Log.LogWarning("[RocketPod] Control round: not the server. Missile physics runs " +
                               "under LocalSim, which is server-only. Host the mission.");
                return false;
            }

            if (!GameManager.GetLocalAircraft(out Aircraft aircraft) || aircraft == null)
            {
                Log.LogWarning("[RocketPod] Control round: no local aircraft. Spawner.SpawnMissile " +
                               "dereferences owner.transform without a null check.");
                return false;
            }

            string wanted = Plugin.ControlRoundName.Value?.Trim() ?? string.Empty;
            MissileDefinition? def = null;
            var available = new List<string>();

            foreach (MissileDefinition candidate in
                     GameData.EncyclopediaOrNull()?.missiles ?? new List<MissileDefinition>())
            {
                if (candidate == null)
                {
                    continue;
                }
                available.Add(candidate.unitName);
                if (wanted.Length > 0 &&
                    candidate.unitName.IndexOf(wanted, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    def = candidate;
                    break;
                }
            }

            if (def == null)
            {
                Log.LogWarning($"[RocketPod] Control round: no MissileDefinition matching " +
                               $"\"{wanted}\". Available: {string.Join(", ", available.ToArray())}");
                return false;
            }

            float elevation = Plugin.ControlRoundElevation.Value;
            Vector3 heading = aircraft.transform.forward;
            Vector3 flat = new Vector3(heading.x, 0f, heading.z).normalized;
            Vector3 axis = Vector3.Cross(Vector3.up, flat);
            Vector3 dir = Quaternion.AngleAxis(-elevation, axis) * flat;

            Vector3 launchPos = aircraft.transform.position + dir * 12f;
            Vector3 launchVel = aircraft.rb.velocity + dir * Plugin.ControlRoundBoost.Value;

            Missile missile = NetworkSceneSingleton<Spawner>.i.SpawnMissile(
                def, launchPos, Quaternion.LookRotation(dir), launchVel, null, aircraft);

            if (missile == null)
            {
                Log.LogWarning("[RocketPod] Control round: SpawnMissile returned null.");
                return false;
            }

            missile.SetTorque(0f, 0f);

            MissileSeeker? seeker = missile.GetComponent<MissileSeeker>();
            if (seeker != null)
            {
                UnityEngine.Object.Destroy(seeker);
            }

            if (Live.TryGetValue(missile.GetInstanceID(), out Tracked t))
            {
                t.IsControl = true;
                Log.LogInfo($"[RocketPod] Control round away: \"{t.RoundName}\" at " +
                            $"{elevation:F0} deg elevation, {launchVel.magnitude:F0} m/s. " +
                            $"Predicted range {t.PredictedRange:F0} m, ToF " +
                            $"{t.PredictedTimeOfFlight:F1} s.");
                return true;
            }

            Log.LogWarning("[RocketPod] Control round spawned but was not tracked - is " +
                           "CheckOnlyWeapon filtering it out?");
            return false;
        }

        private static bool ReadLiveMotor(Missile missile, out float fuelMass, out float burnRate)
        {
            fuelMass = -1f;
            burnRate = -1f;
            try
            {
                const System.Reflection.BindingFlags flags =
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Public;

                var fMotors = typeof(Missile).GetField("motors", flags);
                if (fMotors?.GetValue(missile) is not Array motors || motors.Length == 0)
                {
                    return false;
                }

                object? first = motors.GetValue(0);
                if (first == null)
                {
                    return false;
                }

                Type mt = first.GetType();
                var fFuel = mt.GetField("fuelMass", flags);
                var fRate = mt.GetField("burnRate", flags);
                if (fFuel == null || fRate == null)
                {
                    return false;
                }

                fuelMass = (float)fFuel.GetValue(first);
                burnRate = (float)fRate.GetValue(first);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static float ToGlobalY(float localY)
        {
            return localY - Datum.originPosition.y;
        }

        private static float SampleGround(Vector3 near)
        {
            Vector3 from = new Vector3(near.x, near.y + 4000f, near.z);
            if (Physics.Raycast(from, Vector3.down, out RaycastHit hit, 20000f,
                                PhysicsLayers.StaticsMask))
            {
                return hit.point.y;
            }
            return Datum.LocalSeaY;
        }

        internal static void SampleFlight()
        {
            if (Live.Count == 0)
            {
                return;
            }

            Finished.Clear();

            foreach (KeyValuePair<int, Tracked> kv in Live)
            {
                Tracked t = kv.Value;
                if (t.Missile == null || t.Missile.disabled)
                {
                    Finished.Add(kv.Key);
                    continue;
                }

                t.LastKnownGlobal = t.Missile.GlobalPosition();

                float age = Time.time - t.LaunchTime;
                if (age >= t.NextActualSample)
                {
                    t.NextActualSample += 1f;
                    t.ActualTrack.Add(new Vector3(
                        age, t.LastKnownGlobal.y,
                        t.Missile.rb != null ? t.Missile.rb.velocity.magnitude : 0f));
                }

                if (ReadLiveMotor(t.Missile, out float fuelNow, out float rateNow))
                {
                    t.FuelAtEnd = fuelNow;
                    t.BurnRateSeen = rateNow;
                }
                if (t.Missile.rb != null)
                {
                    t.MassAtEnd = t.Missile.rb.mass;
                }

                if (!t.Scored && CrossedGround(t.Missile))
                {
                    Score(t, t.LastKnownGlobal, hasImpact: true, "ground contact");
                    Finished.Add(kv.Key);
                    continue;
                }

                Vector3 v = t.Missile.rb != null ? t.Missile.rb.velocity : Vector3.zero;

                if (v.sqrMagnitude > 1f)
                {
                    float alpha = Vector3.Angle(t.Missile.transform.forward, v);
                    t.AlphaSamples++;
                    t.AlphaSum += alpha;
                    t.AlphaMax = Mathf.Max(t.AlphaMax, alpha);
                    t.SpeedMax = Mathf.Max(t.SpeedMax, v.magnitude);
                }

                if (t.IsControl && v.sqrMagnitude > 1f && t.Missile.rb != null)
                {
                    Quaternion along = Quaternion.LookRotation(v);
                    t.Missile.SetTorque(0f, 0f);
                    t.Missile.rb.MoveRotation(along);
                    t.Missile.transform.rotation = along;
                }

                if (Time.time - t.LaunchTime > 300f)
                {
                    Finished.Add(kv.Key);
                }
            }

            foreach (int id in Finished)
            {
                if (Live.TryGetValue(id, out Tracked t) && !t.Scored)
                {

                    Score(t, t.LastKnownGlobal, hasImpact: true, "last known position");
                }
                Live.Remove(id);
            }
        }

        private static bool CrossedGround(Missile missile)
        {
            Vector3 p = missile.transform.position;
            if (Physics.Raycast(p + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 4f,
                                PhysicsLayers.StaticsMask))
            {
                return p.y <= hit.point.y + 0.5f;
            }
            return missile.GlobalPosition().y <= 0f;
        }

        [HarmonyPatch(typeof(Missile), nameof(Missile.Detonate))]
        [HarmonyPrefix]
        private static void OnDetonate(Missile __instance)
        {
            try
            {
                if (__instance == null)
                {
                    return;
                }
                int id = __instance.GetInstanceID();
                if (!Live.TryGetValue(id, out Tracked t) || t.Scored)
                {
                    return;
                }
                Score(t, __instance.GlobalPosition(), hasImpact: true, "detonation");
                Live.Remove(id);
            }
            catch (Exception ex)
            {
                Log.LogError($"[RocketPod] OnDetonate failed: {ex.Message}");
            }
        }

        private static void Score(Tracked t, GlobalPosition actualImpact, bool hasImpact, string how)
        {
            t.Scored = true;

            var sb = new StringBuilder();
            string inv(float f, int dp = 1) => f.ToString("F" + dp, CultureInfo.InvariantCulture);

            float meanAlpha = t.AlphaSamples > 0 ? t.AlphaSum / t.AlphaSamples : 0f;

            sb.AppendLine($"[BallisticsCheck] round=\"{t.RoundName}\" seeker={t.SeekerType}" +
                          (t.IsControl ? "  [CONTROL ROUND: unsteered, held nose-to-velocity]" : ""));

            sb.AppendLine($"[BallisticsCheck]   launch speed={inv(t.LaunchVelocity.magnitude)} m/s  " +
                          $"alt={inv(t.LaunchGlobal.y)} m above sea  ended via {how}");

            if (!t.HasPrediction)
            {
                sb.AppendLine("[BallisticsCheck]   NO PREDICTION - the solver could not read this " +
                              "round's flight model, or it never came down.");
            }
            else if (!hasImpact)
            {
                sb.AppendLine($"[BallisticsCheck]   predicted range={inv(t.PredictedRange)} m " +
                              $"ToF={inv(t.PredictedTimeOfFlight)} s, but the round vanished " +
                              "without detonating - not scored.");
            }
            else
            {
                float actualTof = Time.time - t.LaunchTime;
                Vector3 dActual = actualImpact - t.LaunchGlobal;
                float actualRange = new Vector2(dActual.x, dActual.z).magnitude;
                float miss = (actualImpact - t.PredictedImpactGlobal).magnitude;
                float rangeErr = t.PredictedRange - actualRange;
                float pct = actualRange > 1f ? rangeErr / actualRange * 100f : 0f;

                sb.AppendLine($"[BallisticsCheck]   range  predicted={inv(t.PredictedRange)} m  " +
                              $"actual={inv(actualRange)} m  error={inv(rangeErr)} m " +
                              $"({inv(pct)}%)");
                sb.AppendLine($"[BallisticsCheck]   ToF    predicted={inv(t.PredictedTimeOfFlight)} s  " +
                              $"actual={inv(actualTof)} s");
                sb.AppendLine($"[BallisticsCheck]   miss distance={inv(miss)} m");

                if (t.PredictedRangeNoDrag > 0f)
                {
                    float toReal = Mathf.Abs(actualRange - t.PredictedRange);
                    float toNoDrag = Mathf.Abs(actualRange - t.PredictedRangeNoDrag);
                    sb.AppendLine($"[BallisticsCheck]   drag-free twin would reach " +
                                  $"{inv(t.PredictedRangeNoDrag)} m");
                    if (toNoDrag < toReal)
                    {
                        sb.AppendLine("[BallisticsCheck]   *** THIS ROUND FLEW WITHOUT DRAG. It " +
                                      "matches the drag-free prediction better than the real one. " +
                                      "This is NOT a solver error. Two causes, and the model line " +
                                      "below tells them apart:");
                        sb.AppendLine("[BallisticsCheck]       1. Cd0=0.0000 -> the round HAS no " +
                                      "drag. dragCurve is empty or zero-valued on the prefab, so " +
                                      "dragCurve.Evaluate() returns 0 at every angle and " +
                                      "ApplyAero computes no drag force. supersonicDrag cannot " +
                                      "save it: that value MULTIPLIES the drag vector, and a " +
                                      "multiple of zero is zero. Fix it in Unity.");
                        sb.AppendLine("[BallisticsCheck]       2. Cd0 nonzero -> ApplyAero did not " +
                                      "RUN. Check Player.log for a NullReferenceException in " +
                                      "Missile.ServerFixedUpdate; a throw in seeker.Seek() or " +
                                      "Steering() aborts the method before ApplyAero.");
                    }
                }
            }

            sb.AppendLine($"[BallisticsCheck]   alpha  mean={inv(meanAlpha)} deg  " +
                          $"max={inv(t.AlphaMax)} deg  over {t.AlphaSamples} samples");
            sb.AppendLine($"[BallisticsCheck]   model  {t.ModelSummary}");
            sb.AppendLine($"[BallisticsCheck]   motor  fuel {inv(t.FuelAtLaunch, 2)} -> " +
                          $"{inv(t.FuelAtEnd, 2)} kg  liveBurnRate={inv(t.BurnRateSeen, 3)} kg/s  " +
                          $"endMass={inv(t.MassAtEnd)} kg" +
                          (t.BurnRateSeen == 0f
                              ? "   <-- burnRate is ZERO: Motor.burnRate is never initialised " +
                                "unless GetRemainingBurnTime or GetRemainingDeltaV is called, so " +
                                "fuelMass never drops and the motor burns for the whole flight."
                              : string.Empty));
            sb.AppendLine($"[BallisticsCheck]   speed  peak predicted={inv(t.PredictedPeakSpeed)} m/s  " +
                          $"actual={inv(t.SpeedMax)} m/s  " +
                          $"error={inv(t.PredictedPeakSpeed - t.SpeedMax)} m/s");
            sb.AppendLine("[BallisticsCheck]   " + Verdict(t, meanAlpha));

            if (t.IsControl && t.PredictedTrack.Count > 0 && t.ActualTrack.Count > 0)
            {
                sb.AppendLine("[BallisticsCheck]   track (t / alt pred vs act / speed pred vs act):");
                int rows = Mathf.Min(t.PredictedTrack.Count, t.ActualTrack.Count);
                for (int i = 0; i < rows; i += Mathf.Max(1, rows / 12))
                {
                    Vector3 p = t.PredictedTrack[i];
                    Vector3 a = t.ActualTrack[i];
                    sb.AppendLine(
                        $"[BallisticsCheck]     t={inv(p.x)}s  alt {inv(p.y, 0)} vs {inv(a.y, 0)} " +
                        $"(d={inv(p.y - a.y, 0)})  spd {inv(p.z, 0)} vs {inv(a.z, 0)} " +
                        $"(d={inv(p.z - a.z, 0)})");
                }
            }

            string text = sb.ToString();
            Log.LogInfo(text);
            Append(text);
        }

        private static string Verdict(Tracked t, float meanAlpha)
        {
            if (!t.HasPrediction)
            {
                return "verdict: no prediction to judge.";
            }

            if (t.IsControl)
            {
                return meanAlpha > 6f
                    ? $"verdict: CONTROL, but alpha averaged {meanAlpha:F1} deg - the " +
                      "nose-to-velocity hold is not working, so this is not a clean test."
                    : "verdict: CONTROL - unsteered and nose-to-velocity, so this miss is the " +
                      "integration's and nothing else's.";
            }

            if (meanAlpha > 12f)
            {
                return $"verdict: alpha averaged {meanAlpha:F1} deg, so this round did NOT fly " +
                       "along its velocity and the zero-alpha assumption does not apply to it. " +
                       "Any miss here is the assumption's, not the integration's.";
            }

            if (t.SeekerType != "(none)" && t.SeekerType != "?")
            {
                return $"verdict: this round is steered by a {t.SeekerType} seeker, which the " +
                       "solver does not model. Treat the miss as an upper bound on the " +
                       "integration error, not a measurement of it.";
            }

            return "verdict: low alpha and unsteered - this miss is a fair measure of the " +
                   "integration.";
        }

        private static void Append(string text)
        {
            try
            {
                string path = Path.Combine(Paths.BepInExRootPath, Plugin.CheckFileName.Value);
                File.AppendAllText(path, text);
            }
            catch (Exception ex)
            {
                Log.LogError($"[RocketPod] Could not write the ballistics report: {ex.Message}");
            }
        }
    }
}
