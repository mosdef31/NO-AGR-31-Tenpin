using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace RocketPod
{

    internal sealed class WaterBackstop : MonoBehaviour
    {

        private const float GraceSeconds = 0.20f;

        private const float DestroySeconds = 0.60f;

        private static readonly FieldInfo? FImpactFuse =
            AccessTools.Field(typeof(Missile), "impactFuse");

        private const int EntryLogCap = 5;
        private static int _entriesLogged;
        private static int _destroyed;

        private Missile? _missile;
        private Rigidbody? _rb;

        private float _underSince = -1f;
        private bool _detonateAsked;
        private bool _logged;

        internal static void Apply(Missile missile)
        {
            if (missile == null) return;
            if (missile.GetComponent<WaterBackstop>() != null) return;
            WaterBackstop b = missile.gameObject.AddComponent<WaterBackstop>();
            b._missile = missile;
            b._rb = missile.GetComponent<Rigidbody>();
        }

        private void FixedUpdate()
        {

            if (_missile == null) { Destroy(this); return; }

            if (!_missile.LocalSim) return;

            float y = transform.position.y;
            float sea = Datum.LocalSeaY;

            if (y >= sea)
            {

                _underSince = -1f;
                return;
            }

            if (_underSince < 0f)
            {
                _underSince = Time.time;
                LogEntry(y, sea);
                return;
            }

            float under = Time.time - _underSince;

            if (!_detonateAsked)
            {
                if (under < GraceSeconds) return;

                _detonateAsked = true;
                try
                {

                    _missile.Detonate(Vector3.up, hitArmor: false, hitTerrain: false);
                }
                catch (Exception ex)
                {

                    Plugin.Log.LogError(
                        "[Tenpin] WATER: Missile.Detonate THREW underwater - this is the fault, " +
                        $"and it is in the engine's detonation path, not in the fuse: {ex}");
                }
                return;
            }

            if (under < GraceSeconds + DestroySeconds) return;

            _destroyed++;
            if (_destroyed <= 3 || _destroyed % 25 == 0)
            {
                Plugin.Log.LogWarning(
                    $"[Tenpin] WATER: round #{_destroyed} survived the sea AND a requested " +
                    "detonation, so the backstop destroyed it. This is the leak the 2026-08-31 " +
                    "report described; the WATER line above says what state the round was in. " +
                    "Every one of these would otherwise have sat in the water for the rest of " +
                    "the mission burning a Burnout call and a splash roll every fixed step.");
            }
            Destroy(gameObject);
        }

        private void LogEntry(float y, float sea)
        {

            if (_logged || _entriesLogged >= EntryLogCap) return;
            _logged = true;
            _entriesLogged++;

            bool impact = true;
            try { if (FImpactFuse != null) impact = (bool)FImpactFuse.GetValue(_missile); }
            catch {  }

            bool armed;
            try { armed = _missile!.IsArmed(); }
            catch { armed = false; }

            float speed = _rb != null ? _rb.velocity.magnitude : 0f;

            Plugin.Log.LogInfo(
                $"[Tenpin] WATER: round entered the sea {(sea - y):F1} m down at {speed:F0} m/s. " +
                $"impactFuse {impact}, armed {armed}, tangible {_missile!.IsTangible()}, " +
                $"disabled {_missile.disabled}. The engine detonates ONLY on impactFuse AND armed; " +
                "anything else here takes the branch that leaves the round in the water for ever.");
        }
    }

    [HarmonyPatch(typeof(Missile), "OnStartClient")]
    internal static class Missile_OnStartClient_WaterPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Missile __instance)
        {
            try
            {
                if (!Plugin.WaterBackstop.Value) return;
                if (__instance.definition == null) return;
                if (!PluginInfo.IsOurRound(__instance.definition.jsonKey)) return;

                WaterBackstop.Apply(__instance);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[Tenpin] Water backstop attach failed: {ex}");
            }
        }
    }
}
