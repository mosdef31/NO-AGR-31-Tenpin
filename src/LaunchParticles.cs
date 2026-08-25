using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace RocketPod
{

    internal static class LaunchParticles
    {
        private const string ContainerName = "Tenpin_LaunchFlash";

        private const float MaxDuration = 0.25f;

        private static readonly FieldInfo? _fEffectsTransform =
            AccessTools.Field(typeof(Missile), "effectsTransform");

        private static bool _logged;

        internal static void Apply(TenpinLauncher launcher)
        {
            if (!Plugin.UseStockEffects.Value) return;

            if (launcher.launchParticles != null) return;
            if (launcher.transform.Find(ContainerName) != null) return;

            ParticleSystem? donor = FindDonor(out string from);
            if (donor == null)
            {
                if (!_logged)
                {
                    _logged = true;
                    Plugin.Log.LogWarning(
                        "[Tenpin] No stock particle system could be borrowed for the launch " +
                        "flash, so the pod fires with nothing at the tube. Cosmetic only.");
                }
                return;
            }

            var container = new GameObject(ContainerName);
            container.transform.SetParent(launcher.transform, false);

            GameObject clone = UnityEngine.Object.Instantiate(donor.gameObject, container.transform);
            clone.name = "Flash";
            clone.transform.localPosition = Vector3.zero;
            clone.transform.localRotation = Quaternion.identity;
            clone.SetActive(true);

            var ps = clone.GetComponent<ParticleSystem>();
            if (ps == null)
            {
                UnityEngine.Object.Destroy(container);
                return;
            }

            Clamp(ps);

            foreach (TrailEmitter te in clone.GetComponentsInChildren<TrailEmitter>(true))
            {
                te.enabled = false;
                te.gameObject.SetActive(false);
            }
            foreach (ParticleSystem child in clone.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (child == ps) continue;
                Clamp(child);
            }

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            launcher.launchParticles = ps;

            if (!_logged)
            {
                _logged = true;
                Plugin.Log.LogInfo(
                    $"[Tenpin] Launch flash borrowed from {from}, clamped to {MaxDuration:0.##}s so " +
                    "an 0.08 s ripple reads as separate shots. MissileLauncher.launchParticles " +
                    "ships null and no stock weapon uses MissileLauncher, so there was nothing " +
                    "purpose-built to copy. Author one in Unity and this stops running.");
            }
        }

        private static void Clamp(ParticleSystem ps)
        {
            ParticleSystem.MainModule main = ps.main;
            main.loop = false;
            main.playOnAwake = false;

            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.customSimulationSpace = null;
            main.duration = Mathf.Min(main.duration, MaxDuration);
            main.startLifetime = new ParticleSystem.MinMaxCurve(
                Mathf.Min(main.startLifetime.constantMax, MaxDuration));
        }

        private static ParticleSystem? FindDonor(out string from)
        {
            from = "(nothing)";

            Encyclopedia? enc = GameData.EncyclopediaOrNull();
            if (enc?.missiles == null) return null;

            var candidates = new List<(string Key, ParticleSystem Ps)>();

            foreach (MissileDefinition md in enc.missiles)
            {
                if (md == null || md.unitPrefab == null) continue;
                if (md.jsonKey == PluginInfo.MissileKey) continue;

                Missile? m = md.unitPrefab.GetComponent<Missile>();
                if (m == null) continue;
                if (_fEffectsTransform?.GetValue(m) is not Transform fx || fx == null) continue;

                ParticleSystem? flash = fx.GetComponentsInChildren<ParticleSystem>(true)
                                          .Where(p => p != null)
                                          .OrderByDescending(PlumeTint.IsFlame)
                                          .ThenBy(p => p.main.startLifetime.constantMax)
                                          .FirstOrDefault();
                if (flash == null) continue;

                candidates.Add(($"{md.jsonKey} {md.unitName}", flash));
            }

            if (candidates.Count == 0) return null;

            var withFire = candidates.Where(c => PlumeTint.IsFlame(c.Ps)).ToList();
            if (withFire.Count > 0) candidates = withFire;

            if (DonorPreference.TryBest(candidates, c => c.Key,
                                        Plugin.LaunchEffectDonor.Value,
                                        out (string Key, ParticleSystem Ps) pick, out string why))
            {
                from = $"'{pick.Key.Trim()}' ({why})";
                return pick.Ps;
            }

            from = $"'{candidates[0].Key.Trim()}' (no preference matched)";
            return candidates[0].Ps;
        }
    }
}
