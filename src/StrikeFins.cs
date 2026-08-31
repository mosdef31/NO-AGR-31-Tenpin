using System;
using HarmonyLib;
using UnityEngine;

namespace RocketPod
{

    internal sealed class StrikeFins : MonoBehaviour
    {

        private const string FinPrefix = "Fin";

        private const string DeployedPrefix = "FinDeployed";

        private const int FinCount = 4;

        private Transform[] _fins = Array.Empty<Transform>();
        private Quaternion[] _folded = Array.Empty<Quaternion>();
        private Quaternion[] _deployed = Array.Empty<Quaternion>();

        private float _t;
        private bool _done;

        internal static void Apply(Missile missile)
        {
            if (missile == null) return;
            if (missile.GetComponent<StrikeFins>() != null) return;
            missile.gameObject.AddComponent<StrikeFins>();
        }

        private void Awake()
        {
            var fins = new Transform[FinCount];
            var folded = new Quaternion[FinCount];
            var deployed = new Quaternion[FinCount];

            int found = 0;
            for (int i = 0; i < FinCount; i++)
            {
                Transform f = transform.Find(FinPrefix + i);
                if (f == null) continue;

                fins[i] = f;
                folded[i] = f.localRotation;

                Transform? d = transform.Find(DeployedPrefix + i);
                if (d == null)
                {

                    Plugin.Log.LogError(
                        $"[Tenpin] AGR-51 round has no '{DeployedPrefix}{i}' child, so there is no " +
                        "deployed pose to swing to and this fin will stay folded. The bundle is " +
                        "older than the DLL: re-run art/make_strike.py with --fbx, rebuild the " +
                        "prefab and re-export tenpin.nobp.");
                    deployed[i] = folded[i];
                    continue;
                }

                deployed[i] = d.localRotation;

                foreach (Renderer r in d.GetComponentsInChildren<Renderer>(true))
                    r.enabled = false;

                found++;
            }

            if (found != FinCount)
            {
                Plugin.Log.LogWarning(
                    $"[Tenpin] AGR-51 round has {found} of {FinCount} fin pairs named " +
                    $"{FinPrefix}0..{FinPrefix}{FinCount - 1} and {DeployedPrefix}0.." +
                    $"{DeployedPrefix}{FinCount - 1}. The missing ones cannot deploy. This is an " +
                    "asset fault, not a timing one - check the round prefab's children.");
            }

            _fins = fins;
            _folded = folded;
            _deployed = deployed;

        }

        private void Update()
        {
            if (_done) return;

            _t += Time.deltaTime;

            float hold = Mathf.Max(0f, Plugin.StrikeFinHold.Value);
            if (_t < hold) return;

            float sweep = Mathf.Max(0.01f, Plugin.StrikeFinSweep.Value);
            float k = Mathf.Clamp01((_t - hold) / sweep);

            float e = 1f - (1f - k) * (1f - k);

            for (int i = 0; i < _fins.Length; i++)
            {
                Transform f = _fins[i];
                if (f == null) continue;
                f.localRotation = Quaternion.Slerp(_folded[i], _deployed[i], e);
            }

            if (k >= 1f) _done = true;
        }
    }

    [HarmonyPatch(typeof(Missile), "OnStartClient")]
    internal static class Missile_OnStartClient_StrikeFinPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Missile __instance)
        {
            try
            {
                if (__instance.definition == null) return;
                if (__instance.definition.jsonKey != PluginInfo.MissileKey51) return;

                StrikeFins.Apply(__instance);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[Tenpin] AGR-51 fin deploy failed (the rocket still flies): {ex}");
            }
        }
    }
}
