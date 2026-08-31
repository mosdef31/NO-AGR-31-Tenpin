using System;
using System.Collections.Generic;
using HarmonyLib;
using RocketPod.Ballistics;
using Shared.Ballistics;
using UnityEngine;

namespace RocketPod
{

    internal static class AiEmployment
    {

        private sealed class Brain
        {
            internal float NextSolveTime;
            internal float ElevationDegrees;
            internal bool HasSolution;
            internal bool HasSolutionEver;
            internal bool Lofted;
            internal float NextClassifyTime;
            internal bool TargetWorthSalvo;
            internal PersistentID ClassifiedTarget;

            internal int RoundsThisPass;

            internal int AmmoAtPassStart;

            internal int AmmoAtTargetStart;
            internal float PassStartedAt;
            internal int RoundsOnTarget;
            internal float NextEngageTime;
            internal float SteadySince;
            internal float CommandedElevation;
            internal float TimeOfFlight;
            internal bool CommandStarted;
            internal bool RoutedPrediction;
            internal float TrimDegrees;
            internal float LastErrorDegrees;
            internal float ErrorRate;
            internal bool HasLastError;
            internal float LastPromisingAim;
            internal float BurstUntil;
            internal int ColumnSize;
            internal int Bursts;
            internal float AttackStarted;

            internal float SolutionSince;

            internal float MinRangeSince;
            internal readonly HashSet<PersistentID> Engaged = new();
        }

        private static readonly Dictionary<AIPilotCombatModes, Brain> _brains = new();

        private static readonly Dictionary<string, float> _nextReport = new();

        [ThreadStatic] private static string? _who;

        internal static void SetReporter(Aircraft aircraft)
        {
            _who = aircraft != null
                ? (string.IsNullOrEmpty(aircraft.UniqueName) ? aircraft.unitName : aircraft.UniqueName)
                : null;
        }

        internal static void Report(string reason)
        {
            if (!Plugin.AiReport.Value) return;

            string who = _who ?? "unknown";

            string key = who + "|" + reason;

            float now = Time.timeSinceLevelLoad;
            if (_nextReport.TryGetValue(key, out float next) && now < next) return;

            _nextReport[key] = now + Plugin.AiReportSeconds.Value;
            Plugin.Log.LogInfo($"[Tenpin] {who}: held fire - {reason}");
        }

        internal static void ReportEvent(string what)
        {
            if (!Plugin.AiReport.Value) return;
            Plugin.Log.LogInfo($"[Tenpin] {_who ?? "unknown"}: {what}");
        }

        private static readonly List<Unit> _targetBuffer = new();

        internal static List<Unit> TargetBuffer => _targetBuffer;
        private static float _nextSweep;
        private static bool _loggedProfile;
        private static bool _loggedReached;

        private static Brain BrainFor(AIPilotCombatModes state)
        {
            if (!_brains.TryGetValue(state, out Brain brain))
            {
                brain = new Brain();
                _brains[state] = brain;
            }

            if (Time.timeSinceLevelLoad > _nextSweep)
            {
                _nextSweep = Time.timeSinceLevelLoad + 60f;
                var dead = new List<AIPilotCombatModes>();
                foreach (KeyValuePair<AIPilotCombatModes, Brain> kv in _brains)
                {
                    if (Time.timeSinceLevelLoad - kv.Value.PassStartedAt > 300f) dead.Add(kv.Key);
                }
                foreach (AIPilotCombatModes k in dead) _brains.Remove(k);
            }

            return brain;
        }

        private static readonly System.Reflection.MethodInfo? SetCombatModeMethod =
            AccessTools.Method(typeof(AIPilotCombatModes), "SetCombatMode");
        private static readonly Type? AttackModeType =
            AccessTools.Inner(typeof(AIPilotCombatModes), "AttackMode");

        private const int BreakOffAttackMode = 1;

