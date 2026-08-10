using System;
using HarmonyLib;
using UnityEngine;

namespace RocketPod
{

    [HarmonyPatch(typeof(WeaponMount), "Initialize")]
    internal static class WeaponMount_Initialize_HideWeaponPrefabPatch
    {
        [HarmonyPrefix]
        private static void Prefix(WeaponMount __instance, ref object? __state)
        {
            __state = null;
            try
            {
                if (__instance == null || __instance.info == null) return;

                if (!PluginInfo.IsOurMount(__instance.jsonKey)) return;
                if (__instance.info.weaponPrefab == null) return;

                __state = __instance.info.weaponPrefab;
                __instance.info.weaponPrefab = null;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[Tenpin] Could not hide weaponPrefab from Initialize: {ex}");
            }
        }

        [HarmonyPostfix]
        private static void Postfix(WeaponMount __instance, object? __state)
        {
            try
            {
                if (__state is GameObject prefab && __instance != null && __instance.info != null)
                {
                    __instance.info.weaponPrefab = prefab;
                }

                EncyclopediaRegistration.ReapplyDerivedFields(__instance);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError(
                    "[Tenpin] Could not restore weaponPrefab after Initialize, so the loadout " +
                    $"store card will read empty this session: {ex}");
            }
        }
    }

    internal static class StoreCard
    {

        private static readonly System.Collections.Generic.HashSet<string> _logged =
            new System.Collections.Generic.HashSet<string>();

        internal static void Detach(WeaponMount? mount)
        {
            if (mount?.info == null) return;
            mount.info.weaponPrefab = null;
        }

        internal static void Apply(WeaponMount? mount, MissileDefinition? def)
        {
            try
            {
                if (mount == null || mount.info == null) return;
                string key = mount.jsonKey ?? mount.name ?? "?";

                GameObject? round = def != null ? def.unitPrefab : null;

                bool answerable = round != null &&
                                  round.GetComponent<MissileSeeker>() != null &&
                                  round.GetComponent<Missile>() != null;

                if (Plugin.StoreCardFields.Value && answerable)
                {
                    mount.info.weaponPrefab = round;
                }
                else if (Plugin.StoreCardFields.Value && round != null && !_logged.Contains(key))
                {
                    Plugin.Log.LogWarning(
                        "[Tenpin] The round prefab has no MissileSeeker or no Missile component, " +
                        "so weaponPrefab is being left null and the loadout card stays empty. " +
                        "AircraftSelectionMenu dereferences both without a null check, and a " +
                        "throw inside the loadout screen is worse than a blank card.");
                }

                float cost = Mathf.Max(0f, Plugin.RoundCostMillions.Value);
                if (cost > 0f)
                {

                    if (def != null) def.value = cost;
                    mount.info.costPerRound = cost;
                }

                float tolerance = Mathf.Max(0f, Plugin.RoundDamageTolerance.Value);
                if (def != null && tolerance > 0f)
                {
                    def.damageTolerance = tolerance;
                }

                if (_logged.Add(key))
                {
                    string seeker = "unknown";
                    if (round != null)
                    {
                        MissileSeeker? s = round.GetComponent<MissileSeeker>();
                        if (s != null) seeker = s.GetSeekerType();
                    }
                    Plugin.Log.LogInfo(
                        $"[Tenpin] {key}: store card wired - seeker '{seeker}', price " +
                        $"${cost * 1000f:0.#}k a round (${cost * 1000f * mount.ammo:0.#}k " +
                        $"a pod of {mount.ammo}), damageTolerance {tolerance:0.##}. The card's seeker, AP, HE, " +
                        "RCS and price all read off WeaponInfo.weaponPrefab, which this pod " +
                        "authors null so that WeaponMount.Initialize does not collapse it to " +
                        "ammo 1. Logged once per session.");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError(
                    $"[Tenpin] Store card wiring failed, the loadout card keeps the bundle's " +
                    $"own values: {ex}");
            }
        }
    }
}
