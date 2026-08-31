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
                        $"[Tenpin] Round RCS {have:0.###} -> {want:0.###}. The bundle authors this " +
                        "at zero, which makes the weapon perfectly uninterceptable and therefore " +
                        "untestable against defences. This is a test instrument, not a change to " +
                        "the survivability the spec settled: set RoundRCS to 0 to restore it. " +
                        "Logged once per session.");
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
                            "[Tenpin] Cannot reach Missile.motors[].IR_intensity by reflection, so " +
                            $"the round keeps its authored IR (motors={_fMotors != null}, " +
                            $"IR_intensity={_fIntensity != null}). The game moved or renamed one of " +
                            "them; check Missile in the 0.34 decompile. Logged once per session.");
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
                        $"[Tenpin] Round IR_intensity set to {want:0.###} on {motors.Length} " +
                        "motor stage(s). The source is added at ignition and Motor.Burnout never " +
                        "removes it, so the round is IR-visible for its whole flight, not just the " +
                        "1.8 s burn. Owner's decision 2026-08-09, reversing the spec's " +
                        "uninterceptable call. Set RoundIRSignature to false to restore it. " +
                        "Logged once per session.");
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
