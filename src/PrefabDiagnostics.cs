using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace RocketPod
{

    internal static class PrefabDiagnostics
    {
        private static bool _ran;

        private const int MaxTransforms = 60;

        internal static void RunOnce()
        {
            if (_ran) return;
            _ran = true;

            try
            {
                WeaponMount? mount = EncyclopediaRegistration.ResolvedMount;
                if (mount == null || mount.prefab == null)
                {
                    Plugin.Log.LogWarning(
                        "[Tenpin] Render diagnostic skipped: no mount or no mounted prefab.");
                    return;
                }

                Plugin.Log.LogInfo("[Tenpin] ---- render diagnostic: OUR mounted prefab ----");
                DumpTree(mount.prefab);

                WeaponMount? donor = FindStockPodMount();
                if (donor?.prefab == null)
                {
                    Plugin.Log.LogInfo(
                        "[Tenpin] No stock pod mount found to compare against. The block above " +
                        "stands on its own.");
                }
                else
                {
                    Plugin.Log.LogInfo(
                        $"[Tenpin] ---- render diagnostic: STOCK reference '{donor.jsonKey}' " +
                        $"({donor.mountName}) ----");
                    DumpTree(donor.prefab);
                    Plugin.Log.LogInfo(
                        "[Tenpin] Compare the two blocks: layer, shader, scale or enabled " +
                        "state.");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[Tenpin] Render diagnostic threw: {ex}");
            }
        }

        private static void DumpTree(GameObject root)
        {
            Transform[] all = root.GetComponentsInChildren<Transform>(includeInactive: true);

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(includeInactive: true);
            Plugin.Log.LogInfo(
                $"[Tenpin]   root '{root.name}' layer={root.layer} ({LayerName(root.layer)}) " +
                $"activeSelf={root.activeSelf} transforms={all.Length} renderers={renderers.Length}");

            if (renderers.Length == 0)
            {

                Plugin.Log.LogError(
                    "[Tenpin]   NO Renderer in this prefab, so it draws nothing on the pylon.");
            }

            int shown = 0;
            foreach (Transform t in all)
            {
                if (shown++ >= MaxTransforms)
                {
                    Plugin.Log.LogInfo($"[Tenpin]   ... {all.Length - MaxTransforms} more transforms not shown.");
                    break;
                }

                var sb = new StringBuilder();
                sb.Append("[Tenpin]   ").Append(Indent(t, root.transform)).Append(t.name);
                sb.Append($" active={t.gameObject.activeSelf}");
                sb.Append($" layer={t.gameObject.layer}");
                sb.Append($" localPos={Fmt(t.localPosition)}");
                sb.Append($" localScale={Fmt(t.localScale)}");
                sb.Append($" lossyScale={Fmt(t.lossyScale)}");

                var mf = t.GetComponent<MeshFilter>();
                if (mf != null)
                {
                    Mesh? m = mf.sharedMesh;

                    sb.Append(m == null
                        ? " | MeshFilter with NULL sharedMesh"
                        : $" | mesh '{m.name}' verts={m.vertexCount} boundsSize={Fmt(m.bounds.size)}");
                }

                var r = t.GetComponent<Renderer>();
                if (r != null)
                {
                    sb.Append($" | {r.GetType().Name} enabled={r.enabled}");
                    sb.Append($" shadows={r.shadowCastingMode}");
                    sb.Append($" worldBounds={Fmt(r.bounds.size)}");
                    sb.Append($" materials={DescribeMaterials(r)}");
                }

                Plugin.Log.LogInfo(sb.ToString());
            }
        }

        private static string DescribeMaterials(Renderer r)
        {

            Material[] mats = r.sharedMaterials;
            if (mats == null || mats.Length == 0) return "NONE (nothing to draw with)";

            var parts = new List<string>(mats.Length);
            foreach (Material? m in mats)
            {
                if (m == null) { parts.Add("NULL"); continue; }

                Shader? s = m.shader;
                string shader = s == null
                    ? "NULL SHADER"
                    : (s.isSupported ? s.name : $"{s.name} (UNSUPPORTED)");

                if (s != null && s.name.IndexOf("InternalErrorShader", StringComparison.OrdinalIgnoreCase) >= 0)
                    shader += " <- the shader did not survive the bundle";

                parts.Add($"'{m.name}'/{shader}");
            }
            return string.Join(", ", parts.ToArray());
        }

        private static WeaponMount? FindStockPodMount()
        {
            Dictionary<string, WeaponMount>? lookup = Encyclopedia.WeaponLookup;
            if (lookup == null) return null;

            WeaponMount? fallback = null;
            foreach (WeaponMount m in lookup.Values)
            {
                if (m == null || m.prefab == null) continue;
                if (PluginInfo.IsOurMount(m.jsonKey)) continue;
                if (m.prefab.GetComponentInChildren<Renderer>(true) == null) continue;

                if (m.jsonKey != null &&
                    m.jsonKey.IndexOf("Rocket", StringComparison.OrdinalIgnoreCase) >= 0)
                    return m;

                fallback ??= m;
            }
            return fallback;
        }

        private static string Fmt(Vector3 v) => $"({v.x:F3},{v.y:F3},{v.z:F3})";

        private static string LayerName(int layer)
        {
            string n = LayerMask.LayerToName(layer);
            return string.IsNullOrEmpty(n) ? "unnamed" : n;
        }

        private static string Indent(Transform t, Transform root)
        {
            int depth = 0;
            for (Transform? p = t; p != null && p != root; p = p.parent) depth++;
            return new string(' ', depth * 2);
        }
    }
}
