using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace RocketPod
{

    [HarmonyPatch(typeof(WeaponStation), nameof(WeaponStation.LaunchMount))]
    internal static class WeaponStation_LaunchMount_CyclePatch
    {
        private static readonly FieldInfo? _fWeaponIndex =
            AccessTools.Field(typeof(WeaponStation), "weaponIndex");

        private static bool _loggedWrap;
        private static bool _loggedBroken;

        [HarmonyPrefix]
        private static void Prefix(WeaponStation __instance)
        {
            try
            {
                if (_fWeaponIndex == null)
                {
                    if (!_loggedBroken)
                    {
                        _loggedBroken = true;
                        Plugin.Log.LogWarning(
                            "[Tenpin] Could not find WeaponStation.weaponIndex by reflection, so " +
                            "multi-pod cycling is off. With more than one pod on a station only " +
                            "one rocket per pod will fire. Re-check the decompile.");
                    }
                    return;
                }

                if (__instance.WeaponInfo == null ||
                    !PluginInfo.IsOurWeaponName(__instance.WeaponInfo.weaponName)) return;

                List<Weapon> weapons = __instance.Weapons;
                if (weapons == null || weapons.Count == 0) return;

                if (_fWeaponIndex.GetValue(__instance) is not int index) return;
                if (index < weapons.Count) return;

                for (int i = 0; i < weapons.Count; i++)
                {
                    Weapon w = weapons[i];
                    if (w == null || !w.IsAttached()) continue;
                    if (w.GetAmmoLoaded() <= 0) continue;

                    _fWeaponIndex.SetValue(__instance, i);
                    if (!_loggedWrap)
                    {
                        _loggedWrap = true;
                        Plugin.Log.LogInfo(
                            $"[Tenpin] Wrapped WeaponStation.weaponIndex {index} -> {i} across " +
                            $"{weapons.Count} pod(s). WeaponStation.LaunchMount never wraps it for a " +
                            "MissileLauncher station, so without this the station goes silent after " +
                            "one rocket per pod with most of its ammo unfired.");
                    }
                    return;
                }

            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[Tenpin] Multi-pod cycling failed (firing still works " +
                                    $"for the first pass): {ex}");
            }
        }
    }
}
