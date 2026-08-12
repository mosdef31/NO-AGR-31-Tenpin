using System;
using HarmonyLib;
using RocketPod.Hud;
using UnityEngine;

namespace RocketPod
{

    [HarmonyPatch(typeof(PilotPlayerState), "PlayerAxisControls")]
    internal static class PilotPlayerState_PlayerAxisControls_TiltAssistPatch
    {

        private const float PitchSign = -1f;

        private const float BearingGateDegrees = 30f;

        private const float ErrorForFullDemand = 400f;

        private const float Lead = 1.2f;

        private const float IntegralSeconds = 2f;

        private const float IntegralLimit = 0.5f;

        private const float DeadbandTolerances = 0.2f;

        private const float MinPitchEffectiveness = 0.25f;

        private const float CommandSlewSeconds = 0.35f;

        private const float Smoothing = 0.12f;

        private static float _lastError;
        private static bool _hasLastError;
        private static float _integral;

        private static float _rate;
        private static float _lastSolutionTime;

        private static float _demand;
        private static float _command;
        private static bool _logged;

        internal static bool Armed;

        internal static bool Engaged;

        internal static void Toggle()
        {
            Armed = !Armed;
            if (!Armed) { Engaged = false; StandDown(); }

            Plugin.Log.LogInfo($"[Tenpin] Tilt assist {(Armed ? "ARMED" : "off")}.");
        }

        private static void StandDown()
        {
            _demand = 0f;
            _command = 0f;
            _hasLastError = false;
            _rate = 0f;
            _lastSolutionTime = 0f;

            _integral = 0f;
        }

        [HarmonyPostfix]
        private static void Postfix()
        {
            try
            {
                Engaged = false;

                if (!Plugin.TiltAssist.Value || !Armed) { StandDown(); return; }

                if (Cursor.visible || DynamicMap.mapMaximized) { StandDown(); return; }

                Aircraft? aircraft = SceneSingleton<CombatHUD>.i?.aircraft;
                if (aircraft == null || aircraft.disabled) { StandDown(); return; }

                if (!IsOurStation(aircraft)) { StandDown(); return; }

                TenpinHud.Firing s = TenpinHud.Solution;
                if (!s.Fresh || !s.InReach) { StandDown(); return; }

                if (s.BearingOff > BearingGateDegrees) { StandDown(); return; }

                ControlInputs inputs = aircraft.GetInputs();
                if (inputs == null) return;

                float effectiveness = Vector3.Dot(aircraft.transform.up, Vector3.up);
                if (Mathf.Abs(effectiveness) < MinPitchEffectiveness) { StandDown(); return; }

                float error = -s.RangeError;

                if (Mathf.Abs(error) <= s.Tolerance * DeadbandTolerances) error = 0f;

                float dt = Mathf.Max(Time.fixedDeltaTime, 1e-4f);

                bool fresh = s.Time > _lastSolutionTime + 1e-6f;

                if (fresh)
                {
                    float sdt = _hasLastError ? Mathf.Max(s.Time - _lastSolutionTime, 1e-4f) : 0f;
                    _rate = _hasLastError ? (error - _lastError) / sdt : 0f;

                    _lastError = error;
                    _lastSolutionTime = s.Time;
                    _hasLastError = true;
                }

                float lookahead = error + Lead * _rate;
                float proportional = lookahead / ErrorForFullDemand;

                if (Mathf.Abs(proportional) < 1f)
                {
                    _integral += (error / ErrorForFullDemand) * (dt / IntegralSeconds);
                    _integral = Mathf.Clamp(_integral, -IntegralLimit, IntegralLimit);
                }

                if (error == 0f) _integral = Mathf.MoveTowards(_integral, 0f, dt / IntegralSeconds);

                float target = Mathf.Clamp(proportional + _integral, -1f, 1f);

                _demand = Mathf.Lerp(_demand, target,
                                     1f - Mathf.Exp(-dt / Smoothing));

                float authority = Mathf.Clamp01(Plugin.TiltAssistAuthority.Value);
                float wanted = PitchSign * _demand * authority * effectiveness;

                _command = Mathf.MoveTowards(_command, wanted, dt / CommandSlewSeconds);

                inputs.pitch = Mathf.Clamp(inputs.pitch + _command, -1f, 1f);
                Engaged = true;

                if (!_logged)
                {
                    _logged = true;
                    Plugin.Log.LogInfo(
                        $"[Tenpin] Tilt assist engaged, flying the elevation at up to {authority:P0} " +
                        "of the pitch axis. Your own input is added on top and is not capped. It " +
                        "stands down when the nose is more than " +
                        $"{BearingGateDegrees:F0} degrees off the target, when the map is open, or " +
                        "when there is no designation. Logged once.");
                }
            }
            catch (Exception ex)
            {
                Engaged = false;
                Plugin.Log.LogWarning(
                    $"[Tenpin] Tilt assist failed and is standing aside, so the aircraft flies " +
                    $"normally: {ex.Message}");
            }
        }

        private static bool IsOurStation(Aircraft aircraft)
        {
            WeaponStation? station = aircraft.weaponManager?.currentWeaponStation;
            return station?.WeaponInfo != null &&
                   station.WeaponInfo.weaponName == PluginInfo.WeaponInfoName;
        }
    }
}
