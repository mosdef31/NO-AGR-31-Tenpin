using System;
using System.Collections.Generic;
using UnityEngine;

namespace RocketPod
{

    internal sealed class RoundVisuals : MonoBehaviour
    {
        private TenpinLauncher _launcher = null!;
        private readonly List<GameObject> _rounds = new();
        private int _lastShown = -1;
        private static bool _loggedMismatch;
        private static bool _loggedMissing;

        internal bool Bind(TenpinLauncher launcher)
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
                        $"[Tenpin] No round meshes on the prefab ('{containerName}', " +
                        $"'{prefix}*'), so the tubes will not empty.");
                }
                return false;
            }

            int full = launcher.GetFullAmmo();
            if (full != _rounds.Count && !_loggedMismatch)
            {
                _loggedMismatch = true;

                Plugin.Log.LogWarning(
                    $"[Tenpin] Pod has {_rounds.Count} round mesh(es) but {full} ammo, " +
                    "so the tubes empty out of step.");
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
}
