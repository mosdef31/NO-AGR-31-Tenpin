using System;
using System.Collections.Generic;
using UnityEngine;

namespace RocketPod
{

    internal sealed class NozzleCut : MonoBehaviour
    {
        private Missile? _round;
        private float _after;
        private List<ParticleSystem>? _systems;
        private bool _done;

        private static bool _logged;

        internal static void Attach(GameObject clone, Missile round,
                                    List<ParticleSystem> nozzle, float after)
        {
            if (clone == null || round == null || nozzle == null || nozzle.Count == 0) return;
            if (after <= 0f) return;

            NozzleCut cut = clone.AddComponent<NozzleCut>();
            cut._round = round;
            cut._after = after;

            cut._systems = new List<ParticleSystem>(nozzle);

            if (_logged) return;
            _logged = true;
            Plugin.Log.LogInfo(
                $"[Tenpin] Nozzle fire stops {after:0.##} s after launch on " +
                $"{nozzle.Count} system(s); the smoke, the trail and the haze run on.");
        }

        private void Update()
        {
            if (_done) return;

            try
            {
                if (_round == null) { _done = true; return; }
                if (_round.timeSinceSpawn < _after) return;

                _done = true;

                foreach (ParticleSystem ps in _systems!)
                {
                    if (ps == null) continue;
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }
            catch (Exception ex)
            {
                _done = true;
                Plugin.Log.LogWarning($"[Tenpin] The nozzle cut failed: {ex.Message}");
            }
        }
    }
}
