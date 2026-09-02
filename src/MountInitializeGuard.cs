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
                        $"[Tenpin] '{__instance.jsonKey}' has a NULL prefab, so this pod " +
                        "will not work.");
                    Plugin.Log.LogError(
                        "[Tenpin] Re-assign the mounted prefab in Unity and re-export.");
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
