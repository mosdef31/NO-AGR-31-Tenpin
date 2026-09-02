using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RocketPod
{

    internal static class TextureRescue
    {

        private static readonly Dictionary<string, Dictionary<string, string>> Maps =
            new Dictionary<string, Dictionary<string, string>>
            {
                ["Tenpin_Body"] = new Dictionary<string, string>
                {
                    ["_BaseMap"] = "RocketPod_Body_BaseColor",
                    ["_MainTex"] = "RocketPod_Body_BaseColor",
                    ["_BumpMap"] = "RocketPod_Body_Normal",
                    ["_MetallicGlossMap"] = "RocketPod_Body_MetallicGloss",
                    ["_OcclusionMap"] = "RocketPod_Body_Occlusion",
                    ["_ParallaxMap"] = "RocketPod_Body_Height",
                },
                ["Tenpin_PodBody19"] = new Dictionary<string, string>
                {
                    ["_BaseMap"] = "RocketPod19_Body_BaseColor",
                    ["_MainTex"] = "RocketPod19_Body_BaseColor",
                    ["_BumpMap"] = "RocketPod19_Body_Normal",
                    ["_MetallicGlossMap"] = "RocketPod19_Body_MetallicGloss",
                    ["_OcclusionMap"] = "RocketPod19_Body_Occlusion",
                    ["_ParallaxMap"] = "RocketPod19_Body_Height",
                },
                ["Tenpin_RocketBody"] = new Dictionary<string, string>
                {
                    ["_BaseMap"] = "TenpinRocket_BaseColor",
                    ["_MainTex"] = "TenpinRocket_BaseColor",
                    ["_BumpMap"] = "TenpinRocket_Normal",
                    ["_MetallicGlossMap"] = "TenpinRocket_MetallicGloss",
                },
            };

        private static bool _ran;
        private static Dictionary<string, Texture>? _textures;

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
                Plugin.Log.LogWarning(
                    $"[Tenpin] Could not check the pod's textures. If it renders white, that is " +
                    $"why: {ex.Message}");
            }
        }

        private static void Apply()
        {
            var repaired = new List<string>();
            var missing = new List<string>();

            foreach (Material m in OurBodyMaterials().Distinct())
            {

                string? key = Maps.Keys.FirstOrDefault(
                    k => m.name.StartsWith(k, StringComparison.Ordinal));
                if (key == null) continue;

                foreach (KeyValuePair<string, string> slot in Maps[key])
                {
                    if (!m.HasProperty(slot.Key)) continue;
                    if (m.GetTexture(slot.Key) != null) continue;

                    Texture? tex = FindTexture(slot.Value);
                    if (tex == null) { missing.Add($"{key}.{slot.Key} ({slot.Value})"); continue; }

                    m.SetTexture(slot.Key, tex);
                    repaired.Add($"{key}.{slot.Key}");
                }

                if (m.GetTexture("_BumpMap") != null) m.EnableKeyword("_NORMALMAP");
                if (m.GetTexture("_MetallicGlossMap") != null) m.EnableKeyword("_METALLICSPECGLOSSMAP");
                if (m.GetTexture("_OcclusionMap") != null) m.EnableKeyword("_OCCLUSIONMAP");
            }

            if (repaired.Count == 0 && missing.Count == 0) return;

            if (repaired.Count > 0)
            {

                Plugin.Log.LogWarning(
                    $"[Tenpin] Rebound {repaired.Count} unbound texture slot(s) by name: " +
                    $"{string.Join(", ", repaired.ToArray())}.");
            }

            if (missing.Count > 0)
            {

                Plugin.Log.LogError(
                    $"[Tenpin] {missing.Count} texture slot(s) empty with nothing to rebind: " +
                    $"{string.Join(", ", missing.ToArray())}.");
            }
        }

        private static Texture? FindTexture(string name)
        {
            if (_textures == null)
            {
                _textures = new Dictionary<string, Texture>(StringComparer.Ordinal);
                foreach (Texture t in Resources.FindObjectsOfTypeAll<Texture>())
                {
                    if (t == null || string.IsNullOrEmpty(t.name)) continue;
                    if (!_textures.ContainsKey(t.name)) _textures[t.name] = t;
                }
            }

            return _textures.TryGetValue(name, out Texture found) ? found : null;
        }

        private static IEnumerable<Material> OurBodyMaterials()
        {
            GameObject? round = EncyclopediaRegistration.ResolvedMissile?.unitPrefab;
            var roots = new List<GameObject>();
            if (round != null) roots.Add(round);

            foreach (WeaponMount mount in EncyclopediaRegistration.ResolvedMounts)
                if (mount != null && mount.prefab != null) roots.Add(mount.prefab);

            foreach (GameObject root in roots)
            {
                foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (r == null || r is ParticleSystemRenderer) continue;
                    foreach (Material m in r.sharedMaterials)
                        if (m != null) yield return m;
                }
            }
        }
    }
}
