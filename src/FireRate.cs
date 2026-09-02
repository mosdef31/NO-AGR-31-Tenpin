using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace RocketPod
{

    internal static class FireRate
    {
        private static readonly FieldInfo? _fInterval =
            AccessTools.Field(typeof(MissileLauncher), "fireInterval");

        private static readonly HashSet<string> _logged = new HashSet<string>();

        private static bool _warnedNoField;

        internal static void Apply(WeaponMount? mount)
        {
            try
            {
                if (mount == null || mount.prefab == null) return;

                float interval = PluginInfo.RippleIntervalFor(mount.jsonKey);
                if (interval <= 0f) return;

                var launcher = mount.prefab.GetComponentInChildren<MissileLauncher>();
                if (launcher == null) return;

                float before = _fInterval?.GetValue(launcher) is float f ? f : float.NaN;

                if (_fInterval == null)
                {

                    if (!_warnedNoField)
                    {
                        _warnedNoField = true;
                        Plugin.Log.LogWarning(
                            "[Tenpin] MissileLauncher.fireInterval not found, so pods keep the " +
                            "bundle's ripple rate.");
                    }
                }
                else if (!Mathf_Approximately(before, interval))
                {
                    _fInterval.SetValue(launcher, interval);
                }

                float infoBefore = mount.info != null ? mount.info.fireInterval : float.NaN;
                if (mount.info != null && !Mathf_Approximately(infoBefore, interval))
                {
                    mount.info.fireInterval = interval;
                }

                if (_logged.Add(mount.jsonKey ?? string.Empty))
                {

                    Plugin.Log.LogInfo(
                        $"[Tenpin] '{mount.jsonKey}' ripple {interval:0.###} s " +
                        $"(was {before:0.###} launcher, {infoBefore:0.###} info).");
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
