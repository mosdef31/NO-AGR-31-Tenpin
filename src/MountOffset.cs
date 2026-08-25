using System;
using HarmonyLib;
using UnityEngine;

namespace RocketPod
{

    internal static class MountRideHeight
    {

        internal static float For(string? jsonKey) =>
            PluginInfo.SpecFor(jsonKey)?.FlushOffset ?? 0f;

    }

    [HarmonyPatch(typeof(Hardpoint), "SpawnMount")]
    internal static class Hardpoint_SpawnMount_OffsetPatch
    {
        private static bool _logged;

        [HarmonyPostfix]
        private static void Postfix(WeaponMount weaponMount, GameObject __result)
        {
            try
            {
                if (__result == null || weaponMount == null) return;
                if (!PluginInfo.IsOurMount(weaponMount.jsonKey)) return;

                float y = MountRideHeight.For(weaponMount.jsonKey);
                if (Mathf.Approximately(y, 0f)) return;

                Transform t = __result.transform;
                Vector3 p = t.localPosition;

                if (Mathf.Approximately(p.y, y)) return;

                t.localPosition = new Vector3(p.x, y, p.z);

                if (!_logged)
                {
                    _logged = true;
                    Plugin.Log.LogInfo(
                        $"[Tenpin] Mount ride height is per variant, from PluginInfo.Mounts: " +
                        string.Join("; ", Array.ConvertAll(
                            PluginInfo.Mounts,
                            m => $"{m.Shape} hangs {m.FlushOffset:0.###} m")) +
                        $". Each is half the pod's own width, so its back sits flush against the " +
                        $"hardpoint. This spawn was '{weaponMount.jsonKey}' at {y:0.###} m " +
                        $"(prefab authored {p.y:0.###}). Logged once.");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError(
                    $"[Tenpin] Could not apply the mount offset, the pod keeps the prefab's own " +
                    $"ride height: {ex}");
            }
        }
    }
}
