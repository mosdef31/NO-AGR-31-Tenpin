using System;
using HarmonyLib;
using UnityEngine;

namespace RocketPod
{

    internal sealed class RoundFlightTime : MonoBehaviour
    {
        internal float PredictedToF;
        internal float LaunchTime;

        internal float Remaining =>
            Mathf.Max(0.25f, PredictedToF - (Time.timeSinceLevelLoad - LaunchTime));
    }

    [HarmonyPatch(typeof(Kinematics), nameof(Kinematics.GetBallisticAimPoint))]
    internal static class Kinematics_GetBallisticAimPoint_FlightTimePatch
    {
        private static bool _logged;

        [HarmonyPrefix]
        private static void Prefix(Missile missile, ref float timeToTarget)
        {
            try
            {
                if (!Plugin.CorrectSeekerFlightTime.Value) return;
                if (missile == null) return;

                RoundFlightTime? state = missile.GetComponent<RoundFlightTime>();
                if (state == null) return;

                float corrected = state.Remaining;

                if (!_logged)
                {
                    _logged = true;
                    Plugin.Log.LogInfo(
                        $"[Tenpin] Seeker flight-time correction active. First round: the seeker " +
                        $"estimated {timeToTarget:0.0} s from horizontal closure, the solver " +
                        $"predicted {corrected:0.0} s. The seeker's own estimate collapses in a " +
                        "dive and aims the round high by half g t squared, which is what made " +
                        "close shots pitch up and fly away. Logged once per session.");
                }

                timeToTarget = corrected;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[Tenpin] Flight-time correction failed, round keeps the " +
                                    $"stock estimate: {ex}");
            }
        }
    }
}
