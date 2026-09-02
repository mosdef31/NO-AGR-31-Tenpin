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
                    $"[Tenpin] Pods not merged, so they appear as two weapons: {ex.Message}");
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
                $"[Tenpin] Repointed {moved} launcher(s) onto {canonicals.Count} " +
                "shared station(s), one per rocket.");
        }

    }
}
