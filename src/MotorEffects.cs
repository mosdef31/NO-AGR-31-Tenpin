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
                    "[Tenpin] No stock missile with an effectsTransform was found, so no " +
                    "exhaust or trail could be borrowed. The rocket will fly invisibly quiet.");
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

            List<Donor> pool = donors.Where(d => d.Usable).ToList();
            if (pool.Count == 0)
            {
                Plugin.Log.LogWarning(
                    "[Tenpin] No donor in this build has a flame system that is not owned by its " +
                    "trail emitter, so the borrowed exhaust will be smoke only. The candidate " +
                    "list above prints flames= for each; if they are all 0 the classifier's " +
                    $"{PlumeTint.FlameLifetimeSeconds:0.##}s lifetime threshold is wrong for 0.34.");
                pool = donors;
            }

            string preference = Plugin.MotorEffectDonor.Value;
            if (DonorPreference.TryBest(pool, d => d.Key, preference,
                                        out Donor preferred, out string why))
            {
                Plugin.Log.LogInfo(
                    $"[Tenpin] Motor effect donor '{preferred.Key}' - {why}; " +
                    $"{preferred.Flames} flame system(s), {preferred.Lights} light(s).");
                return preferred;
            }

            if (!string.IsNullOrWhiteSpace(preference))
                Plugin.Log.LogInfo(
                    $"[Tenpin] No donor WITH FIRE matched any of '{preference}', so one was chosen " +
                    "automatically. This is expected rather than an error - the stock rockets " +
                    "carry smoke and a trail and leave the fire to the launcher. The candidate " +
                    "list above prints flames= per donor.");

            Donor pick = pool
                .OrderBy(d => Math.Abs(d.Burn - OurBurn(donors)))
                .ThenByDescending(d => d.Lights)
                .ThenByDescending(d => d.Flames)
                .First();

            Plugin.Log.LogInfo(
                $"[Tenpin] Motor effect donor '{pick.Key}' - closest burn to ours with fire " +
                $"({pick.Flames} flame system(s), {pick.Lights} light(s), burn {pick.Burn:0.#}s).");
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

        private static void CloneOnto(Missile ours, object? motor, Donor donor,
                                      Transform parent, int silenced)
        {
            if (motor == null) return;

            GameObject clone = UnityEngine.Object.Instantiate(donor.Fx.gameObject, parent);
            clone.name = "Motor0_Exhaust";
            float ourLen = ours.definition != null ? ours.definition.length : 0f;
            float donorLen = donor.Missile.definition != null ? donor.Missile.definition.length : ourLen;
            clone.transform.localPosition = donor.Fx.localPosition
                                            + new Vector3(0f, 0f, -(ourLen - donorLen) * 0.5f);
            clone.transform.localRotation = donor.Fx.localRotation;
            clone.SetActive(true);

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

            PlumeTint.Describe(donor.Key, particles);
            PlumeTint.Apply(particles);

            foreach (AudioSource a in audio)
            {
                if (a == null) continue;
                a.playOnAwake = false;
                a.Stop();
                a.enabled = false;
            }

            SetMotorArray(motor, "particleSystems", particles.ToArray());
            SetMotorArray(motor, "trailEmitters", trails.ToArray());
            SetMotorArray(motor, "lights", lights.ToArray());

            if (!_appliedLogged)
            {
                _appliedLogged = true;
                Plugin.Log.LogInfo(
                    $"[Tenpin] Motor effects borrowed from '{donor.Key}' (burn {donor.Burn:0.#}s): " +
                    $"{particles.Count} particle system(s), {trails.Count} trail emitter(s), " +
                    $"{lights.Count} light(s), {audio.Count} audio source(s)" +
                    (silenced > 0 ? $"; silenced {silenced} authored system(s)" : "") +
                    ". Author real ones in Unity and this stops running - it only fills empty slots.");
            }

            if (scale < 0.999f)
                Plugin.Log.LogDebug(
                    $"[Tenpin] Salvo budget: {SalvoBudget.Live} round(s) up, emission scaled to " +
                    $"{scale:0.00}.");
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
                if (md.jsonKey == PluginInfo.MissileKey) continue;

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
                    "[Tenpin]   flames= is the column that matters and particles= is not: it " +
                    "counts short-lived systems that are NOT owned by a trail emitter, which is " +
                    "the only thing that draws fire. A donor at flames=0 gives smoke and nothing " +
                    "else, which is exactly what Rocket_MLRS1 did. Pin one with " +
                    "Plugin.MotorEffectDonor.");
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
                    __instance.definition.jsonKey != PluginInfo.MissileKey) return;

                FunEffects.StripMotor(__instance);
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
