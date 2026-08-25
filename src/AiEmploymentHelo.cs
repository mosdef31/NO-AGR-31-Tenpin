using System;
using HarmonyLib;
using RocketPod.Ballistics;
using UnityEngine;

namespace RocketPod
{

    [HarmonyPatch(typeof(AIHeloCombatState), "UseMissiles")]
    internal static class AIHeloCombatState_UseMissiles_TenpinPatch
    {
        private static float _nextSolve;
        private static float _elevation;
        private static bool _hasSolution;
        private static bool _lofted;
        private static bool _logged;

        private static readonly System.Reflection.MethodInfo? SetModeMethod =
            AccessTools.Method(typeof(AIHeloCombatState), "SetCombatMode");
        private static readonly Type? ModeType =
            AccessTools.Inner(typeof(AIHeloCombatState), "CombatMode");

        private const int FlyingToTargetMode = 0;

        private const int BreakOffMode = 2;

        private static void SetHeloMode(AIHeloCombatState state, int mode)
        {
            if (SetModeMethod == null || ModeType == null) return;
            SetModeMethod.Invoke(state, new[] { Enum.ToObject(ModeType, mode) });
        }
        private static float _timeOfFlight;

        private enum Phase { Recover, RunIn, Pitch, Fire, Break }

        private static Phase _phase = Phase.Recover;
        private static float _phaseSince;
        private static int _roundsThisAttack;

        private static void SetPhase(Phase phase)
        {
            if (_phase == phase) return;
            _phase = phase;
            _phaseSince = Time.timeSinceLevelLoad;
        }

        private static float PhaseAge => Time.timeSinceLevelLoad - _phaseSince;

        private static float Alignment(Aircraft aircraft, Vector3 heading) =>
            Vector3.Angle(
                new Vector3(aircraft.transform.forward.x, 0f, aircraft.transform.forward.z),
                new Vector3(heading.x, 0f, heading.z));

        private static float SideSpeed(Aircraft aircraft)
        {
            if (aircraft.rb == null) return 0f;
            return Mathf.Abs(Vector3.Dot(aircraft.rb.velocity, aircraft.transform.right));
        }

        private static string PhaseName(float range) => _phase switch
        {
            Phase.Recover => "AGR-31 steadying up",
            Phase.RunIn   => $"AGR-31 run-in {range / 1000f:0.0} km",
            Phase.Pitch   => "AGR-31 pop-up",
            Phase.Fire    => "AGR-31 firing salvo",
            _             => "AGR-31 breaking off",
        };
        private static float _trim;
        private static float _commanded;
        private static bool _commandStarted;

