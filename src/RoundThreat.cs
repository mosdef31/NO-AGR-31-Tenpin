using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace RocketPod
{

    internal static class RoundThreat
    {

        private static readonly Dictionary<Missile, PersistentID> _claimed = new();
        private static bool _logged;

        internal static void Register(Missile missile)
        {
            if (!Plugin.RoundsCountAsAttacks.Value) return;
            if (missile == null || !missile.IsServer) return;

            if (missile.targetID.IsValid) return;

            if (Plugin.RoundRadarSize.Value > 0f && missile.definition != null)
            {
                missile.RCS = Mathf.Max(missile.RCS, Plugin.RoundRadarSize.Value);
            }

            FactionHQ hq = missile.NetworkHQ;
            if (hq == null) return;

            Vector3 heading = missile.rb != null ? missile.rb.velocity : missile.transform.forward;
            Vector3 from = missile.GlobalPosition().AsVector3();

            Unit? nearest = null;
            float bestSq = Plugin.RoundThreatRadius.Value * Plugin.RoundThreatRadius.Value;

            foreach (KeyValuePair<PersistentID, TrackingInfo> kv in hq.trackingDatabase)
            {
                if (!kv.Value.TryGetUnit(out Unit unit) || unit == null || unit.disabled) continue;
                if (unit is Missile) continue;

                Vector3 to = unit.GlobalPosition().AsVector3() - from;

                if (Vector3.Dot(to, heading) <= 0f) continue;

                float sq = Vector3.Cross(to, heading.normalized).sqrMagnitude;
                if (sq >= bestSq) continue;

                nearest = unit;
                bestSq = sq;
            }

            if (nearest == null) return;

            if (hq.trackingDatabase.TryGetValue(nearest.persistentID, out TrackingInfo info))
            {
                info.missileAttacks += 1;
                _claimed[missile] = nearest.persistentID;

                if (!_logged)
                {
                    _logged = true;
                    Plugin.Log.LogInfo(
                        $"[Tenpin] Rounds now register as an attack on what they are aimed at - " +
                        $"this one on '{nearest.unitName}'. An unguided round with no target unit " +
                        "increments nothing, so every AI in the game treated a salvo of eighteen " +
                        "rockets as though nothing had been fired. Guidance is untouched. " +
                        "Logged once.");
                }
            }
        }

        internal static void Release(Missile missile)
        {
            if (missile == null) return;
            if (!_claimed.TryGetValue(missile, out PersistentID id)) return;
            _claimed.Remove(missile);

            FactionHQ hq = missile.NetworkHQ;
            if (hq == null) return;

            if (hq.trackingDatabase.TryGetValue(id, out TrackingInfo info))
            {

                if (info.missileAttacks > 0) info.missileAttacks -= 1;
            }
        }
    }

    [HarmonyPatch(typeof(Missile), "OnStartClient")]
    internal static class Missile_OnStartClient_ThreatPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Missile __instance)
        {
            try
            {
                if (__instance.definition == null ||
                    __instance.definition.jsonKey != PluginInfo.MissileKey) return;

                RoundThreat.Register(__instance);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[Tenpin] Round threat registration failed: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(Missile), nameof(Missile.Detonate))]
    internal static class Missile_Detonate_ThreatPatch
    {
        [HarmonyPrefix]
        private static void Prefix(Missile __instance)
        {
            try
            {
                if (__instance.definition == null ||
                    __instance.definition.jsonKey != PluginInfo.MissileKey) return;

                RoundThreat.Release(__instance);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[Tenpin] Round threat release failed: {ex}");
            }
        }
    }
}
