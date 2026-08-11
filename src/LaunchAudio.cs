using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Audio;

namespace RocketPod
{

    internal static class LaunchAudio
    {

        private const int Voices = 8;

        private const float PitchLow = 0.95f;
        private const float PitchHigh = 1.05f;

        private static readonly FieldInfo? _fLaunchSound =
            AccessTools.Field(typeof(MissileLauncher), "launchSound");
        private static readonly FieldInfo? _fLaunchTransform =
            AccessTools.Field(typeof(MissileLauncher), "launchTransform");

        private static AudioSource[]? _pool;
        private static int _next;
        private static bool _resolved;
        private static AudioClip? _clip;
        private static AudioMixerGroup? _mixer;
        private static bool _playedLogged;

        internal static void Report(MissileLauncher launcher)
        {

            if (_fLaunchSound?.GetValue(launcher) as AudioSource != null) return;

            if (!Resolve()) return;
            if (_pool == null || _clip == null) return;

            Transform at = _fLaunchTransform?.GetValue(launcher) as Transform ?? launcher.transform;

            AudioSource src = _pool[_next];
            _next = (_next + 1) % _pool.Length;

            src.transform.position = at.position;
            src.pitch = UnityEngine.Random.Range(PitchLow, PitchHigh);
            src.PlayOneShot(_clip);

            if (!_playedLogged)
            {
                _playedLogged = true;
                Plugin.Log.LogInfo(
                    $"[Tenpin] Launch report: '{_clip.name}' ({_clip.length:0.##}s) over {Voices} " +
                    "voices. The prefab authors MissileLauncher.launchSound null, so this fills it; " +
                    "author a real one in Unity and this stops running. Logged once.");
            }
        }

        internal static AudioClip? SharedClip() => Resolve() ? _clip : null;

        private static bool Resolve()
        {
            if (_resolved) return _clip != null;
            _resolved = true;

            try
            {
                _clip = FindClip(out _mixer, out string from);
                if (_clip == null)
                {
                    Plugin.Log.LogWarning(
                        "[Tenpin] No stock AudioClip could be borrowed for the launch report, so " +
                        "the pod fires silently. Author MissileLauncher.launchSound in Unity.");
                    return false;
                }

                var host = new GameObject("Tenpin_LaunchAudio");
                UnityEngine.Object.DontDestroyOnLoad(host);

                _pool = new AudioSource[Voices];
                for (int i = 0; i < Voices; i++)
                {
                    var go = new GameObject($"Voice{i}");
                    go.transform.SetParent(host.transform, false);
                    _pool[i] = Configure(go.AddComponent<AudioSource>());
                }

                Plugin.Log.LogInfo(
                    $"[Tenpin] Launch report borrowed from {from}: clip '{_clip.name}'.");
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError(
                    $"[Tenpin] Could not build the launch audio pool, the pod fires silently: {ex}");
                _clip = null;
                return false;
            }
        }

        internal static AudioSource Configure(AudioSource src)
        {
            src.playOnAwake = false;
            src.loop = false;
            src.spatialBlend = 1f;
            src.rolloffMode = AudioRolloffMode.Logarithmic;
            src.minDistance = 25f;
            src.maxDistance = 2500f;
            src.dopplerLevel = 0.2f;
            src.outputAudioMixerGroup = _mixer;
            return src;
        }

        private static AudioClip? FindClip(out AudioMixerGroup? mixer, out string from)
        {
            mixer = null;
            from = "(nothing)";

            AudioSource? best = MotorSources()
                .OrderByDescending(s => s.clip != null && s.clip.length > 0.2f)
                .ThenBy(s => s.clip != null ? s.clip.length : float.MaxValue)
                .FirstOrDefault(s => s.clip != null);

            if (best?.clip != null)
            {
                mixer = best.outputAudioMixerGroup;
                from = "a stock missile motor";
                return best.clip;
            }

            AudioClip? gun = GunClips().FirstOrDefault(c => c != null);
            if (gun != null)
            {
                from = "a stock gun (no motor clip was reachable)";
                return gun;
            }

            return null;
        }

