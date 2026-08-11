using System;
using HarmonyLib;

namespace RocketPod
{

    [HarmonyPatch(typeof(WeaponMount), nameof(WeaponMount.Initialize))]
    internal static class WeaponMount_Initialize_NullPrefabGuard
    {
        private static bool _warned;

        [HarmonyPrefix]
        private static bool Prefix(WeaponMount __instance)
        {
            try
            {
                if (__instance == null) return true;
                if (!PluginInfo.IsOurMount(__instance.jsonKey)) return true;
                if (__instance.prefab != null) return true;

                if (!_warned)
                {
                    _warned = true;
                    Plugin.Log.LogError(
                        $"[Tenpin] '{__instance.jsonKey}' has a NULL prefab, so " +
                        "WeaponMount.Initialize was skipped to keep the game running. THE POD " +
                        "WILL NOT WORK - it has no mounted prefab. The bundle's WeaponMount asset " +
                        "has lost its prefab reference, which happens when a Unity script re-saves " +
                        "the mounted prefab and SaveAsPrefabAsset regenerates the root's local " +
                        "fileID. Re-assign it in Unity and re-export. Without this guard the " +
                        "unchecked dereference inside Initialize throws through " +
                        "Encyclopedia.Preload and MainMenu.StartAsync never completes, so the " +
                        "MAIN MENU NEVER APPEARS.");
                }

                return false;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[Tenpin] Mount initialize guard failed: {ex}");
                return true;
            }
        }
    }
}
