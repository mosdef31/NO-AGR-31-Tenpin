using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RocketPod
{

    internal static class ExtraHardpoints
    {
        private static bool _applied;

        private static string DescribeAircraft(string key, WeaponManager wm)
        {
            try
            {
                Transform? root = wm.transform != null ? wm.transform.root : null;
                Unit? unit = root != null ? root.GetComponent<Unit>() : null;
                UnitDefinition? def = unit != null ? unit.definition : null;
                if (def == null) return key;

                string label = !string.IsNullOrEmpty(def.unitName) ? def.unitName : def.name;
                string code = !string.IsNullOrEmpty(def.code) ? $" / {def.code}" : "";
                return $"{key} (\"{label}\"{code})";
            }
            catch
            {
                return key;
            }
        }

        internal static void Apply(bool force = false)
        {
            if (_applied && !force) return;
            _applied = true;

            string spec = Plugin.ExtraHardpoints.Value;
            if (string.IsNullOrWhiteSpace(spec)) return;

            WeaponMount? mount = EncyclopediaRegistration.ResolvedMount;
            if (mount == null)
            {
                Plugin.Log.LogWarning(
                    "[Tenpin] ExtraHardpoints skipped: the WeaponMount has not resolved yet, so " +
                    "there is nothing to attach. This usually means the bundle did not load.");
                return;
            }

            var managers = new Dictionary<string, WeaponManager>(
                System.StringComparer.OrdinalIgnoreCase);
            foreach (WeaponManager wm in Resources.FindObjectsOfTypeAll<WeaponManager>())
            {
                string? root = wm.transform != null && wm.transform.root != null
                    ? wm.transform.root.name : null;
                if (!string.IsNullOrEmpty(root)) managers[root!] = wm;
                if (!managers.ContainsKey(wm.name)) managers[wm.name] = wm;
            }

            int attached = 0, alreadyThere = 0;
            var missing = new List<string>();

            foreach (string clause in spec.Split(';'))
            {
                string entry = clause.Trim();
                if (entry.Length == 0) continue;

                int colon = entry.LastIndexOf(':');
                if (colon <= 0)
                {
                    Plugin.Log.LogWarning(
                        $"[Tenpin] ExtraHardpoints: cannot parse '{entry}'. Expected Name:indices, " +
                        "e.g. 'AttackHelo1:2,3' or 'AttackHelo1:*'.");
                    continue;
                }

                string name = entry.Substring(0, colon).Trim();
                string idxPart = entry.Substring(colon + 1).Trim();

                if (!managers.TryGetValue(name, out WeaponManager wm))
                {

                    missing.Add(name);
                    continue;
                }

                HardpointSet[]? sets = wm.hardpointSets;
                if (sets == null || sets.Length == 0) continue;

                IEnumerable<int> indices = idxPart == "*"
                    ? Enumerable.Range(0, sets.Length)
                    : idxPart.Split(',')
                             .Select(s => int.TryParse(s.Trim(), out int v) ? v : -1)
                             .Where(v => v >= 0);

                foreach (int i in indices)
                {
                    if (i >= sets.Length)
                    {
                        Plugin.Log.LogWarning(
                            $"[Tenpin] ExtraHardpoints: {name} has {sets.Length} set(s), so index " +
                            $"{i} does not exist. Re-run the hardpoint scan - set indices are " +
                            "positional and move when a prefab is edited.");
                        continue;
                    }

                    HardpointSet s = sets[i];
                    s.weaponOptions ??= new List<WeaponMount>();

                    foreach (WeaponMount m in EncyclopediaRegistration.ResolvedMounts)
                    {
                        if (m == null) continue;
                        if (s.weaponOptions.Contains(m)) { alreadyThere++; continue; }

                        s.weaponOptions.Add(m);
                        attached++;
                        Plugin.Log.LogInfo(
                            $"[Tenpin] Attached '{m.jsonKey}' to {DescribeAircraft(name, wm)}[{i}] " +
                            $"\"{s.name}\" ({s.hardpoints?.Count ?? 0} hardpoint(s)).");
                    }
                }
            }

            if (attached > 0 || alreadyThere > 0)
                Plugin.Log.LogInfo(
                    $"[Tenpin] ExtraHardpoints: {attached} set(s) attached" +
                    (alreadyThere > 0 ? $", {alreadyThere} already had it" : "") + ".");

            if (attached > 0)
                Plugin.Log.LogInfo(
                    "[Tenpin] The bracketed name above each set is the config key; the quoted " +
                    "name and code beside it are the aircraft's own. Use those to confirm the " +
                    "pod landed on the airframes you meant, then narrow ExtraHardpoints - the " +
                    "defaults are deliberately wide because the key-to-aircraft mapping was " +
                    "guessed once and got the Vagrant wrong.");

            if (missing.Count > 0)
                Plugin.Log.LogInfo(
                    "[Tenpin] ExtraHardpoints: not installed, skipped: " +
                    string.Join(", ", missing.ToArray()) + ".");
        }
    }
}
