using System;
using HarmonyLib;
using UnityEngine;
using Shared.Ballistics;

namespace RocketPod
{

    internal sealed class RoundFlightTime : MonoBehaviour
    {
        internal float PredictedToF;
        internal float LaunchTime;

        internal float Remaining =>
            Mathf.Max(0f, PredictedToF - (Time.timeSinceLevelLoad - LaunchTime));
    }

    [HarmonyPatch(typeof(Kinematics), nameof(Kinematics.GetBallisticAimPoint))]
    internal static class Kinematics_GetBallisticAimPoint_FlightTimePatch
    {
        private static bool _logged;
        private static bool _terminalLogged;

        private const float TerminalCutoffSeconds = 0.15f;

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

                timeToTarget = Mathf.Max(0f, corrected);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[Tenpin] Flight-time correction failed, round keeps the " +
                                    $"stock estimate: {ex}");
            }
        }

        [HarmonyPostfix]
        private static void Postfix(Missile missile, ref GlobalPosition __result)
        {
            try
            {
                if (!Plugin.CorrectSeekerFlightTime.Value) return;
                if (missile == null || missile.rb == null) return;

                RoundFlightTime? state = missile.GetComponent<RoundFlightTime>();
                if (state == null) return;
                if (state.Remaining > TerminalCutoffSeconds) return;

                Vector3 v = missile.rb.velocity;
                if (v.sqrMagnitude < 1f) return;

                if (!_terminalLogged)
                {
                    _terminalLogged = true;
                    Plugin.Log.LogInfo(
                        "[Tenpin] A round ran its flight time out and stopped steering. Past " +
                        "that point the aimpoint is pinned along its own velocity, so it flies " +
                        "on ballistically instead of turning back toward a target it has " +
                        "already passed. Logged once per session.");
                }

                __result = missile.GlobalPosition() + v;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[Tenpin] Terminal steering cutoff failed, round keeps " +
                                    $"the stock aimpoint: {ex}");
            }
        }
    }
}
