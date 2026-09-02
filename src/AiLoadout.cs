using System;
using HarmonyLib;
using NuclearOption.Networking;
using NuclearOption.SavedMission;
using UnityEngine;

namespace RocketPod
{

    internal static class AiLoadout
    {
        private static bool _logged;

        internal static bool RollWins() =>
            Plugin.AiForceLoadout.Value ||
            UnityEngine.Random.value < Mathf.Clamp01(Plugin.AiLoadoutChance.Value);

        internal static bool PrefersHex(WeaponManager? manager)
        {
            if (manager == null) return false;
            Aircraft? aircraft = Traverse.Create(manager).Field<Aircraft>("aircraft").Value;
            return PrefersHex(aircraft);
        }

        internal static bool PrefersHex(Aircraft? aircraft)
        {
            if (aircraft == null) return false;

            if (aircraft.pilots != null)
            {
                foreach (Pilot pilot in aircraft.pilots)
                {
                    if (pilot != null && pilot.pilotType == Pilot.PilotType.Helo) return true;
                }
            }

            string? name = aircraft.definition != null ? aircraft.definition.unitName : null;
            if (string.IsNullOrEmpty(name)) return false;

            foreach (string hint in Plugin.AiHexAircraft.Value.Split(','))
            {
                string trimmed = hint.Trim();
                if (trimmed.Length > 0 &&
                    name!.IndexOf(trimmed, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }

            return false;
        }

        internal static WeaponMount? OurMountFor(HardpointSet set, FactionHQ? hq = null,
                                                 Aircraft? aircraft = null)
        {
            if (set == null || set.weaponOptions == null) return null;

            bool hex = PrefersHex(aircraft);

            WeaponMount? best = null;
            int bestRounds = -1;

            foreach (WeaponMount option in set.weaponOptions)
            {
                if (!Legal(option, hq)) continue;

                PluginInfo.MountSpec? spec = PluginInfo.SpecFor(option.jsonKey);
                if (spec == null) continue;

                int rounds = spec.Value.Rounds;
                if (rounds <= 0) continue;

                if (hex != spec.Value.HexFamily) continue;

                if (rounds > bestRounds)
                {
                    best = option;
                    bestRounds = rounds;
                }
            }

            return bestRounds > 0 ? best : null;
        }

        private static bool Legal(WeaponMount? mount, FactionHQ? hq)
        {
            if (mount == null || mount.info == null) return false;
            if (mount.info.nuclear) return false;

            if (hq != null && hq.restrictedWeapons != null &&
                hq.restrictedWeapons.Contains(mount.name)) return false;

            return true;
        }

        private static WeaponMount? SelfDefenceFor(HardpointSet set, FactionHQ? hq)
        {
            if (set == null || set.weaponOptions == null) return null;

            WeaponMount? best = null;
            float bestScore = 0f;

            foreach (WeaponMount option in set.weaponOptions)
            {
                if (!Legal(option, hq)) continue;

                WeaponInfo? info = option != null ? option.info : null;
                if (info == null || !info.missile) continue;
                if (info.effectiveness.antiAir < 0.4f) continue;

                float score = info.effectiveness.antiAir + (info.targetRequirements.minIR > 0f ? 1f : 0f);
                if (score > bestScore)
                {
                    best = option;
                    bestScore = score;
                }
            }

            return best;
        }

        private static WeaponMount? GunFor(HardpointSet set, FactionHQ? hq)
        {
            if (set == null || set.weaponOptions == null) return null;

            foreach (WeaponMount option in set.weaponOptions)
            {
                if (Legal(option, hq) && option.info.gun) return option;
            }

            return null;
        }

        internal const string LoadoutName = "Saturation and Self Defence";

        internal static Loadout? BuildSaturationLoadout(WeaponManager weaponManager,
                                                        Loadout? basis = null,
                                                        FactionHQ? hq = null,
                                                        Aircraft? aircraft = null)
        {
            if (weaponManager == null || weaponManager.hardpointSets == null) return null;

            var loadout = new Loadout();
            int pods = 0;
            int airToAir = 0;
            int guns = 0;

            foreach (HardpointSet set in weaponManager.hardpointSets)
            {
                WeaponMount? ours = OurMountFor(set, hq, aircraft);

                if (airToAir < Plugin.AiSelfDefenceSets.Value)
                {
                    WeaponMount? aa = SelfDefenceFor(set, hq);
                    if (aa != null)
                    {
                        loadout.weapons.Add(aa);
                        airToAir++;
                        continue;
                    }
                }

                if (guns < 1 && ours == null)
                {
                    WeaponMount? gun = GunFor(set, hq);
                    if (gun != null)
                    {
                        loadout.weapons.Add(gun);
                        guns++;
                        continue;
                    }
                }

                if (ours != null)
                {
                    loadout.weapons.Add(ours);
                    pods++;
                    continue;
                }

                int index = loadout.weapons.Count;
                loadout.weapons.Add(
                    basis != null && index < basis.weapons.Count ? basis.weapons[index] : null!);
            }

            if (pods == 0) return null;

            if (!_logged)
            {
                _logged = true;
                Plugin.Log.LogInfo(
                    $"[Tenpin] AI loadout '{LoadoutName}': {pods} pod(s) " +
                    $"({(PrefersHex(aircraft) ? "hex" : "18-tube drum")}), " +
                    $"{airToAir} air-to-air, {guns} gun.");
                Plugin.Log.LogInfo(
                    $"[Tenpin] [AI] LoadoutChance = {Plugin.AiLoadoutChance.Value:0.00}" +
                    (Plugin.AiForceLoadout.Value ? ", FORCED by AiForceLoadout" : "") + ".");
            }

            return loadout;
        }
    }

    [HarmonyPatch(typeof(AircraftParameters), nameof(AircraftParameters.GetRandomStandardLoadout))]
    internal static class AircraftParameters_GetRandomStandardLoadout_TenpinPatch
    {
        [HarmonyPostfix]
        private static void Postfix(AircraftDefinition definition, FactionHQ hq, ref StandardLoadout __result)
        {
            try
            {
                if (!AiLoadout.RollWins()) return;
                if (definition == null || definition.unitPrefab == null) return;

                Aircraft prefab = definition.unitPrefab.GetComponent<Aircraft>();
                if (prefab == null || prefab.weaponManager == null) return;

                Loadout? ours = AiLoadout.BuildSaturationLoadout(
                    prefab.weaponManager, __result != null ? __result.loadout : null, hq, prefab);
                if (ours == null) return;

                __result = new StandardLoadout
                {
                    disabled = false,
                    Name = AiLoadout.LoadoutName,
                    FuelRatio = __result != null ? __result.FuelRatio : 1f,
                    loadout = ours,
                };
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError(
                    $"[Tenpin] AI loadout roll threw; the flight keeps the loadout the game " +
                    $"chose: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(Spawner), nameof(Spawner.SpawnAircraft))]
    internal static class Spawner_SpawnAircraft_TenpinLoadoutPatch
    {
        [HarmonyPrefix]
        private static void Prefix(Player player, GameObject prefab, ref Loadout loadout, FactionHQ HQ)
        {
            try
            {

                if (player != null) return;
                if (prefab == null) return;
                if (!AiLoadout.RollWins()) return;

                Aircraft aircraft = prefab.GetComponent<Aircraft>();
                if (aircraft == null || aircraft.weaponManager == null) return;

                Loadout? ours = AiLoadout.BuildSaturationLoadout(
                    aircraft.weaponManager, loadout, HQ, aircraft);
                if (ours == null) return;

                loadout = ours;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError(
                    $"[Tenpin] AI loadout roll threw; the flight keeps the loadout it was " +
                    $"spawned with: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(WeaponManager), nameof(WeaponManager.SelectAIAircraftWeapons))]
    internal static class WeaponManager_SelectAIAircraftWeapons_TenpinPatch
    {
        [HarmonyPostfix]
        private static void Postfix(WeaponManager __instance, ref Loadout __result)
        {
            try
            {
                if (!AiLoadout.RollWins()) return;

                Aircraft? owner = Traverse.Create(__instance).Field<Aircraft>("aircraft").Value;
                Loadout? ours = AiLoadout.BuildSaturationLoadout(
                    __instance, __result, owner != null ? owner.NetworkHQ : null, owner);
                if (ours != null) __result = ours;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError(
                    $"[Tenpin] AI loadout roll threw; the flight keeps the loadout the game " +
                    $"chose: {ex}");
            }
        }
    }
}
