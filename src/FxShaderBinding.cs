using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RocketPod
{

    internal static class FxShaderBinding
    {

        private const string PreferredShader = "Universal Render Pipeline/Particles/Unlit";

        private static bool _ran;

        internal static void RunOnce()
        {
            if (_ran) return;
            _ran = true;

            try
            {
                Apply();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError(
                    $"[Tenpin] Effect shader binding failed, so the plume may not render: {ex}");
            }

            try
            {
                RebindLit();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError(
                    $"[Tenpin] Lit rebind failed, so the pod may render magenta: {ex}");
            }
        }

        private const string LitShader = "Universal Render Pipeline/Lit";

        private static void RebindLit()
        {
            Shader? stock = StockLitShader(out string from);
            if (stock == null)
            {

                Plugin.Log.LogWarning(
                    "[Tenpin] The game's Lit shader was not found, so materials keep " +
                    "the bundle's copy.");
                return;
            }

            int rebound = 0, already = 0;
            foreach (Material m in OurBodyMaterials().Distinct())
            {
                if (ReferenceEquals(m.shader, stock)) { already++; continue; }
                m.shader = stock;
                rebound++;
            }

            if (rebound == 0 && already == 0) return;

            Plugin.Log.LogInfo(
                $"[Tenpin] Lit bind: {rebound} material(s) put on '{stock.name}' " +
                $"from {from}, {already} already on it.");
        }

        private static IEnumerable<Material> OurBodyMaterials()
        {
            foreach (GameObject root in OurPrefabs())
            {
                if (root == null) continue;
                foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (r == null || r is ParticleSystemRenderer) continue;
                    foreach (Material m in r.sharedMaterials)
                    {
                        if (m == null) continue;
                        if (NeedsShader(m) || m.shader.name == LitShader) yield return m;
                    }
                }
            }
        }

        private static Shader? StockLitShader(out string from)
        {
            from = "(nothing)";

            Encyclopedia? enc = GameData.EncyclopediaOrNull();
            if (enc?.missiles != null)
            {
                foreach (MissileDefinition md in enc.missiles)
                {
                    if (md == null || md.unitPrefab == null) continue;
                    if (PluginInfo.IsOurRound(md.jsonKey)) continue;

                    foreach (Renderer r in md.unitPrefab.GetComponentsInChildren<Renderer>(true))
                    {
                        if (r == null || r is ParticleSystemRenderer) continue;
                        foreach (Material m in r.sharedMaterials)
                        {
                            if (m == null || m.shader == null) continue;
                            if (m.shader.name != LitShader) continue;
                            from = $"stock missile '{md.jsonKey}'";
                            return m.shader;
                        }
                    }
                }
            }

            Shader? found = Shader.Find(LitShader);
            if (found != null)
            {
                from = $"Shader.Find(\"{LitShader}\")";
                return found;
            }

            return null;
        }

        private static void Apply()
        {
            List<Material> orphaned = OurMaterials().Where(NeedsShader).Distinct().ToList();
            if (orphaned.Count == 0) return;

            Shader? shader = FindShader(out string from);
            if (shader == null)
            {

                Plugin.Log.LogError(
                    $"[Tenpin] No particle shader found for {orphaned.Count} effect " +
                    "material(s), so they will not render.");
                return;
            }

            foreach (Material m in orphaned) m.shader = shader;

            Plugin.Log.LogInfo(
                $"[Tenpin] Bound '{shader.name}' to {orphaned.Count} effect " +
                $"material(s) from {from}.");
        }

        private static bool NeedsShader(Material m) =>
            m != null &&
            (m.shader == null ||
             m.shader.name.IndexOf("InternalError", StringComparison.OrdinalIgnoreCase) >= 0 ||
             m.shader.name.IndexOf("Hidden/", StringComparison.OrdinalIgnoreCase) == 0);

        private static IEnumerable<Material> OurMaterials()
        {
            foreach (GameObject root in OurPrefabs())
            {
                if (root == null) continue;
                foreach (ParticleSystemRenderer r in
                         root.GetComponentsInChildren<ParticleSystemRenderer>(true))
                {
                    if (r == null) continue;
                    foreach (Material m in r.sharedMaterials)
                        if (m != null) yield return m;
                }
            }
        }

        private static IEnumerable<GameObject> OurPrefabs()
        {
            GameObject? round = EncyclopediaRegistration.ResolvedMissile?.unitPrefab;
            if (round != null) yield return round;

            foreach (WeaponMount mount in EncyclopediaRegistration.ResolvedMounts)
            {
                if (mount == null || mount.prefab == null) continue;
                yield return mount.prefab;
            }
        }

        private static Shader? FindShader(out string from)
        {
            from = "(nothing)";

            Shader? stock = StockParticleShaders().FirstOrDefault(s => s != null);
            if (stock != null)
            {
                from = "a stock missile's own effects";
                return stock;
            }

            Shader? found = Shader.Find(PreferredShader);
            if (found != null)
            {
                from = $"Shader.Find(\"{PreferredShader}\")";
                return found;
            }

            return null;
        }

        private static IEnumerable<Shader> StockParticleShaders()
        {
            Encyclopedia? enc = GameData.EncyclopediaOrNull();
            if (enc?.missiles == null) yield break;

            foreach (MissileDefinition md in enc.missiles)
            {
                if (md == null || md.unitPrefab == null) continue;
                if (PluginInfo.IsOurRound(md.jsonKey)) continue;

                foreach (ParticleSystemRenderer r in
                         md.unitPrefab.GetComponentsInChildren<ParticleSystemRenderer>(true))
                {
                    if (r == null) continue;
                    foreach (Material m in r.sharedMaterials)
                        if (m != null && m.shader != null) yield return m.shader;
                }
            }
        }
    }
}
