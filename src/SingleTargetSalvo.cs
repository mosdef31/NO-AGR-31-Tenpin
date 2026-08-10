using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace RocketPod
{

    [HarmonyPatch(typeof(WeaponManager), nameof(WeaponManager.Fire))]
    internal static class WeaponManager_Fire_SingleTargetPatch
    {
        private static readonly FieldInfo? _fAircraft =
            AccessTools.Field(typeof(WeaponManager), "aircraft");
        private static readonly FieldInfo? _fTargetList =
            AccessTools.Field(typeof(WeaponManager), "targetList");

        private static bool _loggedBroken;
        private static bool _loggedRedirect;

        [HarmonyPrefix]
        private static bool Prefix(WeaponManager __instance)
        {
            try
            {
                if (!Plugin.SingleTargetSalvo.Value) return true;

                if (_fAircraft == null || _fTargetList == null)
                {
                    if (!_loggedBroken)
                    {
                        _loggedBroken = true;
                        Plugin.Log.LogWarning(
                            "[Tenpin] Could not find WeaponManager.aircraft/targetList by " +
                            "reflection, so the single-target salvo is off. With more than one " +
                            "unit locked the pod will fire ONE rocket per target instead of its " +
                            $"load at one point. Re-check the decompile: aircraft={_fAircraft != null} " +
                            $"targetList={_fTargetList != null}.");
                    }
                    return true;
                }

                WeaponStation station = __instance.currentWeaponStation;
                if (station == null) return true;

                if (station.WeaponInfo == null ||
                    station.WeaponInfo.weaponName != PluginInfo.WeaponInfoName) return true;

                if (_fTargetList.GetValue(__instance) is not List<Unit> targets) return true;
                if (targets.Count <= 1) return true;

                if (_fAircraft.GetValue(__instance) is not Aircraft aircraft) return true;

                if (station.SafetyIsOn(aircraft) || aircraft.weaponStations.Count == 0) return true;
                if (aircraft.remoteSim || !station.Ready() || station.SalvoInProgress) return true;

                Unit? target = null;
                for (int i = 0; i < targets.Count; i++)
                {
                    Unit u = targets[i];
                    if (u != null && !u.disabled) { target = u; break; }
                }
                if (target == null) return true;

                station.LaunchMount(
                    aircraft, target,
                    aircraft.GlobalPosition() + aircraft.transform.forward * 50000f);

                if (!_loggedRedirect)
                {
                    _loggedRedirect = true;
                    Plugin.Log.LogInfo(
                        $"[Tenpin] {targets.Count} targets locked. Firing the whole pod at the " +
                        "first one instead of the game's one-rocket-per-target salvo, which for a " +
                        "saturation weapon sends a single guided round to each unit. Extra locks " +
                        "are a target list to cycle, not a fire order. Logged once per session.");
                }
                return false;
            }
            catch (Exception ex)
            {

                Plugin.Log.LogError($"[Tenpin] Single-target salvo failed, falling back to the " +
                                    $"game's firing behaviour: {ex}");
                return true;
            }
        }
    }
}
