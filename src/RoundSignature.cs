using System;
using HarmonyLib;
using UnityEngine;

namespace RocketPod
{

    [HarmonyPatch(typeof(Missile), "OnStartClient")]
    internal static class Missile_OnStartClient_SignaturePatch
    {
        private static bool _logged;

        [HarmonyPostfix]
        private static void Postfix(Missile __instance)
        {
            try
            {
                if (!Plugin.RoundRadarSignature.Value) return;

                if (__instance.definition == null ||
                    !PluginInfo.IsOurUnitName(__instance.definition.unitName)) return;

                float want = Mathf.Max(0f, Plugin.RoundRCS.Value);
                float have = __instance.RCS;
                if (Mathf.Approximately(have, want)) return;

                __instance.ModifyRCS(want - have);

                if (!_logged)
                {
                    _logged = true;

                    Plugin.Log.LogInfo(
                        $"[Tenpin] Round RCS {have:0.###} -> {want:0.###}. " +
                        "Set RoundRCS to 0 to restore it.");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[Tenpin] Round signature failed, round keeps its " +
                                    $"authored RCS: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(Missile), "OnStartClient")]
    internal static class Missile_OnStartClient_IRSignaturePatch
    {
        private static readonly System.Reflection.FieldInfo? _fMotors =
            AccessTools.Field(typeof(Missile), "motors");

        private static readonly System.Reflection.FieldInfo? _fIntensity =
            _fMotors?.FieldType.GetElementType() is Type motorType
                ? AccessTools.Field(motorType, "IR_intensity")
                : null;

        private static bool _logged;
        private static bool _loggedBroken;

        [HarmonyPostfix]
        private static void Postfix(Missile __instance)
        {
            try
            {
                if (!Plugin.RoundIRSignature.Value) return;

                if (__instance.definition == null ||
                    !PluginInfo.IsOurUnitName(__instance.definition.unitName)) return;

                if (_fMotors == null || _fIntensity == null)
                {
                    if (!_loggedBroken)
                    {
                        _loggedBroken = true;

                        Plugin.Log.LogWarning(
                            "[Tenpin] Cannot reach Missile.motors[].IR_intensity " +
                            $"(motors={_fMotors != null}, field={_fIntensity != null}).");
                    }
                    return;
                }

                if (!(_fMotors.GetValue(__instance) is Array motors) || motors.Length == 0) return;

                float want = Mathf.Max(0f, Plugin.RoundIRIntensity.Value);

                for (int i = 0; i < motors.Length; i++)
                {
                    object? motor = motors.GetValue(i);
                    if (motor == null) continue;
                    _fIntensity.SetValue(motor, want);
                }

                if (!_logged)
                {
                    _logged = true;

                    Plugin.Log.LogInfo(
                        $"[Tenpin] Round IR_intensity {want:0.###} on {motors.Length} " +
                        "stage(s). RoundIRSignature false restores it.");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[Tenpin] Round IR signature failed, round keeps its " +
                                    $"authored IR: {ex}");
            }
        }
    }
}
