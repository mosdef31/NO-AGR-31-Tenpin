using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace RocketPod
{

    internal static class PlumeShape
    {

        private const float SegmentMetres = 6f;

        private const float WidthOverSpacing = 1.6f;

        private const float JetLifetime = 0.45f;
        private const float JetSize = 0.55f;

        private const float JetConeDegrees = 6f;

        private const float HazeLifetime = 1.20f;

        private const float HazeSpeed = 2f;

        private const float HazeSpacingMetres = 3.0f;

        private const float HazeSizePerCalibre = 2.0f;

        private const float HazeSpread = 2.2f;

        private const float HazeRatePerSecond = 12f;

        private const int HazeMaxParticles = 140;

        private static readonly FieldInfo? _fSegmentLength =
            AccessTools.Field(typeof(TrailEmitter), "segmentLength");

        private static bool _logged;
        private static bool _hazeLogged;
        private static bool _warnedNoSegment;
        private static bool _warnedNoHaze;

        private static Material? _haze;
        private static bool _hazeSearched;

        internal static void Apply(GameObject clone, Missile ours,
                                   List<ParticleSystem> particles,
                                   IEnumerable<TrailEmitter> trails,
                                   ICollection<ParticleSystem> trailOwned,
                                   float salvoScale)
        {
            if (!Plugin.RealisticPlume.Value) return;

            try
            {
                float spacing = Spacing(salvoScale);
                int tightened = Stream(trails, trailOwned, spacing);

                List<ParticleSystem> nozzle = Jet(particles, trailOwned);

                bool haze = Haze(clone, ours, particles, salvoScale);

                NozzleCut.Attach(clone, ours, nozzle, Plugin.NozzleFireSeconds.Value);

                int jets = nozzle.Count;

                if (!_logged)
                {
                    _logged = true;
                    Plugin.Log.LogInfo(
                        $"[Tenpin] Plume shaped: {tightened} trail(s) at {spacing:0.#} m " +
                        $"spacing, {jets} flame(s) cut to a jet.");
                    Plugin.Log.LogInfo(
                        "[Tenpin] Author a plume on the round in Unity and turn " +
                        "Effects/RealisticPlume off.");
                }

                if (haze && !_hazeLogged)
                {
                    _hazeLogged = true;
                    Plugin.Log.LogInfo(
                        "[Tenpin] Heat haze borrowed from the game's own HeatDistortion.");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError(
                    $"[Tenpin] Shaping the plume failed, so it keeps the donor's: {ex}");
            }
        }

        private static float Spacing(float salvoScale) =>
            Mathf.Clamp(SegmentMetres / Mathf.Max(0.05f, salvoScale), SegmentMetres, 30f);

        private static int Stream(IEnumerable<TrailEmitter> trails,
                                  ICollection<ParticleSystem> trailOwned, float spacing)
        {
            if (_fSegmentLength == null)
            {
                if (!_warnedNoSegment)
                {
                    _warnedNoSegment = true;
                    Plugin.Log.LogWarning(
                        "[Tenpin] TrailEmitter.segmentLength not found, so the trail keeps " +
                        "its 30 m spacing.");
                }
                return 0;
            }

            int count = 0;
            float widest = 0f;

            foreach (TrailEmitter te in trails)
            {
                if (te == null) continue;
                if (!(_fSegmentLength.GetValue(te) is float was) || was <= 0f) continue;

                _fSegmentLength.SetValue(te, spacing);
                widest = Mathf.Max(widest, was);
                count++;
            }

            if (count == 0 || widest <= 0f) return count;

            float shrink = Mathf.Clamp01(spacing * WidthOverSpacing / widest);

            foreach (ParticleSystem ps in trailOwned)
            {
                if (ps == null) continue;

                ParticleSystem.MainModule main = ps.main;
                main.startSizeMultiplier *= shrink;

                main.startLifetimeMultiplier *= 1.85f;

                Color c = main.startColor.color;
                c.a *= 0.28f;
                main.startColor = c;

                main.maxParticles = Mathf.Max(main.maxParticles, 400);
            }

            return count;
        }

        private static List<ParticleSystem> Jet(List<ParticleSystem> particles,
                                               ICollection<ParticleSystem> trailOwned)
        {
            var cut = new List<ParticleSystem>();

            foreach (ParticleSystem ps in particles)
            {
                if (ps == null || trailOwned.Contains(ps)) continue;

                ParticleSystem.MainModule main = ps.main;
                main.startLifetimeMultiplier *= JetLifetime;
                main.startSizeMultiplier *= JetSize;

                ParticleSystem.ShapeModule shape = ps.shape;
                if (shape.enabled && shape.angle > JetConeDegrees)
                    shape.angle = JetConeDegrees;

                cut.Add(ps);
            }

            return cut;
        }

        private static bool Haze(GameObject clone, Missile ours,
                                 List<ParticleSystem> particles, float salvoScale)
        {
            Material? material = HazeMaterial();
            if (material == null)
            {
                if (!_warnedNoHaze)
                {
                    _warnedNoHaze = true;
                    Plugin.Log.LogWarning(
                        "[Tenpin] No HeatDistortion material found, so the round flies " +
                        "with no heat haze.");
                }
                return false;
            }

            var go = new GameObject("Tenpin_HeatHaze");
            go.transform.SetParent(clone.transform, false);

            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;

            ParticleSystem ps = go.AddComponent<ParticleSystem>();

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = HazeRatePerSecond * Mathf.Clamp01(salvoScale);
            emission.rateOverDistance = (1f / HazeSpacingMetres) * Mathf.Clamp01(salvoScale);

            float size = RoundCalibre(ours) * HazeSizePerCalibre;

            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = HazeLifetime;
            main.startSpeed = HazeSpeed;
            main.startSize = Mathf.Max(0.05f, size);
            main.maxParticles = Mathf.Max(4, Mathf.RoundToInt(HazeMaxParticles * Mathf.Clamp01(salvoScale)));
            main.playOnAwake = false;

            main.simulationSpace = ParticleSystemSimulationSpace.World;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;

            shape.angle = 2f;
            shape.radius = Mathf.Max(0.02f, size * 0.35f);

            ParticleSystem.SizeOverLifetimeModule sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(
                1f, new AnimationCurve(
                    new Keyframe(0f, 1f / HazeSpread),
                    new Keyframe(1f, 1f)));

            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.9f, 0.35f),
                    new GradientAlphaKey(0f, 1f),
                });

            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(fade);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.allowRoll = false;
            }

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            particles.Add(ps);
            return true;
        }

        private static Material? HazeMaterial()
        {
            if (_hazeSearched) return _haze;
            _hazeSearched = true;

            Material? best = null;

            foreach (Material m in Resources.FindObjectsOfTypeAll<Material>())
            {
                if (m == null || m.shader == null) continue;

                if (m.name.StartsWith("Tenpin", StringComparison.Ordinal)) continue;

                if (!m.HasProperty("_DistortionEnabled")) continue;
                if (m.GetFloat("_DistortionEnabled") <= 0f) continue;

                best = m;
                if (m.name.IndexOf("HeatDistortion", StringComparison.OrdinalIgnoreCase) >= 0)
                    break;
            }

            _haze = best;
            return _haze;
        }

        private static float RoundCalibre(Missile ours)
        {
            if (ours == null) return 0.13f;

            bool any = false;
            Bounds bounds = default;

            foreach (Renderer r in ours.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || r is ParticleSystemRenderer) continue;
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }

            if (!any) return 0.13f;

            Vector3 s = bounds.size;
            float a = Mathf.Min(s.x, Mathf.Min(s.y, s.z));
            float b = s.x + s.y + s.z - a - Mathf.Max(s.x, Mathf.Max(s.y, s.z));

            return Mathf.Clamp(Mathf.Min(a, b), 0.05f, 0.5f);
        }
    }
}
