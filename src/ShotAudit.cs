using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace RocketPod
{

    internal static class ShotAudit
    {
        private readonly struct Shot
        {
            internal readonly Vector3 PredictedImpact;
            internal readonly Vector3 AimPoint;
            internal readonly Vector3 TargetAtLaunch;
            internal readonly float Range;
            internal readonly float Elevation;
            internal readonly float PredictedTof;
            internal readonly float FiredAt;

            internal Shot(Vector3 predicted, Vector3 aim, Vector3 target,
                          float range, float elevation, float tof, float firedAt)
            {
                PredictedImpact = predicted;
                AimPoint = aim;
                TargetAtLaunch = target;
                Range = range;
                Elevation = elevation;
                PredictedTof = tof;
                FiredAt = firedAt;
            }
        }

        private static readonly Dictionary<int, Shot> _pending = new();
        private static int _reported;

        internal static void Expect(Vector3 predictedImpact, Vector3 aimPoint,
                                    Vector3 targetAtLaunch, float range,
                                    float elevation, float predictedTof)
        {
            if (!Plugin.AiShotAudit.Value) return;
            if (_reported >= Plugin.AiShotAuditCount.Value) return;

            _pending[Time.frameCount] = new Shot(
                predictedImpact, aimPoint, targetAtLaunch, range, elevation,
                predictedTof, Time.timeSinceLevelLoad);
        }

        private static bool TryClaim(out Shot shot)
        {

            for (int f = Time.frameCount; f >= Time.frameCount - 1; f--)
            {
                if (_pending.TryGetValue(f, out shot))
                {
                    _pending.Remove(f);
                    return true;
                }
            }

            shot = default;
            return false;
        }

        private static readonly Dictionary<Missile, Shot> _inFlight = new();

        internal static void Track(Missile missile)
        {
            if (missile == null) return;
            if (!TryClaim(out Shot shot)) return;
            _inFlight[missile] = shot;
        }

        internal static void Landed(Missile missile, Vector3 impact)
        {
            if (missile == null) return;
            if (!_inFlight.TryGetValue(missile, out Shot shot)) return;
            _inFlight.Remove(missile);

            if (_reported >= Plugin.AiShotAuditCount.Value) return;
            _reported++;

            Vector3 solverError = impact - shot.PredictedImpact;
            solverError.y = 0f;

            Vector3 aimError = shot.PredictedImpact - shot.AimPoint;
            aimError.y = 0f;

            Vector3 result = impact - shot.TargetAtLaunch;
            result.y = 0f;

            float tof = Time.timeSinceLevelLoad - shot.FiredAt;

            Vector3 downrange = shot.PredictedImpact - shot.TargetAtLaunch;
            downrange.y = 0f;
            if (downrange.sqrMagnitude < 1f) downrange = Vector3.forward;
            downrange.Normalize();
            Vector3 cross = Vector3.Cross(Vector3.up, downrange);

            Plugin.Log.LogInfo(
                $"[Tenpin] SHOT AUDIT {_reported}/{Plugin.AiShotAuditCount.Value}: " +
                $"range {shot.Range:0} m, arc {shot.Elevation:0.0} deg. " +
                $"SOLVER predicted-vs-actual {solverError.magnitude:0} m " +
                $"(down {Vector3.Dot(solverError, downrange):0}, cross {Vector3.Dot(solverError, cross):0}). " +
                $"AIM predicted-vs-aimpoint {aimError.magnitude:0} m. " +
                $"RESULT actual-vs-target {result.magnitude:0} m " +
                $"(down {Vector3.Dot(result, downrange):0}, cross {Vector3.Dot(result, cross):0}). " +
                $"Time of flight {tof:0.0} s against {shot.PredictedTof:0.0} s predicted.");
        }
    }

    [HarmonyPatch(typeof(Missile), "OnStartClient")]
    internal static class Missile_OnStartClient_AuditPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Missile __instance)
        {
            try
            {
                if (!Plugin.AiShotAudit.Value) return;
                if (__instance.definition == null ||
                    !PluginInfo.IsOurRound(__instance.definition.jsonKey)) return;

                ShotAudit.Track(__instance);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[Tenpin] Shot audit tracking failed: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(Missile), nameof(Missile.Detonate))]
    internal static class Missile_Detonate_AuditPatch
    {
        [HarmonyPrefix]
        private static void Prefix(Missile __instance)
        {
            try
            {
                if (!Plugin.AiShotAudit.Value) return;
                if (__instance.definition == null ||
                    !PluginInfo.IsOurRound(__instance.definition.jsonKey)) return;

                ShotAudit.Landed(__instance, __instance.GlobalPosition().AsVector3());
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[Tenpin] Shot audit landing failed: {ex}");
            }
        }
    }
}