        private static void BreakOff(AIPilotCombatModes state, Brain brain, string why)
        {
            var t = Traverse.Create(state);
            t.Field<float>("breakOffTimer").Value = Plugin.AiEgressSeconds.Value;

            if (SetCombatModeMethod == null || AttackModeType == null)
            {
                Plugin.Log.LogWarning(
                    "[Tenpin] Could not resolve AIPilotCombatModes.SetCombatMode, so AI flights " +
                    "cannot be told to break off after a pass. They will keep attacking until the " +
                    "stock logic stops them.");
                return;
            }

            SetCombatModeMethod.Invoke(
                state, new[] { Enum.ToObject(AttackModeType, BreakOffAttackMode) });

            brain.NextEngageTime = Time.timeSinceLevelLoad + Plugin.AiEgressSeconds.Value;
            brain.RoundsThisPass = 0;
            brain.RoundsOnTarget = 0;
            brain.AmmoAtPassStart = -1;
            brain.AmmoAtTargetStart = -1;
            brain.TrimDegrees = 0f;
            brain.CommandStarted = false;
            brain.HasLastError = false;
            brain.ErrorRate = 0f;
            brain.BurstUntil = 0f;
            brain.Bursts = 0;
            brain.AttackStarted = 0f;
            brain.SolutionSince = 0f;
            brain.MinRangeSince = 0f;
            brain.Engaged.Clear();

            Report($"breaking off - {why}");

            Traverse.Create(state).Field<string>("stateDisplayName").Value =
                "AGR-31 egress after salvo";
        }

        private static Unit? NextInCluster(Unit current, Aircraft shooter, Brain brain)
        {
            FactionHQ? hq = shooter != null ? shooter.NetworkHQ : null;
            if (hq == null || current == null) return null;

            Vector3 centre = current.GlobalPosition().AsVector3();
            float radius = Plugin.AiClusterRadius.Value * 2f;
            float radiusSq = radius * radius;

            Unit? best = null;
            float bestSq = float.MaxValue;

            foreach (KeyValuePair<PersistentID, TrackingInfo> kv in hq.trackingDatabase)
            {
                if (!kv.Value.TryGetUnit(out Unit unit) || unit == null || unit.disabled) continue;
                if (unit == current) continue;
                if (!(unit is GroundVehicle) && !(unit is Ship) && !(unit is Building)) continue;
                if (brain.Engaged.Contains(unit.persistentID)) continue;

                float sq = (unit.GlobalPosition().AsVector3() - centre).sqrMagnitude;
                if (sq > radiusSq || sq >= bestSq) continue;

                best = unit;
                bestSq = sq;
            }

            return best;
        }

        private static int SalvoFor(Unit target, float far)
        {
            float rounds = Mathf.Lerp(Plugin.AiSalvoNear.Value, Plugin.AiSalvoFar.Value, far);

            bool overwhelm = NeedsOverwhelming(target);
            if (overwhelm) rounds *= Plugin.AiOverwhelmFactor.Value;

            int cap = overwhelm
                ? Mathf.RoundToInt(PluginInfo.Rounds18 * Plugin.AiOverwhelmFactor.Value)
                : PluginInfo.Rounds18;

            return Mathf.Clamp(Mathf.RoundToInt(rounds), 1, cap);
        }

        internal static Vector3 PredictPosition(Unit target, float seconds, out bool routed)
            => TargetLead.PredictPosition(target, seconds, out routed);

        private static Vector3 ColumnAimPoint(Unit target, Aircraft shooter, float tof,
                                              out int count)
        {
            count = 0;

            FactionHQ? hq = shooter != null ? shooter.NetworkHQ : null;
            Vector3 anchor = PredictPosition(target, tof, out _);
            if (hq == null) { count = 1; return anchor; }

            float radius = Plugin.AiClusterRadius.Value;
            float radiusSq = radius * radius;

            Vector3 sum = Vector3.zero;

            foreach (KeyValuePair<PersistentID, TrackingInfo> kv in hq.trackingDatabase)
            {
                if (!kv.Value.TryGetUnit(out Unit unit) || unit == null || unit.disabled) continue;
                if (!(unit is GroundVehicle) && !(unit is Ship)) continue;

                Vector3 here = unit.GlobalPosition().AsVector3();
                if ((here - target.GlobalPosition().AsVector3()).sqrMagnitude > radiusSq) continue;

                sum += PredictPosition(unit, tof, out _);
                count++;

                if (count >= 24) break;
            }

            if (count == 0) { count = 1; return anchor; }

            return sum / count;
        }

