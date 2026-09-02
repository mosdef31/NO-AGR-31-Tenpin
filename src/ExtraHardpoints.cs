using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RocketPod
{

    internal static class ExtraHardpoints
    {

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

        private static IEnumerable<string> NamesOf(WeaponManager wm)
        {
            if (!string.IsNullOrEmpty(wm.name)) yield return wm.name;

            Transform? root = wm.transform != null ? wm.transform.root : null;
            if (root != null && !string.IsNullOrEmpty(root.name)) yield return root.name;

            UnitDefinition? def = root != null ? root.GetComponent<Unit>()?.definition : null;
            if (def == null) yield break;
            if (!string.IsNullOrEmpty(def.unitName)) yield return def.unitName;
            if (!string.IsNullOrEmpty(def.code)) yield return def.code;
            if (!string.IsNullOrEmpty(def.name)) yield return def.name;
        }

        private static string Normalize(string s)
        {
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (char c in s)
                if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
            return sb.ToString();
        }

        private static bool GetsBothFamilies(string key, WeaponManager wm)
        {
            if (AiLoadout.PrefersHex(wm)) return true;

            string k = Normalize(key);
            return k == Normalize("COIN") || k == Normalize("CAS1");
        }

        private static readonly Dictionary<string, string[]> NamedMounts =
            new Dictionary<string, string[]>
            {

                ["Aryx_PropAttacker1"] = new[] { PluginInfo.MountKey, PluginInfo.MountKey51 },
            };

        private static string[]? NamedMountsFor(string key)
        {
            string k = Normalize(key);
            foreach (KeyValuePair<string, string[]> kv in NamedMounts)
                if (Normalize(kv.Key) == k) return kv.Value;
            return null;
        }

        private static bool Resolve(string name, Dictionary<string, WeaponManager> managers,
                                    List<WeaponManager> all, out WeaponManager found,
                                    out string how)
        {
            how = "";
            if (managers.TryGetValue(name, out found)) return true;

            string want = Normalize(name);
            if (want.Length == 0) return false;

            foreach (WeaponManager wm in all)
            {
                foreach (string candidate in NamesOf(wm))
                {
                    if (!Normalize(candidate).Contains(want)) continue;
                    found = wm;
                    how = candidate;
                    return true;
                }
            }

            return false;
        }

        internal static IList<string> MissingAircraft => _missing;

        private static List<string> _missing = new List<string>();

        internal static bool Complete { get; private set; }

        internal static void Apply(bool force = false)
        {
            if (Complete && !force) return;

            string spec = Plugin.ExtraHardpoints.Value;
            if (string.IsNullOrWhiteSpace(spec)) return;

            WeaponMount? mount = EncyclopediaRegistration.ResolvedMount;
            if (mount == null)
            {

                Plugin.Log.LogWarning(
                    "[Tenpin] ExtraHardpoints skipped: the WeaponMount has not resolved.");
                return;
            }

            var managers = new Dictionary<string, WeaponManager>(
                System.StringComparer.OrdinalIgnoreCase);
            var all = new List<WeaponManager>();
            foreach (WeaponManager wm in Resources.FindObjectsOfTypeAll<WeaponManager>())
            {
                string? root = wm.transform != null && wm.transform.root != null
                    ? wm.transform.root.name : null;
                if (!string.IsNullOrEmpty(root)) managers[root!] = wm;
                if (!managers.ContainsKey(wm.name)) managers[wm.name] = wm;
                all.Add(wm);
            }

            _passes++;

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
                        $"[Tenpin] ExtraHardpoints: cannot parse '{entry}'. " +
                        "Expected Name:indices.");
                    continue;
                }

                string name = entry.Substring(0, colon).Trim();
                string idxPart = entry.Substring(colon + 1).Trim();

                if (!Resolve(name, managers, all, out WeaponManager wm, out string how))
                {

                    missing.Add(name);
                    continue;
                }

                if (how.Length > 0)

                    Plugin.Log.LogInfo(
                        $"[Tenpin] ExtraHardpoints: '{name}' matched by name against " +
                        $"{DescribeAircraft(wm.name, wm)} (on \"{how}\").");

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
                            $"[Tenpin] ExtraHardpoints: {name} has {sets.Length} set(s), " +
                            $"so index {i} does not exist.");
                        continue;
                    }

                    HardpointSet s = sets[i];
                    s.weaponOptions ??= new List<WeaponMount>();

                    bool both = GetsBothFamilies(name, wm);

                    string[]? named = NamedMountsFor(name);

                    foreach (WeaponMount m in EncyclopediaRegistration.ResolvedMounts)
                    {
                        if (m == null) continue;

                        PluginInfo.MountSpec? mountSpec = PluginInfo.SpecFor(m.jsonKey);
                        if (mountSpec == null) continue;

                        if (named != null)
                        {
                            if (System.Array.IndexOf(named, m.jsonKey) < 0) continue;
                        }
                        else if (!both && mountSpec.Value.HexFamily) continue;

                        if (s.weaponOptions.Contains(m)) { alreadyThere++; continue; }

                        s.weaponOptions.Add(m);
                        attached++;
                        Plugin.Log.LogInfo(
                            $"[Tenpin] Attached '{m.jsonKey}' to {DescribeAircraft(name, wm)}[{i}] " +
                            $"\"{s.name}\" ({s.hardpoints?.Count ?? 0} hardpoint(s)).");
                    }
                }
            }

            _totalAttached += attached;

            if (attached > 0)
            {
                Plugin.Log.LogInfo(
                    $"[Tenpin] ExtraHardpoints: {_totalAttached} set(s) attached" +
                    (_passes > 1 ? $" (pass {_passes})" : "") + ".");

                if (!_explained)
                {
                    _explained = true;

                    Plugin.Log.LogInfo(
                        "[Tenpin] The bracketed name is the config key; the quoted name " +
                        "is the aircraft's own.");
                }
            }

            _missing = missing;
            Complete = missing.Count == 0;

            if (Complete)
            {
                if (!_reportedComplete)
                {
                    _reportedComplete = true;
                    if (_passes > 1)
                        Plugin.Log.LogInfo(
                            $"[Tenpin] ExtraHardpoints: all aircraft found after {_passes} passes.");
                }
            }
            else if (!_reportedMissing && _passes >= MissingReportPass)
            {
                _reportedMissing = true;
                Plugin.Log.LogInfo(
                    "[Tenpin] ExtraHardpoints: not installed, skipped: " +
                    string.Join(", ", missing.ToArray()) + ".");
            }
        }

        private static int _passes;

        private static int _totalAttached;

        private static bool _explained;

        private static bool _reportedComplete;
        private static bool _reportedMissing;

        private const int MissingReportPass = 45;
    }
}
