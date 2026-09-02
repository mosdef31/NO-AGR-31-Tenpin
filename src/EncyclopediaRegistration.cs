using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace RocketPod
{

    [HarmonyPatch(typeof(Encyclopedia), "AfterLoad", new Type[0])]
    internal static class Encyclopedia_AfterLoad_RegistrationPatch
    {
        [HarmonyPrefix]
        private static void Prefix(Encyclopedia __instance)
        {

            try
            {
                EncyclopediaRegistration.EnsureInLists(__instance);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError(
                    $"[Tenpin] Encyclopedia registration threw, the pod may be unregistered: {ex}");
            }
        }
    }

    internal static class EncyclopediaRegistration
    {

        private static readonly List<WeaponMount> _mounts = new List<WeaponMount>();

        private static readonly List<MissileDefinition> _defs = new List<MissileDefinition>();

        private static MissileDefinition? _def;

        private static int _resolveAttempts;

        private static bool _resolveReported;
        private static bool _addedLogged;

        internal static IList<WeaponMount> ResolvedMounts => _mounts;

        internal static WeaponMount? ResolvedMount =>
            _mounts.FirstOrDefault(m => m != null && m.jsonKey == PluginInfo.MountKey)
            ?? _mounts.FirstOrDefault();

        internal static MissileDefinition? ResolvedMissile => _def;

        internal static IList<MissileDefinition> ResolvedMissiles => _defs;

        internal static void EnsureInLists(Encyclopedia enc)
        {
            if (enc == null) return;
            if (!TryResolveAssets()) return;

            bool added = false;

            foreach (WeaponMount mount in _mounts)
            {
                if (mount == null || enc.weaponMounts == null) continue;
                if (ContainsMount(enc, mount)) continue;
                enc.weaponMounts.Add(mount);
                added = true;
            }

            foreach (MissileDefinition d in _defs)
            {
                if (d == null || enc.missiles == null) continue;
                if (ContainsMissile(enc, d)) continue;
                enc.missiles.Add(d);
                added = true;
            }

            foreach (WeaponMount mount in _mounts)
            {
                StoreCard.Detach(mount);
                ApplyMountFieldsInitializeSkips(mount);
                StoreCard.Apply(mount, DefinitionFor(mount));
            }

            if (added && !_addedLogged)
            {
                _addedLogged = true;
                string list = string.Join(", ", _mounts
                    .Where(m => m != null)
                    .Select(m => $"'{m.jsonKey}' x{m.ammo}")
                    .ToArray());

                Plugin.Log.LogInfo(
                    "[Tenpin] Registered from the DLL prefix. The 'Duplicate JSON key' " +
                    "lines next are benign.");

                Plugin.Log.LogInfo(
                    $"[Tenpin] Registered {_mounts.Count} mount(s): {list}; " +
                    $"{_defs.Count} rocket(s): " +
                    string.Join(", ", _defs.Where(d => d != null)
                                           .Select(d => $"'{d.jsonKey}'").ToArray()) +
                    ".");
            }
        }

        internal static bool EnsureRegisteredAndRebuild()
        {
            Encyclopedia? enc = null;
            enc = GameData.EncyclopediaOrNull();
            if (enc == null) enc = UnityEngine.Object.FindObjectOfType<Encyclopedia>();
            if (enc == null)
            {
                Plugin.Log.LogWarning("[Tenpin] No Encyclopedia instance available yet.");
                return false;
            }

            bool alreadyPresent =
                Encyclopedia.WeaponLookup != null &&
                _mounts.Count > 0 &&
                _mounts.All(m => m == null || string.IsNullOrEmpty(m.jsonKey) ||
                                 Encyclopedia.WeaponLookup.ContainsKey(m.jsonKey));

            EnsureInLists(enc);

            if (!alreadyPresent)
            {
                MethodInfo? afterLoad =
                    AccessTools.Method(typeof(Encyclopedia), "AfterLoad", Type.EmptyTypes);
                if (afterLoad != null)
                {
                    afterLoad.Invoke(enc, null);
                    Plugin.Log.LogInfo("[Tenpin] Forced Encyclopedia.AfterLoad() to index late registration.");
                }
                else
                {
                    Plugin.Log.LogWarning("[Tenpin] Could not find Encyclopedia.AfterLoad() to force a rebuild.");
                }
            }

            bool ok = true;
            foreach (WeaponMount mount in _mounts)
            {
                if (mount == null || string.IsNullOrEmpty(mount.jsonKey)) continue;
                bool present = Encyclopedia.WeaponLookup != null &&
                               Encyclopedia.WeaponLookup.ContainsKey(mount.jsonKey);
                Plugin.Log.LogInfo($"[Tenpin] WeaponLookup contains '{mount.jsonKey}': {present}.");
                ok &= present;
            }
            return ok && _mounts.Count > 0;
        }

        private static readonly HashSet<string> _fixupLogged = new HashSet<string>();

        internal static void ReapplyDerivedFields(WeaponMount? mount)
        {
            if (mount == null || !PluginInfo.IsOurMount(mount.jsonKey)) return;
            StoreCard.Detach(mount);
            ApplyMountFieldsInitializeSkips(mount);
            StoreCard.Apply(mount, DefinitionFor(mount));
        }

        internal static MissileDefinition? DefinitionFor(WeaponMount? mount)
        {

            if (mount == null) return null;

            PluginInfo.MountSpec? spec = PluginInfo.SpecFor(mount.jsonKey);
            if (spec == null)
            {
                WarnOnce($"nodef:spec:{mount.jsonKey}",
                    $"[Tenpin] Mount '{mount.jsonKey}' is in the bundle but has no MountSpec row, " +
                    "so there is no way to know which rocket it fires. Add it to " +
                    "PluginInfo.Mounts. Its store card will stay blank until then.");
                return null;
            }

            foreach (MissileDefinition d in _defs)
                if (d != null && d.jsonKey == spec.Value.RoundKey) return d;

            WarnOnce($"nodef:round:{mount.jsonKey}",
                $"[Tenpin] Mount '{mount.jsonKey}' fires '{spec.Value.RoundKey}', which is not among " +
                $"the {_defs.Count} MissileDefinition(s) resolved so far. If this is the last word on " +
                "it the store card stays blank; if it clears on a later pass it was the bundle not " +
                "being resident yet.");
            return null;
        }

        private static readonly HashSet<string> _warnedOnce = new HashSet<string>();

        private static void WarnOnce(string key, string message)
        {
            if (!_warnedOnce.Add(key)) return;
            Plugin.Log.LogWarning(message);
        }

        private static void ApplyMountFieldsInitializeSkips(WeaponMount? mount)
        {
            if (mount == null || mount.info == null) return;

            if (mount.info.weaponPrefab != null) return;
            if (mount.prefab != null && mount.prefab.GetComponentInChildren<Gun>() != null) return;
            if (mount.Cargo) return;

            int expectedAmmo = PluginInfo.RoundsFor(mount.jsonKey);
            if (expectedAmmo > 0 && mount.ammo != expectedAmmo)
            {

                Plugin.Log.LogWarning(
                    $"[Tenpin] {mount.jsonKey}: WeaponMount.ammo was {mount.ammo}, " +
                    $"restored to {expectedAmmo}.");
                mount.ammo = expectedAmmo;
            }

            string expectedName = mount.ammo > 1
                ? $"{mount.info.weaponName} x{mount.ammo}"
                : (mount.info.weaponName ?? "");
            float expectedMass = mount.emptyMass + mount.info.massPerRound * mount.ammo;

            bool changed = mount.mountName != expectedName ||
                           Mathf.Abs(mount.mass - expectedMass) > 0.001f;

            mount.mountName = expectedName;
            mount.mass = expectedMass;

            string key = mount.jsonKey ?? mount.name ?? "?";
            if (changed && _fixupLogged.Add(key))
            {

                Plugin.Log.LogInfo(
                    $"[Tenpin] {key}: derived mountName '{expectedName}' and " +
                    $"mass {expectedMass:F0} kg.");
            }
        }

        private static bool ContainsMount(Encyclopedia enc, WeaponMount mount) =>
            enc.weaponMounts.Any(w => w == mount ||
                (w != null && !string.IsNullOrEmpty(w.jsonKey) && w.jsonKey == mount.jsonKey));

        private static bool ContainsMissile(Encyclopedia enc, MissileDefinition def) =>
            enc.missiles.Any(m => m == def ||
                (m != null && !string.IsNullOrEmpty(m.jsonKey) && m.jsonKey == def.jsonKey));

        private static bool TryResolveAssets()
        {
            if (_mounts.Count > 0) return true;

            _resolveAttempts++;

            _mounts.AddRange(FindLoadedBundleAssets<WeaponMount>(PluginInfo.MountAssetFragment));
            _defs.AddRange(FindLoadedBundleAssets<MissileDefinition>(
                PluginInfo.MissileDefAssetFragment));
            _def = _defs.FirstOrDefault(d => d != null && d.jsonKey == PluginInfo.MissileKey)
                   ?? _defs.FirstOrDefault();

            if (_mounts.Count == 0)
            {

                if (_resolveAttempts < 60 || _resolveReported) return false;
                _resolveReported = true;

                Plugin.Log.LogError(
                    "[Tenpin] Could not resolve the WeaponMount from any bundle or disk. " +
                    "The pod cannot register.");
                Plugin.Log.LogError(
                    $"[Tenpin] Expected an asset whose path contains " +
                    $"'{PluginInfo.MountAssetFragment}'.");

                DumpOurBundleContents();
                return false;
            }

            if (_def == null)
            {

                Plugin.Log.LogWarning(
                    "[Tenpin] No MissileDefinition resolved, so the rocket's entry " +
                    "will be incomplete.");
            }

            foreach (WeaponMount mount in _mounts)
            {
                NormalizeJsonKey(mount, ref mount.jsonKey);
            }
            foreach (MissileDefinition d in _defs)
                if (d != null) NormalizeJsonKey(d, ref d.jsonKey);

            var seen = new HashSet<string>();
            foreach (WeaponMount mount in _mounts)
            {
                string key = mount.jsonKey ?? "";
                if (!string.IsNullOrEmpty(key) && !seen.Add(key))
                {

                    Plugin.Log.LogError(
                        $"[Tenpin] Two WeaponMounts share the jsonKey '{key}'. " +
                        "Encyclopedia.AfterLoad will throw.");
                }
                if (!string.IsNullOrEmpty(key) && !PluginInfo.IsOurMount(key))
                {

                    Plugin.Log.LogWarning(
                        $"[Tenpin] WeaponMount jsonKey '{key}' is none of " +
                        $"{PluginInfo.MountKeyList}. Fix it in Unity.");
                }
            }

            foreach (WeaponMount mount in _mounts)
            {
                FireRate.Apply(mount);
            }

            return true;
        }

        private static List<T> FindLoadedBundleAssets<T>(string nameFragment)
            where T : UnityEngine.Object
        {
            var found = new List<T>();
            foreach (AssetBundle bundle in AssetBundle.GetAllLoadedAssetBundles())
            {
                if (bundle == null || bundle.isStreamedSceneAssetBundle) continue;

                string[] names;
                try { names = bundle.GetAllAssetNames(); }
                catch { continue; }

                foreach (string n in names)
                {
                    if (n.IndexOf(nameFragment, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    T asset = bundle.LoadAsset<T>(n);
                    if (asset != null && !found.Contains(asset)) found.Add(asset);
                }
            }
            return found;
        }

        private static void NormalizeJsonKey(UnityEngine.Object asset, ref string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            string trimmed = key.Trim();
            if (trimmed == key) return;

            Plugin.Log.LogWarning(
                $"[Tenpin] '{asset.name}' jsonKey has stray whitespace: '{key}'. " +
                $"Trimmed to '{trimmed}'.");
            key = trimmed;
        }

        private static void DumpOurBundleContents()
        {
            try
            {
                bool foundAny = false;
                foreach (AssetBundle bundle in AssetBundle.GetAllLoadedAssetBundles())
                {
                    if (bundle == null || bundle.isStreamedSceneAssetBundle) continue;

                    string[] names;
                    try { names = bundle.GetAllAssetNames(); }
                    catch { continue; }

                    var ours = names
                        .Where(n => n.IndexOf("tenpin", StringComparison.OrdinalIgnoreCase) >= 0)
                        .ToArray();
                    if (ours.Length == 0) continue;

                    foundAny = true;
                    Plugin.Log.LogError(
                        $"[Tenpin] Bundle '{bundle.name}' contains {ours.Length} Tenpin asset(s):");
                    foreach (string n in ours)
                    {
                        Plugin.Log.LogError($"[Tenpin]     {n}");
                    }
                }

                if (!foundAny)
                {
                    Plugin.Log.LogError(
                        "[Tenpin] No loaded bundle has any asset with 'tenpin' in its path.");
                    Plugin.Log.LogError(
                        "[Tenpin] Either the bundle did not load, or nothing was assigned to it.");
                }
                else
                {

                    Plugin.Log.LogError(
                        "[Tenpin] If that list has no WeaponMount, tick it into the bundle " +
                        "and re-export.");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[Tenpin] Could not enumerate bundle contents: {ex.Message}");
            }
        }

        private static T? FindLoadedBundleAsset<T>(string nameFragment) where T : UnityEngine.Object
        {
            foreach (AssetBundle bundle in AssetBundle.GetAllLoadedAssetBundles())
            {
                if (bundle == null || bundle.isStreamedSceneAssetBundle) continue;

                string? match;
                try
                {
                    match = bundle.GetAllAssetNames()
                                  .FirstOrDefault(n => n.IndexOf(nameFragment,
                                      StringComparison.OrdinalIgnoreCase) >= 0);
                }
                catch
                {
                    continue;
                }

                if (match == null) continue;

                T asset = bundle.LoadAsset<T>(match);
                if (asset != null) return asset;
            }
            return null;
        }

        private static AssetBundle? TryLoadBundleFromDisk()
        {
            try
            {
                string root = Paths.PluginPath;
                string? file = Directory
                    .EnumerateFiles(root, "*.nobp", SearchOption.AllDirectories)
                    .FirstOrDefault(p => Path.GetFileName(p)
                        .IndexOf("tenpin", StringComparison.OrdinalIgnoreCase) >= 0);

                if (file == null)
                {
                    Plugin.Log.LogWarning($"[Tenpin] No tenpin .nobp bundle found under '{root}'.");
                    return null;
                }
                return AssetBundle.LoadFromFile(file);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[Tenpin] Disk bundle load failed: {ex.Message}");
                return null;
            }
        }
    }
}
