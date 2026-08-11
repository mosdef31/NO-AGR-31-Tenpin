using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace RocketPod
{

    internal static class FunEffects
    {

        private const string MotorFxName = "Tenpin_MotorFX";

        private const string LaunchFxName = "Tenpin_LaunchFX";

        private static readonly FieldInfo? _fMotors =
            AccessTools.Field(typeof(Missile), "motors");
        private static readonly FieldInfo? _fEffectsTransform =
            AccessTools.Field(typeof(Missile), "effectsTransform");
        private static readonly FieldInfo? _fLaunchParticles =
            AccessTools.Field(typeof(MissileLauncher), "launchParticles");

        private static bool _motorLogged;
        private static bool _launcherLogged;

        internal static bool On => Plugin.FunEffectsEnabled.Value;

        internal static void StripMotor(Missile ours)
        {
            if (On) return;
            if (_fMotors?.GetValue(ours) is not Array motors || motors.Length == 0) return;

            object? motor = motors.GetValue(0);
            if (motor == null) return;

            int cleared = 0;
            foreach (string field in new[] { "particleSystems", "trailEmitters", "lights" })
                cleared += ClearArray(motor, field);

            Transform? fx = ours.transform.Find(MotorFxName);
            if (_fEffectsTransform?.GetValue(ours) as Transform is { } et &&
                (fx == null || et == fx || et.IsChildOf(fx)))
                _fEffectsTransform?.SetValue(ours, null);

            if (fx != null) fx.gameObject.SetActive(false);

            if (!_motorLogged && (cleared > 0 || fx != null))
            {
                _motorLogged = true;
                Plugin.Log.LogInfo(
                    $"[Tenpin] Fun effects are OFF, so the authored motor effects were taken back " +
                    $"out ({cleared} slot(s) cleared" +
                    (fx != null ? $", '{MotorFxName}' disabled" : "") +
                    ") and the stock ones are borrowed instead. Turn on Effects/Fun effects for " +
                    "the authored cyan plume.");
            }
        }

        internal static void StripLauncher(MissileLauncher launcher)
        {
            if (On) return;
            if (_fLaunchParticles == null) return;

            Transform? fx = launcher.transform.Find(LaunchFxName);

            bool cleared = false;
            if (_fLaunchParticles.GetValue(launcher) is ParticleSystem ps && ps != null &&
                (fx == null || ps.transform.IsChildOf(fx)))
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                _fLaunchParticles.SetValue(launcher, null);
                cleared = true;
            }

            if (fx != null) fx.gameObject.SetActive(false);

            if (!_launcherLogged && (cleared || fx != null))
            {
                _launcherLogged = true;
                Plugin.Log.LogInfo(
                    "[Tenpin] Fun effects are OFF, so the authored launch flash was taken back out " +
                    "and a stock one is borrowed instead.");
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
    }
}
