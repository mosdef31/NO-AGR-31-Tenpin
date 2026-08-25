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
                    $"[Tenpin] Launcher swap REFUSED on '{host.name}': the stock MissileLauncher " +
                    "carried no missile definition or no launch transforms, so there was nothing " +
                    "to copy. The game's own component is left in place, which means this pod " +
                    "fires for the HOST ONLY. Check the prefab's MissileLauncher fields in Unity.");
                return;
            }

            ReplaceInStations(stock, ours);

            ours.Configure();

            UnityEngine.Object.Destroy(stock);
            _swapped++;

            Plugin.Log.LogInfo(
                $"[Tenpin] Launcher swap on '{host.name}': MissileLauncher -> TenpinLauncher, " +
                $"{ours.launchTransforms.Length} tube(s), ripple {ours.fireInterval:0.###} s, " +
                $"ammo {ours.ammo}. The stock component gates its spawn on owner.LocalSim, which " +
                "is the wrong flag on an aircraft and is why the pod never worked for a client; " +
                "ours gates on IsServer and hands the shot to Aircraft.CmdLaunchMissile the way " +
                "MountedMissile does.");
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
                $"[Tenpin] Launcher swap: {_swapped} pod(s) on the native launch path, " +
                $"{_refused} refused. A client's shot now travels the game's own " +
                "Aircraft.CmdLaunchMissile as a throttled trigger-hold heartbeat rather than " +
                "one command per round, which is what the 15/s rate limiter needs.");
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
                "[Tenpin] No UserCode_RpcSyncAmmoCount(byte, int) on Unit, so the ammo " +
                "divergence measurement is off for this game build. Everything else is " +
                "unaffected. The Mirage weaver renames these per build; see the note above.");
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
                if (__instance.info.weaponName != PluginInfo.WeaponInfoName) return;

                LauncherSwap.Bind(__instance);
            }
            catch (System.Exception e)
            {

                Plugin.Log.LogError($"[Tenpin] Launcher swap threw: {e}");
            }
        }
    }
}
