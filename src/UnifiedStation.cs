using System;
using UnityEngine;

namespace RocketPod
{

    internal static class UnifiedStation
    {
        private static bool _ran;

        internal static void RunOnce()
        {
            if (_ran) return;
            _ran = true;

            try
            {
                Apply();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning(
                    $"[Tenpin] Could not put both pods on one station, so they will appear as two " +
                    $"separate weapons. Everything else is unaffected: {ex.Message}");
            }
        }

        private static void Apply()
        {
            WeaponInfo? canonical = LauncherInfo(PluginInfo.MountKey);
            if (canonical == null)
            {
                Plugin.Log.LogWarning(
                    "[Tenpin] The 7-tube's launcher has no WeaponInfo, so there is nothing to put " +
                    "the heavy pod's station on. Both pods still work, as two stations.");
                return;
            }

            int moved = 0;
            foreach (WeaponMount mount in EncyclopediaRegistration.ResolvedMounts)
            {
                if (mount == null || mount.jsonKey == PluginInfo.MountKey) continue;
                if (mount.prefab == null) continue;

                foreach (Weapon w in mount.prefab.GetComponentsInChildren<Weapon>(true))
                {
                    if (w == null || ReferenceEquals(w.info, canonical)) continue;
                    w.info = canonical;
                    moved++;
                }
            }

            if (moved == 0) return;

            Plugin.Log.LogInfo(
                $"[Tenpin] Both pods now share one weapon station: repointed {moved} launcher(s) at " +
                $"'{canonical.weaponName}'s WeaponInfo. WeaponManager keys a station off reference " +
                "equality of this asset, so two identical copies were two stations. The mounts keep " +
                "their own WeaponInfo, so the loadout still lists and prices them separately.");
        }

        private static WeaponInfo? LauncherInfo(string jsonKey)
        {
            foreach (WeaponMount mount in EncyclopediaRegistration.ResolvedMounts)
            {
                if (mount == null || mount.jsonKey != jsonKey || mount.prefab == null) continue;

                foreach (Weapon w in mount.prefab.GetComponentsInChildren<Weapon>(true))
                    if (w != null && w.info != null) return w.info;
            }

            return null;
        }
    }
}
