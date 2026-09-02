using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace RocketPod
{

    internal static class SillyEffects
    {
        private const string MotorFxName = "Tenpin_MotorFX";
        private const string LaunchFxName = "Tenpin_LaunchFX";

        internal static bool On => Plugin.SillyEffectsEnabled.Value;

        private static readonly Color FlameCore = Rgb(0xC8, 0xF6, 0xFF);
        private static readonly Color FlameMid = Rgb(0x3F, 0xC9, 0xFF);
        private static readonly Color FlameFringe = Rgb(0x7A, 0x4B, 0xFF);
        private static readonly Color FlameDying = Rgb(0x2A, 0x1B, 0x5E);
        private static readonly Color SmokeNear = Rgb(0xB9, 0xC2, 0xCC);
        private static readonly Color SmokeFar = Rgb(0x6E, 0x74, 0x7C);
        private static readonly Color FlashCore = Rgb(0xEA, 0xFD, 0xFF);
        private static readonly Color FlashEdge = Rgb(0x48, 0xD4, 0xFF);

        private static readonly FieldInfo? FEffectsTransform =
            AccessTools.Field(typeof(Missile), "effectsTransform");

        private static readonly FieldInfo? FMotors =
            AccessTools.Field(typeof(Missile), "motors");

        private static bool _motorLogged;
        private static bool _launcherLogged;

        internal static void ApplyMotor(Missile ours)
        {
            if (!On) { StripMotor(ours); return; }

            try
            {
                Transform? fx = ours.transform.Find(MotorFxName)
                                ?? FEffectsTransform?.GetValue(ours) as Transform;
                if (fx == null) return;

                int touched = 0;
                foreach (ParticleSystem ps in fx.GetComponentsInChildren<ParticleSystem>(true))
                    if (ps != null && Recolour(ps)) touched++;

                foreach (Light l in fx.GetComponentsInChildren<Light>(true))
                    if (l != null) l.color = FlameMid;

                if (_motorLogged || touched == 0) return;
                _motorLogged = true;

                Plugin.Log.LogInfo(
                    $"[Tenpin] Silly effects ON: the plume was recoloured cyan " +
                    $"({touched} system(s)).");
            }
            catch (Exception ex)
            {

                Plugin.Log.LogError($"[Tenpin] Recolouring the plume failed (it still flies): {ex}");
            }
        }

        internal static void ApplyLauncher(TenpinLauncher launcher)
        {
            if (!On) { StripLauncher(launcher); return; }

            try
            {
                Transform? fx = launcher.transform.Find(LaunchFxName);
                if (fx == null) return;

                foreach (ParticleSystem ps in fx.GetComponentsInChildren<ParticleSystem>(true))
                {
                    if (ps == null) continue;
                    ParticleSystem.MainModule main = ps.main;
                    main.startColor = new ParticleSystem.MinMaxGradient(FlashCore, FlashEdge);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[Tenpin] Recolouring the tube flash failed: {ex}");
            }
        }

        private static bool Recolour(ParticleSystem ps)
        {
            ParticleSystem.MainModule main = ps.main;
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;

            switch (ps.gameObject.name)
            {
                case "Flame":
                    main.startColor = new ParticleSystem.MinMaxGradient(Color.white);
                    col.enabled = true;
                    col.color = new ParticleSystem.MinMaxGradient(Ramp(
                        (FlameCore, 0f, 1f), (FlameMid, 0.3f, 1f),
                        (FlameFringe, 0.7f, 1f), (FlameDying, 1f, 0f)));
                    return true;

                case "Embers":
                    main.startColor = new ParticleSystem.MinMaxGradient(FlameCore, FlameMid);
                    col.enabled = true;
                    col.color = new ParticleSystem.MinMaxGradient(Ramp(
                        (FlameCore, 0f, 1f), (FlameFringe, 1f, 0f)));
                    return true;

                case "TrailSystem":
                    main.startColor = new ParticleSystem.MinMaxGradient(Color.white);
                    col.enabled = true;
                    col.color = new ParticleSystem.MinMaxGradient(Ramp(
                        (FlameFringe, 0f, 1f), (SmokeNear, 0.18f, 1f), (SmokeFar, 1f, 0f)));
                    return true;

                default:

                    return false;
            }
        }

        private static void StripMotor(Missile ours)
        {
            try
            {
                if (FMotors?.GetValue(ours) is not Array motors || motors.Length == 0) return;

                object? motor = motors.GetValue(0);
                if (motor == null) return;

                int cleared = 0;
                foreach (string field in new[] { "particleSystems", "trailEmitters", "lights" })
                    cleared += ClearArray(motor, field);

                Transform? fx = ours.transform.Find(MotorFxName);
                if (FEffectsTransform?.GetValue(ours) as Transform is { } et &&
                    (fx == null || et == fx || et.IsChildOf(fx)))
                    FEffectsTransform?.SetValue(ours, null);

                if (fx != null) fx.gameObject.SetActive(false);

                if (_motorLogged || (cleared == 0 && fx == null)) return;
                _motorLogged = true;

                Plugin.Log.LogInfo(
                    $"[Tenpin] Authored effects removed ({cleared} slot(s) " +
                    (fx != null ? $"cleared, '{MotorFxName}' disabled" : "cleared") +
                    $"), borrowing '{Plugin.MotorEffectDonor.Value}'.");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[Tenpin] Stripping the authored plume failed: {ex}");
            }
        }

        private static void StripLauncher(TenpinLauncher launcher)
        {
            try
            {
                Transform? fx = launcher.transform.Find(LaunchFxName);

                bool cleared = false;
                if (launcher.launchParticles is { } ps && ps != null &&
                    (fx == null || ps.transform.IsChildOf(fx)))
                {
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    launcher.launchParticles = null;
                    cleared = true;
                }

                if (fx != null) fx.gameObject.SetActive(false);

                if (_launcherLogged || (!cleared && fx == null)) return;
                _launcherLogged = true;

                Plugin.Log.LogInfo(
                    "[Tenpin] The authored launch flash was taken back out and a stock one is " +
                    "borrowed instead.");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[Tenpin] Stripping the tube flash failed: {ex}");
            }
        }

        private static int ClearArray(object owner, string field)
        {
            FieldInfo? f = AccessTools.Field(owner.GetType(), field);
            if (f?.GetValue(owner) is not Array current || current.Length == 0) return 0;

            Type? element = f.FieldType.GetElementType();
            if (element == null) return 0;

            f.SetValue(owner, Array.CreateInstance(element, 0));
            return 1;
        }

        private static Gradient Ramp(params (Color C, float T, float A)[] keys)
        {
            var g = new Gradient();
            var ck = new GradientColorKey[keys.Length];
            var ak = new GradientAlphaKey[keys.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                ck[i] = new GradientColorKey(keys[i].C, keys[i].T);
                ak[i] = new GradientAlphaKey(keys[i].A, keys[i].T);
            }
            g.SetKeys(ck, ak);
            return g;
        }

        private static Color Rgb(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f);
    }
}
