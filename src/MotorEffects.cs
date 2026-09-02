using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace RocketPod
{

    internal static class MotorEffects
    {
        private const string ContainerName = "Tenpin_BorrowedEffects";

        private static readonly FieldInfo? _fMotors =
            AccessTools.Field(typeof(Missile), "motors");
        private static readonly FieldInfo? _fEffectsTransform =
            AccessTools.Field(typeof(Missile), "effectsTransform");

        private static bool _donorsLogged;
        private static bool _appliedLogged;

        private static bool _pickLogged;
        private static string _lastPreference = string.Empty;

        internal static void Apply(Missile ours)
        {
            if (!Plugin.UseStockEffects.Value) return;
            if (_fMotors?.GetValue(ours) is not Array motors || motors.Length == 0) return;

            if (ours.transform.Find(ContainerName) != null) return;

            if (MotorHasEffects(motors.GetValue(0))) return;

            List<Donor> donors = FindDonors();
            if (donors.Count == 0)
            {
                Plugin.Log.LogWarning(
                    "[Tenpin] No stock missile with an effectsTransform, so nothing " +
                    "could be borrowed.");
                return;
            }

            int silenced = 0;
            foreach (ParticleSystem ps in ours.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (ps.gameObject == ours.gameObject) continue;
                ps.gameObject.SetActive(false);
                silenced++;
            }
            foreach (TrailEmitter te in ours.GetComponentsInChildren<TrailEmitter>(true))
            {
                te.enabled = false;
                te.gameObject.SetActive(false);
            }

            var container = new GameObject(ContainerName);
            container.transform.SetParent(ours.transform, false);
            if (_fEffectsTransform?.GetValue(ours) as Transform == null)
                _fEffectsTransform?.SetValue(ours, container.transform);

            Donor pick = ChooseDonor(donors);
            CloneOnto(ours, motors.GetValue(0), pick, container.transform, silenced);
        }

        private static bool MotorHasEffects(object? motor)
        {
            if (motor == null) return false;
            foreach (string field in new[] { "particleSystems", "trailEmitters", "lights" })
            {
                if (AccessTools.Field(motor.GetType(), field)?.GetValue(motor) is Array a &&
                    a.Length > 0)
                    return true;
            }
            return false;
        }

        private readonly struct Donor
        {
            internal Donor(Missile m, Transform fx, float burn, int flames, int lights)
            {
                Missile = m; Fx = fx; Burn = burn; Flames = flames; Lights = lights;
            }
            internal Missile Missile { get; }
            internal Transform Fx { get; }
            internal float Burn { get; }

            internal int Flames { get; }
            internal int Lights { get; }

            internal bool Usable => Flames > 0;

            internal string Key => Missile.definition != null ? Missile.definition.jsonKey : "?";
        }

        private static Donor ChooseDonor(List<Donor> donors)
        {

            string pref = Plugin.MotorEffectDonor.Value ?? string.Empty;
            bool say = !_pickLogged || pref != _lastPreference;
            _pickLogged = true;
            _lastPreference = pref;

            List<Donor> pool = donors.Where(d => d.Usable).ToList();
            if (pool.Count == 0)
            {

                if (say) Plugin.Log.LogWarning(
                    "[Tenpin] No donor has a flame system of its own, so the borrowed " +
                    "exhaust is smoke only.");
                pool = donors;
            }

            string preference = pref;
            if (DonorPreference.TryBest(pool, d => d.Key, preference,
                                        out Donor preferred, out string why))
            {
                if (say) Plugin.Log.LogInfo(
                    $"[Tenpin] Motor effect donor '{preferred.Key}' - {why}; " +
                    $"{preferred.Flames} flame system(s), {preferred.Lights} light(s).");
                return preferred;
            }

            if (say && !string.IsNullOrWhiteSpace(preference))

                Plugin.Log.LogInfo(
                    $"[Tenpin] No donor with fire matched '{preference}', so one was " +
                    "chosen automatically.");

            Donor pick = pool
                .OrderBy(d => Math.Abs(d.Burn - OurBurn(donors)))
                .ThenByDescending(d => d.Lights)
                .ThenByDescending(d => d.Flames)
                .First();

            if (say) Plugin.Log.LogInfo(
                $"[Tenpin] Motor donor '{pick.Key}': {pick.Flames} flame(s), " +
                $"{pick.Lights} light(s), burn {pick.Burn:0.#}s.");
            return pick;
        }

        private static void Score(Transform fx, out int flames, out int lights)
        {
            var trailOwned = new HashSet<ParticleSystem>();
            FieldInfo? fTrailSystem = AccessTools.Field(typeof(TrailEmitter), "trailSystem");
            foreach (TrailEmitter te in fx.GetComponentsInChildren<TrailEmitter>(true))
                if (fTrailSystem?.GetValue(te) is ParticleSystem ts) trailOwned.Add(ts);

            flames = fx.GetComponentsInChildren<ParticleSystem>(true)
                       .Count(p => p != null && !trailOwned.Contains(p) && PlumeTint.IsFlame(p));
            lights = fx.GetComponentsInChildren<Light>(true).Length;
        }

        private static float OurBurn(List<Donor> donors) =>
            EncyclopediaRegistration.ResolvedMissile?.unitPrefab != null &&
            EncyclopediaRegistration.ResolvedMissile.unitPrefab.GetComponent<Missile>() is { } m
                ? Mathf.Max(0.1f, m.GetTotalBurnTime())
                : donors.Min(d => d.Burn);

        private static int Lights(Transform fx) => fx.GetComponentsInChildren<Light>(true).Length;
        private static int Flash(Transform fx) => fx.GetComponentsInChildren<ParticleSystem>(true).Length;

        private static Vector3 ExhaustPoint(Missile ours, Donor donor)
        {

            var meshes = new List<(Transform At, Mesh Mesh)>();
            foreach (Renderer r in ours.GetComponentsInChildren<Renderer>(true))
            {
                Mesh? mesh = r switch
                {
                    SkinnedMeshRenderer skinned => skinned.sharedMesh,
                    MeshRenderer => r.GetComponent<MeshFilter>()?.sharedMesh,
                    _ => null,
                };
                if (mesh != null) meshes.Add((r.transform, mesh));
            }

            if (meshes.Count == 0) return donor.Fx.localPosition;

            float tailZ = float.MaxValue;
            float noseZ = float.MinValue;

            foreach ((Transform at, Mesh mesh) in meshes)
            {
                Vector3 c = mesh.bounds.center, e = mesh.bounds.extents;
                for (int corner = 0; corner < 8; corner++)
                {
                    var local = new Vector3(
                        c.x + ((corner & 1) == 0 ? -e.x : e.x),
                        c.y + ((corner & 2) == 0 ? -e.y : e.y),
                        c.z + ((corner & 4) == 0 ? -e.z : e.z));

                    float z = ours.transform.InverseTransformPoint(at.TransformPoint(local)).z;
                    if (z < tailZ) tailZ = z;
                    if (z > noseZ) noseZ = z;
                }
            }

            if (!_exhaustLogged)
            {
                _exhaustLogged = true;

                string verdict = Mathf.Abs(tailZ) > 5f
                    ? "  IMPLAUSIBLE - a round is ~2 m long, so this is not its tail."
                    : string.Empty;

                Plugin.Log.LogInfo(
                    $"[Tenpin] Exhaust point from {meshes.Count} mesh(es): tail " +
                    $"{tailZ:0.###} m, length {noseZ - tailZ:0.##} m.{verdict}");
            }

            return new Vector3(0f, 0f, tailZ);
        }

        private static bool _spaceLogged;
        private static bool _exhaustLogged;

        private static void NormalizeSimulationSpace(GameObject clone, string donorKey)
        {
            ParticleSystem[] systems = clone.GetComponentsInChildren<ParticleSystem>(true);

            int fixedCount = 0;
            var seen = new List<string>();

            foreach (ParticleSystem ps in systems)
            {
                if (ps == null) continue;

                ParticleSystem.MainModule main = ps.main;
                ParticleSystemSimulationSpace was = main.simulationSpace;

                if (!_spaceLogged)
                    seen.Add($"{ps.gameObject.name}={was}" +
                             (was == ParticleSystemSimulationSpace.Custom
                                 ? $"(anchor '{(main.customSimulationSpace == null ? "(null)" : main.customSimulationSpace.name)}')"
                                 : string.Empty));

                if (was != ParticleSystemSimulationSpace.Custom) continue;

                main.simulationSpace = PlumeTint.IsFlame(ps)
                    ? ParticleSystemSimulationSpace.Local
                    : ParticleSystemSimulationSpace.World;

                main.customSimulationSpace = null;
                fixedCount++;
            }

            if (_spaceLogged) return;
            _spaceLogged = true;

            Plugin.Log.LogInfo(
                $"[Tenpin] Motor plume borrowed from '{donorKey}': {systems.Length} system(s) - " +
                string.Join(", ", seen));

            if (fixedCount > 0)

                Plugin.Log.LogInfo(
                    $"[Tenpin] {fixedCount} of them were in CUSTOM space and were re-based.");
        }

        private static void CloneOnto(Missile ours, object? motor, Donor donor,
                                      Transform parent, int silenced)
        {
            if (motor == null) return;

            Vector3 exhaust = ExhaustPoint(ours, donor);

            GameObject clone = UnityEngine.Object.Instantiate(donor.Fx.gameObject, parent);
            clone.name = "Motor0_Exhaust";

            clone.transform.localPosition = exhaust;
            clone.transform.localRotation = donor.Fx.localRotation;
            clone.SetActive(true);

            NormalizeSimulationSpace(clone, donor.Key);
            ShapeNozzleGlow(clone, exhaust);

            var particles = clone.GetComponentsInChildren<ParticleSystem>(true).ToList();
            var trails = clone.GetComponentsInChildren<TrailEmitter>(true).ToList();
            var lights = clone.GetComponentsInChildren<Light>(true).ToList();
            var audio = clone.GetComponentsInChildren<AudioSource>(true).ToList();

            var trailOwned = new HashSet<ParticleSystem>();
            FieldInfo? fTrailSystem = AccessTools.Field(typeof(TrailEmitter), "trailSystem");
            FieldInfo? fEmitTransform = AccessTools.Field(typeof(TrailEmitter), "emitTransform");
            foreach (TrailEmitter te in trails)
            {
                if (fTrailSystem?.GetValue(te) is ParticleSystem ts) trailOwned.Add(ts);

                te.rb = ours.rb;
                if (fEmitTransform?.GetValue(te) as Transform == null)
                    fEmitTransform?.SetValue(te, te.transform);
                te.enabled = false;
            }
            particles.RemoveAll(p => trailOwned.Contains(p));

            float scale = SalvoBudget.EmissionScale();
            SalvoBudget.ApplyEmissionScale(particles, scale);
            SalvoBudget.ApplyEmissionScale(trailOwned, scale);

            SalvoBudget.ApplySmokeTrim(trailOwned);

            PlumeTint.Describe(donor.Key, particles);
            PlumeTint.Apply(particles);

            PlumeShape.Apply(clone, ours, particles, trails, trailOwned, scale);

            foreach (AudioSource a in audio)
            {
                if (a == null) continue;
                a.playOnAwake = false;
                a.Stop();
                a.enabled = false;
            }

            SetMotorArray(motor, "particleSystems", particles.ToArray());
            SetMotorArray(motor, "trailEmitters", trails.ToArray());

            if (!Plugin.NozzleGlow.Value)
            {
                foreach (Light l in lights)
                {
                    if (l == null) continue;
                    l.enabled = false;
                }
                lights.Clear();
            }

            SetMotorArray(motor, "lights", lights.ToArray());

            if (!_appliedLogged)
            {
                _appliedLogged = true;

                Plugin.Log.LogInfo(
                    $"[Tenpin] Motor effects from '{donor.Key}': {particles.Count} particles, " +
                    $"{trails.Count} trails, {lights.Count} lights, {audio.Count} audio.");

                if (silenced > 0)
                    Plugin.Log.LogInfo(
                        $"[Tenpin] Silenced {silenced} authored system(s) on the round.");
            }

            if (scale < 0.999f)
                Plugin.Log.LogDebug(
                    $"[Tenpin] Salvo budget: {SalvoBudget.Live} round(s) up, emission scaled to " +
                    $"{scale:0.00}.");
        }

        private static bool _glowLogged;

        private static void ShapeNozzleGlow(GameObject clone, Vector3 exhaust)
        {
            bool keepGlow = Plugin.NozzleGlow.Value;

            float inset = Plugin.GlowNozzleInset.Value;
            float sizeScale = Plugin.GlowSizeScale.Value;
            if (keepGlow && inset <= 0f && sizeScale >= 0.999f) return;

            int shaped = 0;
            int removed = 0;

            foreach (ParticleSystem ps in clone.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (ps == null) continue;

                bool isRoot = ReferenceEquals(ps.gameObject, clone);

                ParticleSystem.MainModule main = ps.main;

                if (Mathf.Abs(main.startSpeed.constantMax) >= 0.5f) continue;
                if (main.startLifetime.constantMax > 0.35f) continue;

                if (!keepGlow)
                {
                    if (isRoot)
                    {

                        ParticleSystem.EmissionModule emission = ps.emission;
                        emission.enabled = false;
                    }
                    else
                    {
                        UnityEngine.Object.Destroy(ps.gameObject);
                    }

                    removed++;
                    continue;
                }

                if (inset > 0f && !isRoot)
                    ps.transform.localPosition += Vector3.forward * inset;

                if (sizeScale < 0.999f)
                {
                    ParticleSystem.MinMaxCurve size = main.startSize;
                    size.constantMin *= sizeScale;
                    size.constantMax *= sizeScale;
                    size.curveMultiplier *= sizeScale;
                    main.startSize = size;
                }

                shaped++;
            }

            if (removed > 0 && !_glowLogged)
            {
                _glowLogged = true;
                Plugin.Log.LogInfo(
                    $"[Tenpin] Nozzle glow: {removed} flash system(s) removed, and the motor's " +
                    "borrowed Light with them. The jet, the smoke trail and the heat haze are " +
                    "untouched.");
                return;
            }

            if (shaped > 0 && !_glowLogged)
            {
                _glowLogged = true;

                Plugin.Log.LogInfo(
                    $"[Tenpin] Nozzle glow: {shaped} system(s) moved {inset:0.##} m " +
                    $"forward, scaled to {sizeScale:0.##}.");
            }
        }

        private static void SetMotorArray(object motor, string field, Array value)
        {
            FieldInfo? f = AccessTools.Field(motor.GetType(), field);
            if (f != null && value.Length > 0) f.SetValue(motor, value);
        }

        private static List<Donor> FindDonors()
        {
            var found = new List<Donor>();
            Encyclopedia? enc = null;
            enc = GameData.EncyclopediaOrNull();
            if (enc?.missiles == null) return found;

            foreach (MissileDefinition md in enc.missiles)
            {
                if (md == null || md.unitPrefab == null) continue;
                if (PluginInfo.IsOurRound(md.jsonKey)) continue;

                Missile? m = md.unitPrefab.GetComponent<Missile>();
                if (m == null) continue;
                if (_fEffectsTransform?.GetValue(m) is not Transform t || t == null) continue;

                float burn = m.GetTotalBurnTime();
                if (burn <= 0f) continue;

                Score(t, out int flames, out int lights);
                found.Add(new Donor(m, t, burn, flames, lights));
            }

            if (!_donorsLogged && found.Count > 0)
            {
                _donorsLogged = true;
                Plugin.Log.LogInfo($"[Tenpin] -- Effect donor candidates ({found.Count}) --");
                foreach (Donor d in found.OrderBy(d => d.Burn))
                    Plugin.Log.LogInfo(
                        $"[Tenpin]   '{d.Key}' burn={d.Burn:0.#}s particles={Flash(d.Fx)} " +
                        $"FLAMES={d.Flames} lights={Lights(d.Fx)} " +
                        $"trails={d.Fx.GetComponentsInChildren<TrailEmitter>(true).Length}");

                Plugin.Log.LogInfo(
                    "[Tenpin]   flames= is the column that matters; particles= draws no fire.");
            }

            return found;
        }
    }

    [HarmonyPatch(typeof(Missile), "OnStartClient")]
    internal static class Missile_OnStartClient_EffectsPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Missile __instance)
        {
            try
            {
                if (__instance.definition == null ||
                    !PluginInfo.IsOurRound(__instance.definition.jsonKey)) return;

                SillyEffects.ApplyMotor(__instance);
                MotorEffects.Apply(__instance);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError(
                    $"[Tenpin] Borrowing motor effects failed (the rocket still flies): {ex}");
            }
        }
    }
}
