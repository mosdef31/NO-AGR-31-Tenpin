using System;
using HarmonyLib;
using UnityEngine;

namespace RocketPod
{

    [HarmonyPatch]
    internal static class Autopilot_AutoAim_BankPatch
    {
        private static bool _logged;

        [HarmonyPatch(typeof(Autopilot), nameof(Autopilot.AutoAim),
            new[] { typeof(GlobalPosition), typeof(bool), typeof(bool), typeof(bool),
                    typeof(float), typeof(float), typeof(bool), typeof(float), typeof(Vector3) })]
        [HarmonyPrefix]
        private static void Prefix(Autopilot __instance, ref float bankAllowed)
        {
            try
            {
                if (!Plugin.AiEmployment.Value) return;
                if (bankAllowed <= Plugin.AiMaxBankDegrees.Value) return;

                Aircraft aircraft = Traverse.Create(__instance).Field<Aircraft>("aircraft").Value;
                if (aircraft == null || aircraft.weaponManager == null) return;

                if (aircraft.pilots != null)
                {
                    foreach (Pilot pilot in aircraft.pilots)
                    {
                        if (pilot != null && pilot.playerControlled) return;
                    }
                }

                WeaponStation station = aircraft.weaponManager.currentWeaponStation;
                if (station == null || station.WeaponInfo == null) return;
                if (station.WeaponInfo.weaponName != PluginInfo.WeaponInfoName) return;
                if (station.Ammo <= 0) return;

                bankAllowed = Plugin.AiMaxBankDegrees.Value;

                if (!_logged)
                {
                    _logged = true;
                    Plugin.Log.LogInfo(
                        $"[Tenpin] Bank limited to {Plugin.AiMaxBankDegrees.Value:0} deg while an AI " +
                        "has the pod selected. The stock combat state allows 180, which is right for " +
                        "a weapon that steers itself and wrong for one that leaves along the nose - " +
                        "a rolling aircraft cannot fire an unguided salvo at anything. Logged once.");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[Tenpin] Bank limit failed, the autopilot keeps its own: {ex}");
            }
        }
    }
}
