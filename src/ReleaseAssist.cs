using System;
using HarmonyLib;
using RocketPod.Hud;
using UnityEngine;

namespace RocketPod
{

    [HarmonyPatch(typeof(WeaponManager), nameof(WeaponManager.Fire))]
    internal static class WeaponManager_Fire_ReleaseAssistPatch
    {

        internal static class TriggerWatch
        {

            private const float UpAfter = 0.08f;

            private static WeaponStation? _station;
            private static float _at;

            internal static void Note(WeaponStation? station)
            {
                if (station == null) return;
                _station = station;
                _at = Time.timeSinceLevelLoad;
            }

            internal static bool Watching(WeaponStation? station) =>
                station != null && ReferenceEquals(_station, station);

            internal static bool Down(WeaponStation? station) =>
                Watching(station) && Time.timeSinceLevelLoad - _at <= UpAfter;

            internal static void Clear() => _at = -99f;
        }

        private const float StrayFactor = 2f;

        internal static bool Suspended;

        internal static void Toggle()
        {
            Suspended = !Suspended;
            _released = false;
            Plugin.Log.LogInfo($"[Tenpin] Release assist {(Suspended ? "OFF - manual trigger" : "on")}.");
        }

        internal static bool Armed => Plugin.ReleaseAssist.Value && !Suspended;

        private static bool _released;
        private static bool _loggedRelease;
        private static bool _loggedStop;

        [HarmonyPrefix]
        private static bool Prefix(WeaponManager __instance)
        {
            try
            {
                if (!IsOurStation(__instance)) return true;

                TriggerWatch.Note(__instance.currentWeaponStation);

                if (!Armed) return true;

                TenpinHud.Firing s = TenpinHud.Solution;

                if (!s.Fresh || !s.InReach)
                {
                    _released = false;
                    return true;
                }

                if (!_released)
                {
                    if (!s.OnTarget) return false;

                    _released = true;

                    if (!_loggedRelease)
                    {
                        _loggedRelease = true;
                        Plugin.Log.LogInfo(
                            $"[Tenpin] Release assist let a salvo go at {s.Miss:F0} m miss against a " +
                            $"{s.Tolerance:F0} m tolerance, and will close again past " +
                            $"{s.Tolerance * StrayFactor:F0} m. Logged once.");
                    }

                    return true;
                }

                if (s.Miss <= s.Tolerance * StrayFactor) return true;

                _released = false;
                TriggerWatch.Clear();

                if (!_loggedStop)
                {
                    _loggedStop = true;
                    Plugin.Log.LogInfo(
                        $"[Tenpin] Release assist stopped a salvo at {s.Miss:F0} m, past the " +
                        $"{s.Tolerance * StrayFactor:F0} m stray limit. Bring the pipper back and it " +
                        "releases again on its own. Logged once.");
                }

                return false;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning(
                    $"[Tenpin] Release assist failed and is standing aside, so the trigger fires " +
                    $"normally: {ex.Message}");
                return true;
            }
        }

        private static bool IsOurStation(WeaponManager wm)
        {
            WeaponStation? station = wm?.currentWeaponStation;
            return station?.WeaponInfo != null &&
                   station.WeaponInfo.weaponName == PluginInfo.WeaponInfoName;
        }
    }
}
