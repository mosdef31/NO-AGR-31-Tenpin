using System;
using System.Reflection;
using HarmonyLib;
using RocketPod.Ballistics;
using UnityEngine;
using Shared.Ballistics;

namespace RocketPod
{

    internal static class SeekerFields
    {
        internal static readonly FieldInfo? Missile =
            AccessTools.Field(typeof(MissileSeeker), "missile");
        internal static readonly FieldInfo? KnownPos =
            AccessTools.Field(typeof(InertialSeekerShell), "knownPos");
        internal static readonly FieldInfo? AimPos =
            AccessTools.Field(typeof(InertialSeekerShell), "aimPos");
        internal static readonly FieldInfo? CEP =
            AccessTools.Field(typeof(InertialSeekerShell), "CEP");
        internal static readonly FieldInfo? Error =
            AccessTools.Field(typeof(InertialSeekerShell), "error");

        internal static readonly FieldInfo? TargetUnit =
            AccessTools.Field(typeof(MissileSeeker), "targetUnit");

        internal static bool Ok =>
            Missile != null && KnownPos != null && AimPos != null &&
            CEP != null && Error != null;

        internal static string Report =>
            $"missile={Missile != null} knownPos={KnownPos != null} " +
            $"aimPos={AimPos != null} CEP={CEP != null} error={Error != null}";
    }

    internal static class AimpointChannel
    {
        private static bool _loggedApplied;
        private static bool _loggedNoSolver;
        private static bool _loggedTerrain;

        private static bool _loggedBudget;

        private const float ReferenceFallbackSpeed = LowSpeedLaunch.ReferenceSpeed;

        private static bool IsAiShot(Missile missile)
        {
            if (missile == null || missile.owner == null) return false;

            if (missile.owner is Aircraft aircraft && aircraft.pilots != null)
            {
                foreach (Pilot pilot in aircraft.pilots)
                {
                    if (pilot != null && pilot.playerControlled) return false;
                }
            }

            return true;
        }

        private static bool _loggedLead;
        private static bool _loggedLeadOff;

        private static GlobalPosition LeadLockedTarget(InertialSeekerShell seeker, Missile missile,
                                                       GlobalPosition target,
                                                       TrajectorySolver.Result ballistic)
        {
            if (!Plugin.LeadLockedAimpoint.Value)
            {
                if (!_loggedLeadOff)
                {
                    _loggedLeadOff = true;

                    Plugin.Log.LogInfo(
                        "[Tenpin] LeadLockedAimpoint is off, so a locked round aims where " +
                        "the target was.");
                }
                return target;
            }

            if (!ballistic.Hit || ballistic.TimeOfFlight <= 0f) return target;

            if (SeekerFields.TargetUnit == null) return target;
            if (SeekerFields.TargetUnit.GetValue(seeker) is not Unit unit) return target;
            if (!TargetLead.IsMoving(unit)) return target;

            Vector3 led = TargetLead.PredictPosition(unit, ballistic.TimeOfFlight, out bool routed);

            led.y = target.y;

            var leadPoint = new GlobalPosition(led);

            if (!_loggedLead)
            {
                _loggedLead = true;
                float moved = ((Vector3)(leadPoint - target)).magnitude;

                Plugin.Log.LogInfo(
                    $"[Tenpin] Locked aimpoint led {moved:0} m over " +
                    $"{ballistic.TimeOfFlight:0.0} s, target '{unit.unitName}'.");
            }

            return leadPoint;
        }

        private static GlobalPosition ClampToBudget(Missile missile, GlobalPosition target,
                                                    TrajectorySolver.Result ballistic)
        {
            if (!Plugin.GuidanceBudget.Value) return target;

            float mrad = IsAiShot(missile)
                ? Plugin.AiGuidanceBudgetMilliradians.Value
                : Plugin.GuidanceBudgetMilliradians.Value;

            if (mrad <= 0f) return target;

            if (!ballistic.Hit) return target;

            Vector3 from = ballistic.ImpactPoint;
            Vector3 to = target.AsVector3();

            var offset = new Vector3(to.x - from.x, 0f, to.z - from.z);
            float slant = (to - missile.GlobalPosition().AsVector3()).magnitude;

            float launchSpeed = missile.rb != null ? missile.rb.velocity.magnitude : ReferenceFallbackSpeed;
            float relief = LowSpeedLaunch.BudgetRelief(launchSpeed);
            float budget = slant * (mrad / 1000f) * relief;

            if (offset.magnitude <= budget) return target;

            Vector3 aimed = from + Vector3.ClampMagnitude(offset, budget);
            aimed.y = to.y;

            if (!_loggedBudget)
            {
                _loggedBudget = true;

                Plugin.Log.LogInfo(
                    $"[Tenpin] Guidance budget active at {mrad:0.##} mrad, allowing " +
                    $"{budget:0} m of correction.");
                Plugin.Log.LogInfo(
                    $"[Tenpin] First round missed by {offset.magnitude:0} m at {slant:0} m, " +
                    $"landing {offset.magnitude - budget:0} m short.");
                Plugin.Log.LogInfo(
                    $"[Tenpin] Launch speed {launchSpeed:0} m/s gave {relief:0.##}x " +
                    "low-speed relief.");
            }

            return new GlobalPosition(aimed);
        }

