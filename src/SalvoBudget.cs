using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace RocketPod
{

    internal static class SalvoBudget
    {

        private const int FullDetailRounds = 8;

        private const int TaperEndsAt = 114;

        private const float MinEmissionScale = 0.35f;

        private const int FlightVoices = 6;

        private static int _voicesInUse;

        private static readonly HashSet<Missile> _live = new HashSet<Missile>();

        internal static int Live
        {
            get
            {

                _live.RemoveWhere(m => m == null);
                return _live.Count;
            }
        }

        internal static void Register(Missile round) => _live.Add(round);

        internal static float EmissionScale()
        {
            int live = Live;
            if (live <= FullDetailRounds) return 1f;

            float t = Mathf.InverseLerp(FullDetailRounds, TaperEndsAt, live);
            return Mathf.Lerp(1f, MinEmissionScale, t);
        }

        internal static void ApplyEmissionScale(IEnumerable<ParticleSystem> systems, float scale)
        {
            if (scale >= 0.999f) return;

            foreach (ParticleSystem ps in systems)
            {
                if (ps == null) continue;
                ParticleSystem.EmissionModule emission = ps.emission;
                emission.rateOverTimeMultiplier *= scale;
                emission.rateOverDistanceMultiplier *= scale;
            }
        }

        internal static bool TryTakeVoice(Missile round)
        {
            if (round == null) return false;
            if (_voicesInUse >= FlightVoices) return false;

            _voicesInUse++;
            round.gameObject.AddComponent<VoiceSlot>();
            return true;
        }

        private static void ReleaseVoice()
        {
            if (_voicesInUse > 0) _voicesInUse--;
        }

        private sealed class VoiceSlot : MonoBehaviour
        {
            private void OnDestroy() => ReleaseVoice();
        }
    }

    [HarmonyPatch(typeof(Missile), "OnStartClient")]
    internal static class Missile_OnStartClient_BudgetPatch
    {
        [HarmonyPrefix]
        private static void Prefix(Missile __instance)
        {
            try
            {
                if (__instance.definition == null ||
                    __instance.definition.jsonKey != PluginInfo.MissileKey) return;

                SalvoBudget.Register(__instance);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[Tenpin] Salvo budget registration failed: {ex}");
            }
        }
    }
}
