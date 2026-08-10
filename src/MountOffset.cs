using System;
using HarmonyLib;
using UnityEngine;

namespace RocketPod
{

    internal static class MountRideHeight
    {

        private const float AcrossFlats7 = 0.3332f;
        private const float AcrossFlats19 = 0.5141f;

        internal const float Flush7 = -AcrossFlats7 / 2f;
        internal const float Flush19 = -AcrossFlats19 / 2f;

        internal static float For(string? jsonKey) =>
            jsonKey == PluginInfo.MountKey ? Flush7 :
            jsonKey == PluginInfo.MountKey19 ? Flush19 : 0f;
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
                        $"[Tenpin] Mount ride height set per variant: the 7-tube hangs " +
                        $"{MountRideHeight.Flush7:0.###} m and the 19-tube {MountRideHeight.Flush19:0.###} m, " +
                        $"each half its own across-flats width so the pod's back sits flush against the " +
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