        private static TrajectorySolver.Result Predict(TrajectorySolver.RoundSpec spec,
                                                       Missile missile, Vector3 launchPos)
        {
            float stepScale = Mathf.Max(1f, Plugin.AimpointStepScale.Value);

            return TerrainImpact.Solve(spec, launchPos, missile.rb.velocity, stepScale,
                                       TrySampleGroundHeight, Plugin.SampleTerrainHeight.Value);
        }

        private static void StampFlightTime(Missile missile, Vector3 launchPos, GlobalPosition target)
        {
            if (!Plugin.CorrectSeekerFlightTime.Value) return;

            TrajectorySolver.RoundSpec? spec = RoundSpecFactory.FromMissile(missile, Plugin.Log);
            if (spec == null) return;

            TrajectorySolver.Result r = TrajectorySolver.Integrate(
                spec, launchPos, missile.rb.velocity,
                groundY: target.y,
                wind: default,
                stepScale: Mathf.Max(1f, Plugin.AimpointStepScale.Value));

            if (r.Hit) Stamp(missile, r.TimeOfFlight);
        }

        private static void Stamp(Missile missile, float tof)
        {
            if (!Plugin.CorrectSeekerFlightTime.Value) return;
            if (tof <= 0f) return;

            RoundFlightTime state = missile.gameObject.GetComponent<RoundFlightTime>()
                                    ?? missile.gameObject.AddComponent<RoundFlightTime>();
            state.PredictedToF = tof;
            state.LaunchTime = Time.timeSinceLevelLoad;
        }