        private static IEnumerable<AudioSource> MotorSources()
        {
            Encyclopedia? enc = null;
            enc = GameData.EncyclopediaOrNull();
            if (enc?.missiles == null) yield break;

            FieldInfo? fMotors = AccessTools.Field(typeof(Missile), "motors");
            if (fMotors == null) yield break;

            foreach (MissileDefinition md in enc.missiles)
            {
                if (md == null || md.unitPrefab == null) continue;
                if (md.jsonKey == PluginInfo.MissileKey) continue;

                Missile? m = md.unitPrefab.GetComponent<Missile>();
                if (m == null) continue;
                if (fMotors.GetValue(m) is not Array motors) continue;

                foreach (object? motor in motors)
                {
                    if (motor == null) continue;
                    if (AccessTools.Field(motor.GetType(), "audioSources")?.GetValue(motor)
                        is not AudioSource[] sources) continue;

                    foreach (AudioSource s in sources)
                        if (s != null) yield return s;
                }
            }
        }

        private static IEnumerable<AudioClip> GunClips()
        {
            Encyclopedia? enc = null;
            enc = GameData.EncyclopediaOrNull();
            if (enc?.weaponMounts == null) yield break;

            FieldInfo? fFireSounds = AccessTools.Field(typeof(Gun), "fireSounds");
            if (fFireSounds == null) yield break;

            foreach (WeaponMount wm in enc.weaponMounts)
            {
                if (wm == null || wm.prefab == null) continue;
                if (PluginInfo.IsOurMount(wm.jsonKey)) continue;

                foreach (Gun g in wm.prefab.GetComponentsInChildren<Gun>(true))
                {
                    if (fFireSounds.GetValue(g) is not AudioClip[] clips) continue;
                    foreach (AudioClip c in clips)
                        if (c != null) yield return c;
                }
            }
        }
    }

    internal static class FlightAudio
    {
        private static readonly FieldInfo? _fFlightSound =
            AccessTools.Field(typeof(Missile), "flightSound");
        private static readonly FieldInfo? _fNearbyDetonation =
            AccessTools.Field(typeof(Missile), "nearbyDetonationClip");

        private static bool _logged;

        internal static void Apply(Missile round)
        {
            if (_fFlightSound == null) return;

            if (_fFlightSound.GetValue(round) as AudioSource != null) return;

            if (!SalvoBudget.TryTakeVoice(round)) return;

            AudioClip? clip = LaunchAudio.SharedClip();
            if (clip == null) return;

            var go = new GameObject("Tenpin_FlightSound");
            go.transform.SetParent(round.transform, false);

            AudioSource src = LaunchAudio.Configure(go.AddComponent<AudioSource>());
            src.clip = clip;
            src.loop = true;
            src.dopplerLevel = 1f;
            src.minDistance = 40f;
            src.maxDistance = 4000f;

            _fFlightSound.SetValue(round, src);

            if (_fNearbyDetonation != null &&
                _fNearbyDetonation.GetValue(round) as AudioClip == null)
                _fNearbyDetonation.SetValue(round, clip);

            round.RegisterDopplerSound(src);
            src.volume = 0f;
            src.Play();

            if (!_logged)
            {
                _logged = true;
                Plugin.Log.LogInfo(
                    $"[Tenpin] Flight sound: '{clip.name}' on Missile.flightSound, doppler " +
                    "registered, volume and pitch driven by the game from speed. Author one in " +
                    "the bundle and this stops running. Logged once.");
            }
        }
    }

    [HarmonyPatch(typeof(Missile), "OnStartClient")]
    internal static class Missile_OnStartClient_FlightAudioPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Missile __instance)
        {
            try
            {
                if (__instance.definition == null ||
                    __instance.definition.jsonKey != PluginInfo.MissileKey) return;

                FlightAudio.Apply(__instance);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError(
                    $"[Tenpin] Flight audio failed (the rocket still flies): {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(MissileLauncher), nameof(MissileLauncher.Fire))]
    internal static class MissileLauncher_Fire_AudioPatch
    {
        [HarmonyPostfix]
        private static void Postfix(MissileLauncher __instance)
        {
            try
            {
                if (__instance.info == null ||
                    __instance.info.weaponName != PluginInfo.WeaponInfoName) return;

                LaunchAudio.Report(__instance);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError(
                    $"[Tenpin] Launch audio failed (the rocket still fires): {ex}");
            }
        }
    }
}