        private static void CountRounds(Brain brain, WeaponStation station)
        {
            int ammo = station.Ammo;

            if (brain.AmmoAtPassStart < 0 || ammo > brain.AmmoAtPassStart)
                brain.AmmoAtPassStart = ammo;

            if (brain.AmmoAtTargetStart < 0 || ammo > brain.AmmoAtTargetStart)
                brain.AmmoAtTargetStart = ammo;

            brain.RoundsThisPass = Mathf.Max(0, brain.AmmoAtPassStart - ammo);
            brain.RoundsOnTarget = Mathf.Max(0, brain.AmmoAtTargetStart - ammo);
        }

        private static float CurrentElevation(Aircraft aircraft)
        {
            Vector3 v = aircraft.rb != null ? aircraft.rb.velocity : aircraft.transform.forward;
            Vector3 flat = new Vector3(v.x, 0f, v.z);
            if (flat.sqrMagnitude < 1e-4f) return 0f;
            return Mathf.Atan2(v.y, flat.magnitude) * Mathf.Rad2Deg;
        }

        internal static bool IsStationaryTarget(Unit target) => IsStationary(target);

        private static bool IsStationary(Unit target)
        {
            if (target == null) return false;
            if (target is Building) return true;
            return target.rb == null || target.speed < 2f;
        }

        private static bool NeedsOverwhelming(Unit target)
        {
            if (target == null || target.definition == null) return false;
            if (target is Ship) return true;
            if (target is Building) return true;

            return target.definition.roleIdentity.antiAir > 0f;
        }

        internal static bool WorthSalvo(Unit target, Aircraft shooter)
        {
            if (target == null || target.definition == null) return false;

            if (target is Aircraft || target is Missile) return false;

            if (target is Building) return true;
            if (target is Ship) return true;

            if (target is GroundVehicle)
            {

                if (target.definition.roleIdentity.antiAir > 0f) return true;

                return ClusterCount(target, shooter) >= 3;
            }

            return false;
        }

        private static int ClusterCount(Unit target, Aircraft shooter)
        {
            FactionHQ? hq = shooter != null ? shooter.NetworkHQ : null;
            if (hq == null) return 0;

            Vector3 centre = target.GlobalPosition().AsVector3();
            float radius = Plugin.AiClusterRadius.Value;
            float radiusSq = radius * radius;
            int count = 0;

            foreach (KeyValuePair<PersistentID, TrackingInfo> kv in hq.trackingDatabase)
            {
                if (!kv.Value.TryGetUnit(out Unit unit) || unit == null || unit.disabled) continue;
                if (!(unit is GroundVehicle) && !(unit is Building)) continue;
                if ((unit.GlobalPosition().AsVector3() - centre).sqrMagnitude > radiusSq) continue;
                count++;
                if (count >= 3) return count;
            }

            return count;
        }

        private static TrajectorySolver.RoundSpec? _spec;
        private static Missile? _specSource;

        internal static TrajectorySolver.RoundSpec? Spec()
        {
            Missile? prefab = EncyclopediaRegistration.ResolvedMissile?.unitPrefab != null
                ? EncyclopediaRegistration.ResolvedMissile.unitPrefab.GetComponent<Missile>()
                : null;
            if (prefab == null) return null;

            if (_spec == null || !ReferenceEquals(prefab, _specSource))
            {
                _spec = RoundSpecFactory.FromMissile(prefab, Plugin.Log);
                _specSource = prefab;
            }
            return _spec;
        }

