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
