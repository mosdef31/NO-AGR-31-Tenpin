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
                    $"[Tenpin] Binding a shader to the effect materials failed, so the plume may " +
                    $"render invisible. The rocket still flies: {ex}");
            }

            try
            {
                RebindLit();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError(
                    $"[Tenpin] Rebinding the pod and rocket materials onto the game's own Lit " +
                    $"shader failed. They may render magenta on some machines: {ex}");
            }
        }

        private const string LitShader = "Universal Render Pipeline/Lit";

        private static void RebindLit()
        {
            Shader? stock = StockLitShader(out string from);
            if (stock == null)
            {
                Plugin.Log.LogWarning(
                    "[Tenpin] Could not find the game's own Lit shader to rebind the pod and rocket " +
                    "materials onto, so they keep the copy the bundle shipped. If the pod renders " +
                    "magenta, that copy is missing a variant for this machine's graphics API.");
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
                $"[Tenpin] Lit bind: {rebound} body material(s) put on the game's own " +
                $"'{stock.name}' from {from}, {already} already on it. A bundle that ships its own " +
                "copy of this shader carries only the variants it was built with and renders magenta " +
                "on a machine using a different graphics API, which is the pink pod. Since the " +
                "2026-08-12 export the bundle ships no shader here at all and these arrive null.");
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
                    if (md.jsonKey == PluginInfo.MissileKey) continue;

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
                    $"[Tenpin] {orphaned.Count} effect material(s) ship without a shader (that is " +
                    "deliberate - see FxShaderBinding) and no particle shader could be found in " +
                    "the running game to bind. The effects will not render. This is cosmetic; the " +
                    "weapon is unaffected.");
                return;
            }

            foreach (Material m in orphaned) m.shader = shader;

            Plugin.Log.LogInfo(
                $"[Tenpin] Bound '{shader.name}' to {orphaned.Count} effect material(s), taken " +
                $"from {from}. The bundle deliberately ships no shader: packing one made the game " +
                "spend minutes loading its variants and never reach the menu.");
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
                if (md.jsonKey == PluginInfo.MissileKey) continue;

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