        internal static bool RunAttack(AIPilotCombatModes state, Pilot pilot,
                                       Aircraft aircraft, WeaponStation station)
        {
            Unit target = Traverse.Create(state).Field<Unit>("currentTarget").Value;
            if (target == null || target.disabled) { Report("no target"); return false; }
            if (station.Ammo <= 0) { Report("pod empty"); return false; }

            FactionHQ hq = aircraft.NetworkHQ;
            if (hq == null || !hq.TryGetKnownPosition(target, out GlobalPosition known))
            {
                Report("the faction has no known position for the target");
                return false;
            }

            TrajectorySolver.RoundSpec? spec = Spec();
            if (spec == null)
            {
                Report("no flight model - RoundSpecFactory could not read the round");
                return false;
            }

            Brain brain = BrainFor(state);
            SetReporter(aircraft);

            if (!_loggedReached)
            {
                _loggedReached = true;
                Plugin.Log.LogInfo(
                    "[Tenpin] AI employment reached the firing branch for the first time. If no " +
                    "shot follows, the lines below say which test declined it. Logged once.");
            }
            Vector3 targetPos = known.AsVector3();
            Vector3 here = aircraft.transform.GlobalPosition().AsVector3();
            Vector3 flat = targetPos - here;
            flat.y = 0f;
            float range = flat.magnitude;
            if (range < 1f) return true;

            Vector3 launch = here;
            Vector3 velocity = aircraft.rb.velocity;

            if (Time.timeSinceLevelLoad < brain.NextEngageTime)
            {
                Report("egressing after a pass - will re-engage from range");
                return true;
            }

            if (brain.AttackStarted <= 0f) brain.AttackStarted = Time.timeSinceLevelLoad;

            CountRounds(brain, station);

            if (Time.timeSinceLevelLoad - brain.AttackStarted > Plugin.AiPassSeconds.Value)
            {
                BreakOff(state, brain,
                         $"{Plugin.AiPassSeconds.Value:0} s in the attack and " +
                         $"{brain.RoundsThisPass} round(s) away - that is the pass");
                return true;
            }

            if (range < Plugin.AiAbortRange.Value)
            {
                BreakOff(state, brain, $"overflown - {range:0} m from the target");
                return true;
            }

            if (range < Plugin.AiPreferredMinRange.Value)
            {
                if (brain.MinRangeSince <= 0f) brain.MinRangeSince = Time.timeSinceLevelLoad;
            }
            else
            {
                brain.MinRangeSince = 0f;
            }

            float quietSince = Mathf.Max(brain.LastPromisingAim, brain.MinRangeSince);

            if (range < Plugin.AiPreferredMinRange.Value && station.Ammo > 0 &&
                brain.MinRangeSince > 0f &&
                Time.timeSinceLevelLoad - quietSince > Plugin.AiGiveUpSeconds.Value)
            {
                BreakOff(state, brain, $"inside {Plugin.AiPreferredMinRange.Value:0} m with " +
                                       $"{station.Ammo} round(s) left and no solution developing " +
                                       $"for {Plugin.AiGiveUpSeconds.Value:0} s - opening the range");
                return true;
            }

            Vector3 lead = PredictPosition(target, brain.TimeOfFlight, out bool routed);
            brain.RoutedPrediction = routed;

            if (Plugin.AiConvoyAim.Value && !IsStationary(target))
            {
                Vector3 column = ColumnAimPoint(target, aircraft, brain.TimeOfFlight,
                                                out int inColumn);
                if (inColumn >= Plugin.AiConvoyMinVehicles.Value)
                {
                    lead = column;
                    brain.ColumnSize = inColumn;
                }
                else
                {
                    brain.ColumnSize = 0;
                }
            }

            flat = lead - here;
            flat.y = 0f;
            range = flat.magnitude;
            targetPos = lead;

            if (Time.timeSinceLevelLoad > brain.NextSolveTime)
            {
                brain.NextSolveTime = Time.timeSinceLevelLoad + Plugin.AiSolveInterval.Value;

                float? low = TrajectorySolver.SolveLaunchElevation(
                    spec, launch, aircraft.transform.forward, velocity.magnitude,
                    range, groundY: targetPos.y, loft: false);

                float? high = low ?? TrajectorySolver.SolveLaunchElevation(
                    spec, launch, aircraft.transform.forward, velocity.magnitude,
                    range, groundY: targetPos.y, loft: true);

                brain.HasSolution = high.HasValue;
                brain.Lofted = !low.HasValue;

                float solved = high ?? 0f;
                brain.ElevationDegrees = brain.HasSolutionEver
                    ? Mathf.Lerp(brain.ElevationDegrees, solved, Plugin.AiArcSmoothing.Value)
                    : solved;
                brain.HasSolutionEver = true;
            }

            if (!brain.HasSolution)
            {

                brain.SolutionSince = 0f;
                Report($"no firing solution at {range:0} m - neither arc reaches");
                return true;
            }

            if (brain.SolutionSince <= 0f) brain.SolutionSince = Time.timeSinceLevelLoad;

            if (range >= Plugin.AiSalvoEconomyRange.Value)
            {
                if (!target.persistentID.Equals(brain.ClassifiedTarget) ||
                    Time.timeSinceLevelLoad > brain.NextClassifyTime)
                {
                    brain.ClassifiedTarget = target.persistentID;
                    brain.NextClassifyTime = Time.timeSinceLevelLoad + 5f;
                    brain.TargetWorthSalvo = WorthSalvo(target, aircraft);
                }

                if (!brain.TargetWorthSalvo)
                {
                    Report($"'{target.unitName}' is not worth a salvo at {range:0} m");
                    return true;
                }
            }

            if (!brain.CommandStarted)
            {
                brain.CommandStarted = true;
                brain.CommandedElevation = CurrentElevation(aircraft);
            }

            if (!brain.CommandStarted)
            {
                brain.CommandStarted = true;
                brain.CommandedElevation = CurrentElevation(aircraft);
            }

            brain.CommandedElevation = Mathf.MoveTowards(
                brain.CommandedElevation, brain.ElevationDegrees,
                Plugin.AiAimSlewDegreesPerSecond.Value * Time.deltaTime);

            Vector3 heading = flat.normalized;
            Vector3 axis = Vector3.Cross(Vector3.up, heading);
            Vector3 launchDir = Quaternion.AngleAxis(-brain.CommandedElevation, axis) * heading;

            GlobalPosition aim = aircraft.transform.GlobalPosition();
            aim += launchDir * Mathf.Max(range, Plugin.AiAimLeadMetres.Value);

            var t = Traverse.Create(state);
            t.Field<GlobalPosition>("destination").Value = aim;
            t.Field<float>("aimEffort").Value = Plugin.AiAimEffort.Value;

            t.Field<string>("stateDisplayName").Value =
                brain.Lofted
                    ? $"AGR-31 lofted salvo {range / 1000f:0.0} km"
                    : $"AGR-31 direct pass {range / 1000f:0.0} km";

            t.Field<bool>("aimVelocity").Value = true;

            TrajectorySolver.Result r = TerrainImpact.Solve(
                spec, launch, velocity, Plugin.AiSolverStepScale.Value,
                AimpointChannel.TrySampleGroundHeight, Plugin.SampleTerrainHeight.Value);

            if (!r.Hit)
            {
                Report($"the predicted trajectory never reaches the ground from {range:0} m");
                return true;
            }

            brain.TimeOfFlight = r.TimeOfFlight * Plugin.AiTimeOfFlightBias.Value;

            Vector3 downrange = targetPos - here;
            downrange.y = 0f;
            downrange = downrange.sqrMagnitude > 1f ? downrange.normalized : Vector3.forward;

            Vector3 calibrated = r.ImpactPoint - downrange * (Plugin.AiRangeBias.Value * range);

            Vector3 miss = calibrated - targetPos;
            miss.y = 0f;

            float reach = Mathf.Max(1f, Plugin.AiFullSalvoRange.Value);
            float far = Mathf.Clamp01(range / reach);

            float tolerance = range * (Plugin.AiGuidanceBudgetMilliradians.Value / 1000f)
                              * Plugin.AiGateMargin.Value;

            if (IsStationary(target)) tolerance *= Plugin.AiStationaryTolerance.Value;

            if (!brain.RoutedPrediction && !IsStationary(target))
            {
                tolerance *= Plugin.AiUnroutedTolerance.Value;
            }

            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            float missNow = miss.magnitude;

            float closingRate = brain.HasLastError ? (brain.LastErrorDegrees - missNow) / dt : 0f;
            brain.LastErrorDegrees = missNow;
            brain.HasLastError = true;

            brain.ErrorRate = Mathf.Lerp(brain.ErrorRate, closingRate,
                                         1f - Mathf.Exp(-dt / Plugin.AiTrimRateSmoothing.Value));

            float credit = Mathf.Clamp(
                brain.ErrorRate * Plugin.AiCrossingLead.Value,
                -tolerance, tolerance);

            float missWhenFired = Mathf.Abs(missNow - credit);

            if (missNow < tolerance * Plugin.AiPromisingFactor.Value)
            {
                brain.LastPromisingAim = Time.timeSinceLevelLoad;
            }

            bool inBurst = Time.timeSinceLevelLoad < brain.BurstUntil &&
                           missNow < tolerance * Plugin.AiCrossingCeiling.Value;

            if (!inBurst &&
                (missWhenFired > tolerance ||
                 missNow > tolerance * Plugin.AiCrossingCeiling.Value))
            {
                brain.SteadySince = 0f;
                Report($"aim is off by {missNow:0} m at {range:0} m, tolerance {tolerance:0} m " +
                       $"(arc {brain.ElevationDegrees:0.0} deg{(brain.Lofted ? ", lofted" : "")}, " +
                       $"commanding {brain.CommandedElevation:0.0} deg, " +
                       $"closing {brain.ErrorRate:0} m/s)");
                return true;
            }

            float rate = aircraft.rb != null
                ? aircraft.rb.angularVelocity.magnitude * Mathf.Rad2Deg
                : 0f;

            if (rate > Plugin.AiSteadyRateDegrees.Value)
            {
                brain.SteadySince = 0f;
                Report($"aim is good but the airframe is still moving at {rate:0.0} deg/s " +
                       $"(needs {Plugin.AiSteadyRateDegrees.Value:0.0})");
                return true;
            }

            if (brain.SteadySince <= 0f) brain.SteadySince = Time.timeSinceLevelLoad;

            float steadyFor = Time.timeSinceLevelLoad - brain.SteadySince;
            if (steadyFor < Plugin.AiSettleSeconds.Value)
            {
                Report($"settling - {steadyFor:0.00} s of {Plugin.AiSettleSeconds.Value:0.00} s " +
                       $"with the solution held");
                return true;
            }

            brain.PassStartedAt = Time.timeSinceLevelLoad;

            brain.BurstUntil = Mathf.Max(brain.BurstUntil,
                                         Time.timeSinceLevelLoad + Plugin.AiBurstSeconds.Value);

            int budget = SalvoFor(target, far);

            if (brain.RoundsThisPass > 0 && brain.BurstUntil > 0f &&
                Time.timeSinceLevelLoad > brain.BurstUntil)
            {
                brain.Bursts++;
                brain.Engaged.Add(target.persistentID);

                brain.BurstUntil = -1f;

                Unit? next = brain.Bursts < Plugin.AiBurstsPerApproach.Value &&
                             station.Ammo > 0 &&
                             range > Plugin.AiAbortRange.Value * 1.5f
                    ? NextInCluster(target, aircraft, brain)
                    : null;

                if (next != null)
                {
                    var tn = Traverse.Create(state);
                    tn.Field<Unit>("currentTarget").Value = next;

                    FactionHQ? hqNext = aircraft.NetworkHQ;
                    if (hqNext != null &&
                        hqNext.trackingDatabase.TryGetValue(next.persistentID, out TrackingInfo tin))
                    {
                        tn.Field<TrackingInfo>("currentTargetTracking").Value = tin;
                    }

                    brain.RoundsOnTarget = 0;
                    brain.AmmoAtTargetStart = station.Ammo;
                    brain.BurstUntil = 0f;
                    brain.HasLastError = false;
                    ReportEvent($"burst {brain.Bursts} complete, shifting to '{next.unitName}' " +
                                $"({station.Ammo} round(s) left)");
                    return true;
                }

                if (brain.Bursts < Plugin.AiBurstsPerApproach.Value &&
                    station.Ammo > 0 &&
                    brain.RoundsOnTarget < budget &&
                    range > Plugin.AiAbortRange.Value * 1.5f)
                {
                    brain.HasLastError = false;
                    ReportEvent($"burst {brain.Bursts} complete, holding the attack on " +
                                $"'{target.unitName}' for another ({station.Ammo} round(s) left, " +
                                $"{brain.RoundsOnTarget}/{budget} into this target)");
                    return true;
                }

                BreakOff(state, brain,
                         $"{brain.Bursts} burst(s) away, {station.Ammo} round(s) left - " +
                         "nothing more worth shooting from here");
                return true;
            }

            if (brain.RoundsThisPass == 0 &&
                brain.SolutionSince > 0f &&
                Time.timeSinceLevelLoad - brain.SolutionSince > Plugin.AiLoiterSeconds.Value)
            {
                BreakOff(state, brain,
                         $"no shot in {Plugin.AiLoiterSeconds.Value:0} s of being able to take " +
                         "one - resetting the approach");
                return true;
            }

            if (brain.RoundsOnTarget >= budget)
            {
                brain.Engaged.Add(target.persistentID);

                Unit? next = brain.Engaged.Count < Plugin.AiTargetsPerPass.Value
                    ? NextInCluster(target, aircraft, brain)
                    : null;

                if (next != null)
                {
                    var tt = Traverse.Create(state);
                    tt.Field<Unit>("currentTarget").Value = next;

                    FactionHQ? hqNow = aircraft.NetworkHQ;
                    if (hqNow != null &&
                        hqNow.trackingDatabase.TryGetValue(next.persistentID, out TrackingInfo info))
                    {
                        tt.Field<TrackingInfo>("currentTargetTracking").Value = info;
                    }

                    brain.RoundsOnTarget = 0;
                    brain.AmmoAtTargetStart = station.Ammo;
                    brain.TrimDegrees = 0f;
                    brain.HasLastError = false;
                    brain.ErrorRate = 0f;
                    Report($"shifting to '{next.unitName}' - {brain.Engaged.Count} target(s) " +
                           $"engaged this pass");
                    return true;
                }

                BreakOff(state, brain,
                         $"pass complete - {brain.Engaged.Count} target(s), " +
                         $"{brain.RoundsThisPass} round(s)");
                return true;
            }

            if (station.Ammo <= 0)
            {
                BreakOff(state, brain, "pods empty");
                return true;
            }

            aircraft.weaponManager.ClearTargetList();
            _targetBuffer.Clear();
            if (CombatAI.LookForMissileTargets(aircraft, target,
                    aircraft.weaponManager.currentWeaponStation, _targetBuffer) > 0)
            {
                ShotAudit.Expect(calibrated, targetPos, targetPos, range,
                                 brain.CommandedElevation, r.TimeOfFlight);

                pilot.Fire();
                Traverse.Create(state).Field<float>("lastFiredTime").Value = Time.timeSinceLevelLoad;

                if (!_loggedProfile)
                {
                    _loggedProfile = true;
                    Plugin.Log.LogInfo(
                        $"[Tenpin] AI employment is live. This shot was at {range:0} m on a " +
                        $"{(brain.Lofted ? "LOFTED" : "LOW")} arc of {brain.ElevationDegrees:0.0} deg, " +
                        $"predicted miss {miss.magnitude:0} m against a {tolerance:0} m tolerance, " +
                        $"salvo budget {budget} round(s)" +
                        (brain.ColumnSize > 1 ? $", aimed at a column of {brain.ColumnSize}" : "") +
                        $", aim point from " +
                        $"{(brain.RoutedPrediction ? "the target's OWN ROUTE" : "extrapolated heading")}. " +
                        "One continuous profile: the arc, the " +
                        "volume and the tolerance all scale with range, and the stock AI's own " +
                        "release test is nose alignment, which is meaningless for a round that " +
                        "does not steer. Logged once.");
                }
            }

            return true;
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  The firing branch, replaced for our station only.
    //
    //  A PREFIX ON ONE METHOD, not a state swap - see the note at the top of
    //  this file. `checkMode` is passed straight through to vanilla: mode
    //  selection, break-off, retreat and the ammo checks are all fine as they
    //  are, and the only thing wrong with the stock behaviour is WHEN it presses
    //  the trigger.
    // ═════════════════════════════════════════════════════════════════════════

    [HarmonyPatch(typeof(AIPilotCombatModes), "UseMissiles")]
    internal static class AIPilotCombatModes_UseMissiles_TenpinPatch
    {
        private static readonly AccessTools.FieldRef<AIPilotCombatModes, Unit> CurrentTarget =
            AccessTools.FieldRefAccess<AIPilotCombatModes, Unit>("currentTarget");
        private static readonly AccessTools.FieldRef<AIPilotCombatModes, float> TargetDist =
            AccessTools.FieldRefAccess<AIPilotCombatModes, float>("targetDist");
        private static readonly AccessTools.FieldRef<AIPilotCombatModes, float> LastFiredTime =
            AccessTools.FieldRefAccess<AIPilotCombatModes, float>("lastFiredTime");
        private static readonly AccessTools.FieldRef<AIPilotCombatModes, float> AimEffort =
            AccessTools.FieldRefAccess<AIPilotCombatModes, float>("aimEffort");
        private static readonly AccessTools.FieldRef<AIPilotCombatModes, WeaponInfo> CurrentWeaponInfo =
            AccessTools.FieldRefAccess<AIPilotCombatModes, WeaponInfo>("currentWeaponInfo");

        [HarmonyPrefix]
        private static bool Prefix(AIPilotCombatModes __instance, bool checkMode)
        {
            try
            {
                if (!Plugin.AiEmployment.Value) return true;

                // Mode maintenance is vanilla's. Only the trigger is ours.
                if (checkMode) return true;

                Pilot pilot = Traverse.Create(__instance).Field<Pilot>("pilot").Value;
                Aircraft aircraft = Traverse.Create(__instance).Field<Aircraft>("aircraft").Value;
                if (pilot == null || aircraft == null || pilot.playerControlled) return true;
                if (!aircraft.IsServer) return true;

                WeaponStation? station = aircraft.weaponManager != null
                    ? aircraft.weaponManager.currentWeaponStation
                    : null;
                if (station == null || station.WeaponInfo == null) return true;

                // Both pods share one weaponName, deliberately - see PluginInfo.
                // That is exactly what makes this one test cover all three
                // mounts without naming any of them.
                if (!PluginInfo.IsOurWeaponName(station.WeaponInfo.weaponName)) return true;

                return !AiEmployment.RunAttack(__instance, pilot, aircraft, station);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError(
                    $"[Tenpin] AI employment threw, falling back to the stock behaviour " +
                    $"for this pass: {ex}");
                return true;
            }
        }
    }
}
