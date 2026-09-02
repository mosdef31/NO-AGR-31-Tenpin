using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace RocketPod
{

    internal static class LauncherSwap
    {
        private static int _swapped;
        private static int _refused;
        private static bool _summaryLogged;

        private static readonly HashSet<int> _seen = new HashSet<int>();

        internal static void Bind(MissileLauncher stock)
        {
            if (stock == null) return;
            if (!_seen.Add(stock.GetInstanceID())) return;

            GameObject host = stock.gameObject;

            TenpinLauncher ours = host.AddComponent<TenpinLauncher>();

            if (!ours.Adopt(stock))
            {
                UnityEngine.Object.Destroy(ours);
                _refused++;

                Plugin.Log.LogError(
                    $"[Tenpin] Launcher swap refused on '{host.name}': no missile " +
                    "definition or no launch transforms.");
                return;
            }

            ReplaceInStations(stock, ours);

            ours.Configure();

            UnityEngine.Object.Destroy(stock);
            _swapped++;

            Plugin.Log.LogInfo(
                $"[Tenpin] Launcher swap on '{host.name}': " +
                $"{ours.launchTransforms.Length} tube(s), ripple {ours.fireInterval:0.###} s, " +
                $"ammo {ours.ammo}.");
        }

        private static void ReplaceInStations(MissileLauncher stock, TenpinLauncher ours)
        {
            Unit? unit = stock.attachedUnit;
            if (unit == null) return;

            List<WeaponStation>? stations = unit.weaponStations;
            if (stations == null) return;

            foreach (WeaponStation station in stations)
            {
                if (station?.Weapons == null) continue;
                for (int i = 0; i < station.Weapons.Count; i++)
                {
                    if (!ReferenceEquals(station.Weapons[i], stock)) continue;
                    station.Weapons[i] = ours;
                    ours.SetWeaponStation(station);
                }
            }
        }

        internal static void LogSummary()
        {
            if (_summaryLogged || (_swapped == 0 && _refused == 0)) return;
            _summaryLogged = true;

            Plugin.Log.LogInfo(
                $"[Tenpin] Launcher swap: {_swapped} pod(s) on the native path, " +
                $"{_refused} refused.");
        }
    }

    [HarmonyPatch]
    internal static class Unit_SyncAmmo_DivergencePatch
    {

        [HarmonyTargetMethod]
        private static MethodBase? Target()
        {
            foreach (MethodInfo m in AccessTools.GetDeclaredMethods(typeof(Unit)))
            {
                if (!m.Name.StartsWith("UserCode_RpcSyncAmmoCount", StringComparison.Ordinal))
                    continue;

                ParameterInfo[] p = m.GetParameters();
                if (p.Length == 2 && p[0].ParameterType == typeof(byte)
                                  && p[1].ParameterType == typeof(int))
                    return m;
            }

            Plugin.Log.LogWarning(
                "[Tenpin] No UserCode_RpcSyncAmmoCount on Unit, so ammo divergence " +
                "is not measured.");
            return null;
        }

        [HarmonyPrefix]
        private static void Prefix(Unit __instance, byte stationIndex, int ammo)
        {
            try
            {
                if (__instance?.weaponStations == null) return;
                if (stationIndex >= __instance.weaponStations.Count) return;

                WeaponStation station = __instance.weaponStations[stationIndex];
                if (station?.Weapons == null) return;

                bool ours = false;
                foreach (Weapon w in station.Weapons)
                    if (w is TenpinLauncher) { ours = true; break; }
                if (!ours) return;

                LaunchTelemetry.NoteServerAmmo(ammo, station.Ammo);
            }
            catch
            {

            }
        }
    }

    [HarmonyPatch(typeof(MissileLauncher), "OnEnable")]
    internal static class MissileLauncher_OnEnable_SwapPatch
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(MissileLauncher __instance)
        {

            try
            {
                if (__instance.info == null) return;
                if (!PluginInfo.IsOurWeaponName(__instance.info.weaponName)) return;

                LauncherSwap.Bind(__instance);
            }
            catch (System.Exception e)
            {

                Plugin.Log.LogError($"[Tenpin] Launcher swap threw: {e}");
            }
        }
    }
}
