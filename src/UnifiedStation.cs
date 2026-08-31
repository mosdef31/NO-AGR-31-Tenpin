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

            var canonicals = new System.Collections.Generic.Dictionary<string, WeaponInfo>();
            int moved = 0;

            foreach (WeaponMount mount in EncyclopediaRegistration.ResolvedMounts)
            {
                if (mount == null || mount.prefab == null) continue;

                PluginInfo.MountSpec? spec = PluginInfo.SpecFor(mount.jsonKey);
                if (spec == null) continue;
                string round = spec.Value.RoundKey;

                foreach (Weapon w in mount.prefab.GetComponentsInChildren<Weapon>(true))
                {
                    if (w == null || w.info == null) continue;

                    if (!canonicals.TryGetValue(round, out WeaponInfo canonical))
                    {
                        canonicals[round] = w.info;
                        continue;
                    }

                    if (ReferenceEquals(w.info, canonical)) continue;
                    w.info = canonical;
                    moved++;
                }
            }

            if (moved == 0) return;

            Plugin.Log.LogInfo(
                $"[Tenpin] Repointed {moved} launcher(s) so pods firing the same rocket share one " +
                $"station, across {canonicals.Count} rocket(s). WeaponManager keys a station off " +
                "reference equality of this asset, so two identical copies were two stations. Pods " +
                "firing DIFFERENT rockets keep their own, or the second weapon flies under the " +
                "first's name, icon and trajectory. The mounts keep their own WeaponInfo, so the " +
                "loadout still lists and prices every pod separately.");
        }

    }
}
