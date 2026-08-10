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
            internal Donor(Missile m, Transform fx, float burn) { Missile = m; Fx = fx; Burn = burn; }
            internal Missile Missile { get; }
            internal Transform Fx { get; }
            internal float Burn { get; }
            internal string Key => Missile.definition != null ? Missile.definition.jsonKey : "?";
        }

        private static Donor ChooseDonor(List<Donor> donors)
        {
            string wanted = Plugin.MotorEffectDonor.Value;
            if (!string.IsNullOrWhiteSpace(wanted))
            {
                foreach (Donor d in donors)
                    if (d.Key == wanted) return d;
                Plugin.Log.LogWarning(
                    $"[Tenpin] Effect donor '{wanted}' not found or has no effects; falling back " +
                    "to automatic selection. The candidate list is logged above.");
            }

            var rockets = donors
                .Where(d => d.Key.IndexOf("AGR", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            d.Key.IndexOf("Rocket", StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            List<Donor> pool = rockets.Count > 0 ? rockets : donors;
            return pool
                .OrderBy(d => Math.Abs(d.Burn - OurBurn(donors)))
                .ThenByDescending(d => Flash(d.Fx))
                .First();
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

            SetMotorArray(motor, "particleSystems", particles.ToArray());
            SetMotorArray(motor, "trailEmitters", trails.ToArray());
            SetMotorArray(motor, "lights", lights.ToArray());

            if (!_appliedLogged)
            {
                _appliedLogged = true;
                Plugin.Log.LogInfo(
                    $"[Tenpin] Motor effects borrowed from '{donor.Key}' (burn {donor.Burn:0.#}s): " +
                    $"{particles.Count} particle system(s), {trails.Count} trail emitter(s), " +
                    $"{lights.Count} light(s)" +
                    (silenced > 0 ? $"; silenced {silenced} authored system(s)" : "") +
                    ". Author real ones in Unity and this stops running - it only fills empty slots.");
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
            try { enc = Encyclopedia.i; } catch {  }
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
                found.Add(new Donor(m, t, burn));
            }

            if (!_donorsLogged && found.Count > 0)
            {
                _donorsLogged = true;
                Plugin.Log.LogInfo($"[Tenpin] -- Effect donor candidates ({found.Count}) --");
                foreach (Donor d in found.OrderBy(d => d.Burn))
                    Plugin.Log.LogInfo(
                        $"[Tenpin]   '{d.Key}' burn={d.Burn:0.#}s particles={Flash(d.Fx)} " +
                        $"lights={Lights(d.Fx)} " +
                        $"trails={d.Fx.GetComponentsInChildren<TrailEmitter>(true).Length}");
                Plugin.Log.LogInfo("[Tenpin]   Pin one with Effects/MotorEffectDonor.");
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