        internal static bool TrySampleGroundHeight(Vector3 globalPoint, out float height)
        {
            height = 0f;
            try
            {
                var global = new GlobalPosition(globalPoint.x, globalPoint.y, globalPoint.z);
                Vector3 local = global.ToLocalPosition();

                Vector3 from = local + Vector3.up * 6000f;
                Vector3 to = local - Vector3.up * 6000f;

                int mask = (int)PhysicsLayers.StaticsMask | (int)PhysicsLayers.ShipsMask;
                if (Physics.Linecast(from, to, out RaycastHit hit, mask))
                {
                    height = hit.point.GlobalY();
                    return true;
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[Tenpin] Ground sample threw: {ex.Message}");
            }

            return false;
        }

        private static float SampleGroundHeight(Vector3 globalPoint)
        {
            try
            {

                var global = new GlobalPosition(globalPoint.x, globalPoint.y, globalPoint.z);
                Vector3 local = global.ToLocalPosition();

                Vector3 from = local + Vector3.up * 6000f;
                Vector3 to = local - Vector3.up * 6000f;

                int mask = (int)PhysicsLayers.StaticsMask | (int)PhysicsLayers.ShipsMask;
                if (Physics.Linecast(from, to, out RaycastHit hit, mask))
                {
                    return hit.point.GlobalY();
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[Tenpin] Ground sample failed, falling back to sea " +
                                      $"level: {ex.Message}");
            }
            return 0f;
        }

        internal static float Apply(InertialSeekerShell seeker, Missile missile)
        {
            if (SeekerFields.KnownPos!.GetValue(seeker) is not GlobalPosition knownPos) return -1f;
            if (SeekerFields.AimPos!.GetValue(seeker) is not GlobalPosition aimPos) return -1f;

            Vector3 launchPos = missile.GlobalPosition().AsVector3();

            if (knownPos != aimPos)
            {

                TrajectorySolver.RoundSpec? lockSpec = RoundSpecFactory.FromMissile(missile, Plugin.Log);
                TrajectorySolver.Result lockBallistic = lockSpec != null
                    ? Predict(lockSpec, missile, launchPos)
                    : default;

                GlobalPosition led = LeadLockedTarget(seeker, missile, knownPos, lockBallistic);
                GlobalPosition aimed = lockSpec != null
                    ? ClampToBudget(missile, led, lockBallistic)
                    : led;

                if (aimed != knownPos) SeekerFields.KnownPos.SetValue(seeker, aimed);
                StampFlightTime(missile, launchPos, aimed);
                return ((Vector3)(aimed - missile.GlobalPosition())).magnitude;
            }

            if (!Plugin.PoweredAimpoint.Value)
            {
                StampFlightTime(missile, launchPos, knownPos);
                return ((Vector3)(knownPos - missile.GlobalPosition())).magnitude;
            }

            TrajectorySolver.RoundSpec? spec = RoundSpecFactory.FromMissile(missile, Plugin.Log);
            if (spec == null)
            {
                if (!_loggedNoSolver)
                {
                    _loggedNoSolver = true;

                    Plugin.Log.LogWarning(
                        "[Tenpin] Could not read the round's flight model, so rounds keep " +
                        "the stock aimpoint.");
                }
                return ((Vector3)(knownPos - missile.GlobalPosition())).magnitude;
            }

            float stepScale = Mathf.Max(1f, Plugin.AimpointStepScale.Value);

            TrajectorySolver.Result result = TrajectorySolver.Integrate(
                spec,
                launchPos,
                missile.rb.velocity,
                groundY: 0f,
                wind: default,
                stepScale: stepScale);

            if (result.Hit && Plugin.SampleTerrainHeight.Value)
            {
                TrajectorySolver.Result marched = TerrainImpact.Solve(
                    spec, launchPos, missile.rb.velocity, stepScale, TrySampleGroundHeight, Plugin.SampleTerrainHeight.Value);

                if (marched.Hit)
                {
                    if (!_loggedTerrain)
                    {
                        _loggedTerrain = true;
                        Vector3 a = result.ImpactPoint;
                        Vector3 b = marched.ImpactPoint;
                        float shift = new Vector2(b.x - a.x, b.z - a.z).magnitude;

                        Plugin.Log.LogInfo(
                            $"[Tenpin] Terrain-marched aimpoint: ground at {b.y:0} m ASL " +
                            $"moves the prediction {shift:0} m.");
                    }

                    result = marched;
                }
            }

            if (!result.Hit)
            {

                return ((Vector3)(knownPos - missile.GlobalPosition())).magnitude;
            }

            var predicted = new GlobalPosition(result.ImpactPoint);
            SeekerFields.KnownPos.SetValue(seeker, predicted);
            SeekerFields.AimPos.SetValue(seeker, predicted);

            Stamp(missile, result.TimeOfFlight);

            float slantRange = ((Vector3)(predicted - missile.GlobalPosition())).magnitude;

            if (!_loggedApplied)
            {
                _loggedApplied = true;
                float stockRange = ((Vector3)(knownPos - missile.GlobalPosition())).magnitude;
                Plugin.Log.LogInfo(
                    $"[Tenpin] Powered aimpoint active: stock was {stockRange:0} m out, " +
                    $"powered is {slantRange:0} m.");
                Plugin.Log.LogInfo(
                    $"[Tenpin] Time of flight {result.TimeOfFlight:0.0} s, {result.Steps} " +
                    $"steps at stepScale {Plugin.AimpointStepScale.Value:0.#}.");
            }

            return slantRange;
        }
    }

    [HarmonyPatch(typeof(InertialSeekerShell), nameof(InertialSeekerShell.Initialize))]
    internal static class InertialSeekerShell_Initialize_Patch
    {
        private static bool _loggedBroken;

        [HarmonyPostfix]
        private static void Postfix(InertialSeekerShell __instance)
        {
            try
            {
                if (!SeekerFields.Ok)
                {
                    if (!_loggedBroken)
                    {
                        _loggedBroken = true;

                        Plugin.Log.LogWarning(
                            "[Tenpin] InertialSeekerShell fields not found, so rounds fly " +
                            "on stock behaviour.");
                        Plugin.Log.LogWarning($"[Tenpin] {SeekerFields.Report}");
                    }
                    return;
                }

                if (SeekerFields.Missile!.GetValue(__instance) is not Missile missile) return;

                if (missile.definition == null ||
                    !PluginInfo.IsOurUnitName(missile.definition.unitName)) return;

                float slantRange = AimpointChannel.Apply(__instance, missile);
                AngularDispersion.Apply(__instance, slantRange);
            }
            catch (Exception ex)
            {

                Plugin.Log.LogError($"[Tenpin] Seeker aimpoint/dispersion failed, round keeps " +
                                    $"stock behaviour: {ex}");
            }
        }
    }
}
