using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace RocketPod
{

    internal sealed class RoundVisuals : MonoBehaviour
    {
        private MissileLauncher _launcher = null!;
        private readonly List<GameObject> _rounds = new();
        private int _lastShown = -1;
        private static bool _loggedMismatch;
        private static bool _loggedMissing;

        internal bool Bind(MissileLauncher launcher)
        {
            _launcher = launcher;

            string containerName = Plugin.RoundContainerName.Value;
            string prefix = Plugin.RoundNamePrefix.Value;

            Transform? container = null;
            if (!string.IsNullOrWhiteSpace(containerName))
            {
                foreach (Transform t in launcher.GetComponentsInChildren<Transform>(includeInactive: true))
                {
                    if (t.name != containerName) continue;
                    container = t;
                    break;
                }
            }

            if (container != null)
            {
                for (int i = 0; i < container.childCount; i++)
                    _rounds.Add(container.GetChild(i).gameObject);
            }
            else if (!string.IsNullOrWhiteSpace(prefix))
            {

                var found = new List<Transform>();
                foreach (Transform t in launcher.GetComponentsInChildren<Transform>(includeInactive: true))
                {
                    if (t == launcher.transform) continue;
                    if (!t.name.StartsWith(prefix, StringComparison.Ordinal)) continue;
                    found.Add(t);
                }
                found.Sort((a, b) => TrailingIndex(a.name).CompareTo(TrailingIndex(b.name)));
                foreach (Transform t in found) _rounds.Add(t.gameObject);
            }

            if (_rounds.Count == 0)
            {
                if (!_loggedMissing)
                {
                    _loggedMissing = true;
                    Plugin.Log.LogInfo(
                        $"[Tenpin] No visible round meshes on the mounted prefab (looked for a child " +
                        $"named '{containerName}', then for children named '{prefix}*'), so firing will " +
                        "not empty the tubes. This is an AUTHORING gap, not an error - the mounted " +
                        "prefab ships with launch transforms only. See UNITY-SETUP.md step 11b. " +
                        "Note the rounds must NOT be parented under Tube0..6: MissileLauncher.OnEnable " +
                        "deactivates every launch transform, which would hide them permanently.");
                }
                return false;
            }

            int full = launcher.GetFullAmmo();
            if (full != _rounds.Count && !_loggedMismatch)
            {
                _loggedMismatch = true;
                Plugin.Log.LogWarning(
                    $"[Tenpin] Pod has {_rounds.Count} visible round mesh(es) but {full} rounds of " +
                    "ammo. The tubes will empty out of step with the ammo count. Author one mesh per " +
                    "tube, or set the launcher's Ammo to match.");
            }

            Apply(force: true);
            return true;
        }

        private static int TrailingIndex(string name)
        {
            int i = name.Length;
            while (i > 0 && char.IsDigit(name[i - 1])) i--;
            if (i == name.Length) return int.MaxValue;
            return int.TryParse(name.Substring(i), out int n) ? n : int.MaxValue;
        }

        private void Update()
        {
            if (_launcher == null) { enabled = false; return; }
            Apply(force: false);
        }

        private void Apply(bool force)
        {

            int shown = Mathf.Clamp(_launcher.GetAmmoLoaded(), 0, _rounds.Count);
            if (!force && shown == _lastShown) return;
            _lastShown = shown;

            int spent = _rounds.Count - shown;
            for (int i = 0; i < _rounds.Count; i++)
            {
                GameObject go = _rounds[i];
                if (go == null) continue;
                bool visible = i >= spent;
                if (go.activeSelf != visible) go.SetActive(visible);
            }
        }
    }

    [HarmonyPatch(typeof(MissileLauncher), "OnEnable")]
    internal static class MissileLauncher_OnEnable_RoundVisualsPatch
    {
        [HarmonyPostfix]
        private static void Postfix(MissileLauncher __instance)
        {
            try
            {
                if (!Plugin.HideFiredRounds.Value) return;
                if (__instance.info == null) return;
                if (__instance.info.weaponName != PluginInfo.WeaponInfoName) return;
                if (__instance.GetComponent<RoundVisuals>() != null) return;

                var visuals = __instance.gameObject.AddComponent<RoundVisuals>();
                if (!visuals.Bind(__instance)) UnityEngine.Object.Destroy(visuals);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[Tenpin] Round visuals failed (firing is unaffected): {ex}");
            }
        }
    }
}