        [HarmonyPrefix]
        private static bool Prefix(AIHeloCombatState __instance)
        {
            try
            {
                if (!Plugin.AiEmployment.Value) return true;

                var t = Traverse.Create(__instance);

                Aircraft aircraft = t.Field<Aircraft>("aircraft").Value;
                Pilot pilot = t.Field<Pilot>("pilot").Value;
                if (aircraft == null || pilot == null || pilot.playerControlled) return true;
                if (!aircraft.IsServer) return true;

                WeaponManager manager = aircraft.weaponManager;
                if (manager == null) return true;

                WeaponStation station = manager.currentWeaponStation;
                if (station == null || station.WeaponInfo == null) return true;
                if (station.WeaponInfo.weaponName != PluginInfo.WeaponInfoName) return true;
                if (station.Ammo <= 0) return true;

                Unit target = t.Field<Unit>("currentTarget").Value;
                if (target == null || target.disabled) return true;

                FactionHQ hq = aircraft.NetworkHQ;
                if (hq == null || !hq.TryGetKnownPosition(target, out GlobalPosition known)) return true;

                TrajectorySolver.RoundSpec? spec = AiEmployment.Spec();
                if (spec == null) return true;

                Vector3 here = aircraft.transform.GlobalPosition().AsVector3();

                Vector3 targetPos = AiEmployment.PredictPosition(target, _timeOfFlight, out _);

                Vector3 flat = targetPos - here;
                flat.y = 0f;
                float range = flat.magnitude;
                if (range < 1f) return true;

                Vector3 velocity = aircraft.rb.velocity;

                if (Time.timeSinceLevelLoad > _nextSolve)
                {
                    _nextSolve = Time.timeSinceLevelLoad + Plugin.AiSolveInterval.Value;

                    float? low = TrajectorySolver.SolveLaunchElevation(
                        spec, here, aircraft.transform.forward, Mathf.Max(velocity.magnitude, 1f),
                        range, groundY: targetPos.y, loft: false);

                    float? arc = low ?? TrajectorySolver.SolveLaunchElevation(
                        spec, here, aircraft.transform.forward, Mathf.Max(velocity.magnitude, 1f),
                        range, groundY: targetPos.y, loft: true);

                    _lofted = !low.HasValue && arc.HasValue;
                    _hasSolution = arc.HasValue && arc.Value <= Plugin.AiHeloMaxLoftDegrees.Value;
                    _elevation = arc ?? 0f;

                    if (arc.HasValue && !_hasSolution)
                    {
                        AiEmployment.Report(
                            $"helo: the only arc to {range:0} m is {arc.Value:0.0} deg, past the " +
                            $"{Plugin.AiHeloMaxLoftDegrees.Value:0} deg a helicopter can hold");
                    }
                }

                if (!_hasSolution)
                {
                    AiEmployment.Report(
                        $"helo: no arc reaches {range:0} m (speed {velocity.magnitude:0} m/s) - closing");

                    SetHeloMode(__instance, FlyingToTargetMode);
                    return false;
                }

                Vector3 heading = flat.normalized;

                if (!_commandStarted)
                {
                    _commandStarted = true;
                    _commanded = 0f;
                }

                float wantElevation;

                switch (_phase)
                {
                    case Phase.Recover:

                        wantElevation = 0f;

                        if (Alignment(aircraft, heading) < Plugin.AiHeloRunInAlignDegrees.Value &&
                            SideSpeed(aircraft) < Plugin.AiHeloMaxSideSpeed.Value)
                        {
                            SetPhase(Phase.RunIn);
                        }
                        else if (PhaseAge > Plugin.AiHeloRecoverTimeout.Value)
                        {

                            AiEmployment.Report(
                                $"helo: cannot settle the airframe (bearing " +
                                $"{Alignment(aircraft, heading):0} deg, side speed " +
                                $"{SideSpeed(aircraft):0} m/s) - resetting the attack");
                            SetPhase(Phase.Break);
                        }
                        break;

                    case Phase.RunIn:

                        wantElevation = 0f;

                        if (Alignment(aircraft, heading) > Plugin.AiHeloRunInAlignDegrees.Value * 2f ||
                            SideSpeed(aircraft) > Plugin.AiHeloMaxSideSpeed.Value * 2f)
                        {
                            SetPhase(Phase.Recover);
                            break;
                        }

                        bool inWindow = range <= Plugin.AiHeloPopUpRange.Value;

                        if (inWindow && PhaseAge > Plugin.AiHeloRunInSeconds.Value)
                        {
                            SetPhase(Phase.Pitch);
                            AiEmployment.Report(
                                $"helo: run-in complete at {range:0} m, pitching up to " +
                                $"{_elevation:0.0} deg");
                        }
                        break;

                    case Phase.Pitch:
                        wantElevation = _elevation + _trim;

                        if (Alignment(aircraft, heading) > Plugin.AiHeloAbortAlignDegrees.Value)
                        {
                            AiEmployment.Report(
                                $"helo: bearing ran out to {Alignment(aircraft, heading):0} deg " +
                                "during the flare - recovering");
                            SetPhase(Phase.Recover);
                            break;
                        }

                        if (Mathf.Abs(_commanded - wantElevation) < 1.5f)
                        {
                            SetPhase(Phase.Fire);
                        }
                        else if (PhaseAge > Plugin.AiHeloPitchTimeout.Value)
                        {
                            AiEmployment.Report(
                                $"helo: could not reach {wantElevation:0.0} deg in " +
                                $"{Plugin.AiHeloPitchTimeout.Value:0} s - breaking off");
                            SetPhase(Phase.Break);
                        }
                        break;

                    case Phase.Fire:
                        wantElevation = _elevation + _trim;

                        if (_roundsThisAttack >= Plugin.AiHeloSalvo.Value ||
                            PhaseAge > Plugin.AiHeloFireSeconds.Value ||
                            station.Ammo <= 0)
                        {
                            SetPhase(Phase.Break);
                        }
                        break;

                    default:
                        wantElevation = -Plugin.AiHeloBreakDegrees.Value;

                        if (PhaseAge < 0.2f)
                        {
                            t.Field<float>("breakOffTimer").Value = Plugin.AiHeloBreakSeconds.Value;
                            SetHeloMode(__instance, BreakOffMode);
                        }

                        if (PhaseAge > Plugin.AiHeloBreakSeconds.Value)
                        {
                            _roundsThisAttack = 0;
                            _trim = 0f;
                            SetPhase(Phase.Recover);
                        }
                        break;
                }

                _commanded = Mathf.MoveTowards(
                    _commanded, wantElevation,
                    Plugin.AiHeloPitchRateDegrees.Value * Time.deltaTime);

                Vector3 axis = Vector3.Cross(Vector3.up, heading);
                Vector3 launchDir = Quaternion.AngleAxis(-_commanded, axis) * heading;

                GlobalPosition aim = aircraft.transform.GlobalPosition();
                aim += launchDir * Mathf.Max(range, Plugin.AiAimLeadMetres.Value);
                t.Field<GlobalPosition>("destination").Value = aim;
                t.Field<string>("stateDisplayName").Value = PhaseName(range);

                if (_phase != Phase.Fire) return false;

                TrajectorySolver.Result r = TerrainImpact.Solve(
                    spec, here, velocity, Plugin.AiSolverStepScale.Value,
                    AimpointChannel.TrySampleGroundHeight);

                if (!r.Hit) return false;

                _timeOfFlight = r.TimeOfFlight;

                Vector3 impactFlat = r.ImpactPoint - here;
                impactFlat.y = 0f;
                float shortBy = range - impactFlat.magnitude;

                _trim = Mathf.Clamp(
                    _trim + Plugin.AiTrimGain.Value *
                            Mathf.Atan2(shortBy, Mathf.Max(range, 1f)) * Mathf.Rad2Deg *
                            Time.deltaTime,
                    -Plugin.AiTrimLimitDegrees.Value, Plugin.AiTrimLimitDegrees.Value);

                Vector3 miss = r.ImpactPoint - targetPos;
                miss.y = 0f;

                float tolerance = range * (Plugin.AiGuidanceBudgetMilliradians.Value / 1000f)
                                  * Plugin.AiGateMargin.Value;

                if (AiEmployment.IsStationaryTarget(target))
                {
                    tolerance *= Plugin.AiStationaryTolerance.Value;
                }

                if (miss.magnitude > tolerance)
                {
                    AiEmployment.Report(
                        $"helo: in the pop-up but aim is off by {miss.magnitude:0} m at {range:0} m, " +
                        $"tolerance {tolerance:0} m (commanding {_commanded:0.0} deg)");
                    return false;
                }

                float lastFired = t.Field<float>("lastFiredTime").Value;
                if (Time.timeSinceLevelLoad - lastFired < Plugin.AiHeloShotInterval.Value) return false;

                manager.ClearTargetList();
                if (CombatAI.LookForMissileTargets(
                        aircraft, target, station, AiEmployment.TargetBuffer) > 0)
                {
                    ShotAudit.Expect(r.ImpactPoint, targetPos, targetPos, range,
                                     _commanded, r.TimeOfFlight);

                    pilot.Fire();
                    _roundsThisAttack++;
                    t.Field<float>("lastFiredTime").Value = Time.timeSinceLevelLoad;

                    if (!_logged)
                    {
                        _logged = true;
                        Plugin.Log.LogInfo(
                            $"[Tenpin] A HELICOPTER fired from a POP-UP: {range:0} m, nose up " +
                            $"{_commanded:0.0} deg, predicted miss {miss.magnitude:0} m. Run in level " +
                            "and lined up, flare, shoot, break - which is what real crews do with " +
                            "rockets, and it works here for the same reason: a large attitude a helo " +
                            "can actually fly beats a fine correction it cannot hold. Logged once.");
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError(
                    $"[Tenpin] Helicopter employment threw, falling back to the stock behaviour " +
                    $"for this pass: {ex}");
                return true;
            }
        }
    }
}
