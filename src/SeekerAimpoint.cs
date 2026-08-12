using System;
using System.Reflection;
using HarmonyLib;
using RocketPod.Ballistics;
using UnityEngine;

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

        private static GlobalPosition ClampToBudget(Missile missile, Vector3 launchPos,
                                                    GlobalPosition target)
        {
            if (!Plugin.GuidanceBudget.Value) return target;

            float mrad = Plugin.GuidanceBudgetMilliradians.Value;
            if (mrad <= 0f) return target;

            TrajectorySolver.RoundSpec? spec = RoundSpecFactory.FromMissile(missile, Plugin.Log);
            if (spec == null) return target;

            TrajectorySolver.Result ballistic = Predict(spec, missile, launchPos);
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
                    $"[Tenpin] Guidance budget active at {mrad:0.##} mrad. First round: the " +
                    $"ballistic path missed the locked target by {offset.magnitude:0} m at " +
                    $"{slant:0} m slant, and the round is allowed {budget:0} m of correction, so " +
                    $"it will land about {offset.magnitude - budget:0} m short of the target. Aim " +
                    $"better and this goes to zero. Launch speed {launchSpeed:0} m/s gave a " +
                    $"{relief:0.##}x low-speed relief on the budget (1x at or above " +
                    $"{LowSpeedLaunch.ReferenceSpeed:0} m/s, so a jet sees none of it). " +
                    "Logged once per session.");
            }

            return new GlobalPosition(aimed);
        }

        private static TrajectorySolver.Result Predict(TrajectorySolver.RoundSpec spec,
                                                       Missile missile, Vector3 launchPos)
        {
            float stepScale = Mathf.Max(1f, Plugin.AimpointStepScale.Value);

            return TerrainImpact.Solve(spec, launchPos, missile.rb.velocity, stepScale,
                                       TrySampleGroundHeight);
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

        internal static float SampleGroundHeightPublic(Vector3 globalPoint) =>
            SampleGroundHeight(globalPoint);

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
                GlobalPosition aimed = ClampToBudget(missile, launchPos, knownPos);
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
                        "[Tenpin] Could not read the round's flight model, so the powered aimpoint " +
                        "is off and rounds keep the stock free-fall aimpoint - which for a weapon " +
                        "with a motor lands far short of where it actually flies. Check the " +
                        "reflection warnings from RoundSpecFactory above this line.");
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
                    spec, launchPos, missile.rb.velocity, stepScale, TrySampleGroundHeight);

                if (marched.Hit)
                {
                    if (!_loggedTerrain)
                    {
                        _loggedTerrain = true;
                        Vector3 a = result.ImpactPoint;
                        Vector3 b = marched.ImpactPoint;
                        float shift = new Vector2(b.x - a.x, b.z - a.z).magnitude;
                        Plugin.Log.LogInfo(
                            $"[Tenpin] Terrain-marched aimpoint: the ground where the round comes " +
                            $"down is {b.y:0} m above sea level, which moves the prediction " +
                            $"{shift:0} m. Without this the pattern lands long of high ground by " +
                            "roughly elevation over tan(descent angle). Logged once per session.");
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
                    $"[Tenpin] Powered aimpoint active. First round: stock free-fall aimpoint was " +
                    $"{stockRange:0} m out, powered prediction is {slantRange:0} m " +
                    $"(time of flight {result.TimeOfFlight:0.0} s, {result.Steps} steps at " +
                    $"stepScale {Plugin.AimpointStepScale.Value:0.#}). Logged once per session.");
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
                            "[Tenpin] Could not resolve InertialSeekerShell's private fields by " +
                            "reflection, so both the powered aimpoint and angular dispersion are " +
                            $"OFF and rounds fly on stock behaviour. Re-check the decompile: " +
                            $"{SeekerFields.Report}.");
                    }
                    return;
                }

                if (SeekerFields.Missile!.GetValue(__instance) is not Missile missile) return;

                if (missile.definition == null ||
                    missile.definition.unitName != PluginInfo.MissileUnitName) return;

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
