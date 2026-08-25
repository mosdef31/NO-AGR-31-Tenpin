using System;
using HarmonyLib;
using UnityEngine;

namespace RocketPod
{

    internal sealed class RoundSpin : MonoBehaviour
    {
        private float _angle;

        internal static bool IsDisplayRound(Missile missile) =>
            !missile.NetworkownerID.IsValid;

        internal static void Apply(Missile missile)
        {
            if (!Plugin.SpinRounds.Value) return;

            var source = missile.GetComponentInChildren<MeshRenderer>();
            if (source == null) return;

            var filter = source.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) return;

            if (missile.GetComponentInChildren<RoundSpin>() != null) return;

            var go = new GameObject("TenpinSpin");
            go.transform.SetParent(source.transform, worldPositionStays: false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            go.layer = source.gameObject.layer;

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = filter.sharedMesh;

            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = source.sharedMaterials;
            mr.shadowCastingMode = source.shadowCastingMode;
            mr.receiveShadows = source.receiveShadows;

            source.enabled = false;

            go.AddComponent<RoundSpin>();
        }

        private void Update()
        {

            _angle += Plugin.SpinDegreesPerSecond.Value * Time.deltaTime;
            if (_angle > 360f || _angle < -360f) _angle %= 360f;
            transform.localRotation = Quaternion.Euler(0f, 0f, _angle);
        }
    }

    [HarmonyPatch(typeof(Missile), "OnStartClient")]
    internal static class Missile_OnStartClient_SpinPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Missile __instance)
        {
            try
            {
                if (__instance.definition == null ||
                    __instance.definition.jsonKey != PluginInfo.MissileKey) return;

                if (RoundSpin.IsDisplayRound(__instance)) return;

                RoundSpin.Apply(__instance);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[Tenpin] Round spin failed (the rocket still flies): {ex}");
            }
        }
    }
}
