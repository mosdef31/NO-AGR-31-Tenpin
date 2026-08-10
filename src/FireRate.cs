using System;
using System.Reflection;
using HarmonyLib;

namespace RocketPod
{

    internal static class FireRate
    {

        internal const float Interval = 0.08f;

        private static readonly FieldInfo? _fInterval =
            AccessTools.Field(typeof(MissileLauncher), "fireInterval");

        private static bool _logged;

        internal static void Apply(WeaponMount? mount)
        {
            try
            {
                if (mount == null || mount.prefab == null) return;

                var launcher = mount.prefab.GetComponentInChildren<MissileLauncher>();
                if (launcher == null) return;

                float before = _fInterval?.GetValue(launcher) is float f ? f : float.NaN;

                if (_fInterval == null)
                {

                    Plugin.Log.LogWarning(
                        "[Tenpin] Could not find MissileLauncher.fireInterval by reflection, so the " +
                        "launcher keeps the bundle's authored interval and the slower of the two " +
                        "figures wins. Re-check the decompile.");
                }
                else if (!Mathf_Approximately(before, Interval))
                {
                    _fInterval.SetValue(launcher, Interval);
                }

                float infoBefore = mount.info != null ? mount.info.fireInterval : float.NaN;
                if (mount.info != null && !Mathf_Approximately(infoBefore, Interval))
                {
                    mount.info.fireInterval = Interval;
                }

                if (!_logged)
                {
                    _logged = true;
                    Plugin.Log.LogInfo(
                        $"[Tenpin] Ripple interval set to {Interval:0.###} s on both " +
                        $"MissileLauncher.fireInterval (was {before:0.###}) and " +
                        $"WeaponInfo.fireInterval (was {infoBefore:0.###}). Both must match: " +
                        "WeaponStation.Ready() gates on the WeaponInfo one and MissileLauncher.Fire " +
                        "on its own, so the slower silently wins. Logged once.");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError(
                    $"[Tenpin] Could not set the ripple interval, the pod keeps the bundle's " +
                    $"authored rate: {ex}");
            }
        }

        private static bool Mathf_Approximately(float a, float b) =>
            !float.IsNaN(a) && Math.Abs(a - b) < 0.0001f;
    }
}
