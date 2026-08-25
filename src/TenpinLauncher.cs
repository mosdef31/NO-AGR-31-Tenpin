using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Cysharp.Threading.Tasks;
using HarmonyLib;
using UnityEngine;

namespace RocketPod
{

    internal sealed class TenpinLauncher : Weapon
    {

        internal MissileDefinition? missile;
        internal Transform[] launchTransforms = Array.Empty<Transform>();
        internal float fireInterval = 0.08f;
        internal float reloadTime;
        internal Vector3 ejectionVelocity;
        internal AudioSource? launchSound;
        internal ParticleSystem? launchParticles;

        private int _maxAmmo;
        private int _currentCell;
        private Transform? _fallbackTransform;

        private static readonly HashSet<WeaponStation> _ripplingStations
            = new HashSet<WeaponStation>();

        private float HoldGrace => Mathf.Max(0.12f, fireInterval * 2f);

        private const float CommandInterval = 0.30f;

        private const int LaunchFlashParticles = 3;

        private static readonly Dictionary<WeaponStation, float> _holdUntil
            = new Dictionary<WeaponStation, float>();

        private float _lastCommandAt = -99f;

        private int _podCursor;

        private static readonly FieldInfo? F_missile
            = AccessTools.Field(typeof(MissileLauncher), "missile");
        private static readonly FieldInfo? F_launchTransforms
            = AccessTools.Field(typeof(MissileLauncher), "launchTransforms");
        private static readonly FieldInfo? F_fireInterval
            = AccessTools.Field(typeof(MissileLauncher), "fireInterval");
        private static readonly FieldInfo? F_reloadTime
            = AccessTools.Field(typeof(MissileLauncher), "reloadTime");
        private static readonly FieldInfo? F_ejectionVelocity
            = AccessTools.Field(typeof(MissileLauncher), "ejectionVelocity");
        private static readonly FieldInfo? F_launchSound
            = AccessTools.Field(typeof(MissileLauncher), "launchSound");
        private static readonly FieldInfo? F_launchParticles
            = AccessTools.Field(typeof(MissileLauncher), "launchParticles");

        internal bool Adopt(MissileLauncher from)
        {
            missile = F_missile?.GetValue(from) as MissileDefinition;
            launchTransforms = F_launchTransforms?.GetValue(from) as Transform[]
                               ?? Array.Empty<Transform>();
            if (F_fireInterval?.GetValue(from) is float fi) fireInterval = fi;
            if (F_reloadTime?.GetValue(from) is float rt) reloadTime = rt;
            if (F_ejectionVelocity?.GetValue(from) is Vector3 ev) ejectionVelocity = ev;
            launchSound = F_launchSound?.GetValue(from) as AudioSource;
            launchParticles = F_launchParticles?.GetValue(from) as ParticleSystem;

            attachedUnit = from.attachedUnit;
            info = from.info;
            CorrectRoleIdentity(info);
            ammo = from.ammo;
            priority = from.priority;
            Rearmable = from.Rearmable;
            RequestRearmLevel = from.RequestRearmLevel;
            Safety = from.Safety;

            return missile != null && launchTransforms.Length > 0;
        }

        private bool _configured;

        internal void Configure()
        {
            _configured = true;
            OnEnableBody();
        }

        private void OnEnable()
        {

            if (!_configured) return;
            OnEnableBody();
        }

        private void OnEnableBody()
        {
            lastFired = 0f - fireInterval;

            for (int i = 0; i < launchTransforms.Length; i++)
                if (launchTransforms[i] != null)
                    launchTransforms[i].gameObject.SetActive(false);

            if (_fallbackTransform == null)
            {
                _fallbackTransform = new GameObject("launchTransform").transform;
                _fallbackTransform.parent = transform;
                _fallbackTransform.localPosition = Vector3.zero;
                _fallbackTransform.localRotation = Quaternion.identity;
            }

            _maxAmmo = ammo;

            try
            {

                if (Plugin.UseStockEffects.Value)
                {

                    SillyEffects.ApplyLauncher(this);
                    LaunchParticles.Apply(this);

                    if (launchParticles != null)
                    {
                        ParticleSystem.MainModule main = launchParticles.main;
                        main.simulationSpace = ParticleSystemSimulationSpace.Local;

                        LogFlashBind(main);
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError(
                    $"[Tenpin] Borrowing a launch flash failed (the pod still fires): {ex}");
            }

            try
            {
                if (Plugin.HideFiredRounds.Value && GetComponent<RoundVisuals>() == null)
                {
                    var visuals = gameObject.AddComponent<RoundVisuals>();
                    if (!visuals.Bind(this)) UnityEngine.Object.Destroy(visuals);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[Tenpin] Round visuals failed (firing is unaffected): {ex}");
            }
        }

        public override void Fire(Unit owner, Unit target, Vector3 inheritedVelocity,
                                  WeaponStation station, GlobalPosition aimpoint)
        {
            if (Safety || ammo <= 0) return;
            if (!IsAttached()) return;

            if (station == null) return;

            this.weaponStation = station;

            LaunchTelemetry.NoteRole(owner);

            _holdUntil[station] = Time.timeSinceLevelLoad + HoldGrace;

            bool isServer = owner.IsServer;
            bool hasAuthority = owner.HasAuthority;

            if (owner is Aircraft aircraft && !isServer && hasAuthority)
            {
                float now = Time.timeSinceLevelLoad;
                if (now - _lastCommandAt >= CommandInterval)
                {
                    _lastCommandAt = now;
                    aircraft.CmdLaunchMissile(station.Number, target, aimpoint);
                    LaunchTelemetry.CommandSent();
                }
            }
            else if (owner is Aircraft host && isServer)
            {
                float now = Time.timeSinceLevelLoad;
                if (now - _lastCommandAt >= CommandInterval)
                {
                    _lastCommandAt = now;
                    host.RpcLaunchMissile(station.Number, target, aimpoint);
                }
            }

            bool spawns = isServer;

            PruneSupersededLaunchers(station);

            if (!_ripplingStations.Add(station)) return;
            StartCoroutine(Ripple(owner, target, inheritedVelocity, station, aimpoint, spawns));
        }

        private IEnumerator Ripple(Unit owner, Unit target, Vector3 inheritedVelocity,
                                   WeaponStation station, GlobalPosition aimpoint, bool spawns)
        {
            LaunchTelemetry.RippleStarted(this);
            int fired = 0;

            try
            {

                float due = Time.timeSinceLevelLoad;

                while (TriggerStillDown(station))
                {
                    if (owner == null) break;

                    if (Time.timeSinceLevelLoad < due)
                    {
                        yield return null;
                        continue;
                    }

                    TenpinLauncher? pod = NextPodInStation(station);
                    if (pod == null) break;

                    int tubeIndex = pod.NextTubeIndex();
                    Transform? tube = pod.TubeAt(tubeIndex);
                    if (tube == null) break;

                    long t0 = System.Diagnostics.Stopwatch.GetTimestamp();

                    pod.ammo--;
                    fired++;
                    pod.lastFired = Time.timeSinceLevelLoad;

                    pod.TrackFiringVisibility().Forget();

                    if (spawns && pod.missile != null)
                    {
                        Vector3 velocity = inheritedVelocity
                                         + pod.ejectionVelocity.x * tube.right
                                         + pod.ejectionVelocity.y * tube.up
                                         + pod.ejectionVelocity.z * tube.forward;

                        Missile? spawned = NetworkSceneSingleton<Spawner>.i.SpawnMissile(
                            pod.missile, tube.position, tube.rotation, velocity, target, owner);

                        LaunchTelemetry.SpawnResult(pod, spawned);

                        if (spawned != null) StartCoroutine(ConfirmStillAlive(spawned));

                        if (owner.NetworkHQ != null && pod.info != null)
                            owner.NetworkHQ.missionStatsTracker.MunitionCost(owner, pod.info.costPerRound);
                    }

                    pod.PlayTubeEffects(tube);
                    LaunchTelemetry.TubeUsed(pod, tubeIndex);

                    station.UpdateLastFired(1);
                    station.AccountAmmo();
                    station.Updated();

                    if (owner.IsServer)
                        owner.RpcSyncAmmoCount(station.Number, station.Ammo);

                    LaunchTelemetry.RoundLeft(pod, station, spawns, fired);
                    LaunchTelemetry.NoteFrameCost(
                        (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0
                        / System.Diagnostics.Stopwatch.Frequency);

                    due += fireInterval;

                    if (Time.timeSinceLevelLoad - due > fireInterval)
                        due = Time.timeSinceLevelLoad + fireInterval;

                    yield return null;
                }
            }
            finally
            {
                _ripplingStations.Remove(station);
                _holdUntil.Remove(station);
            }

            if (ammo == 0) ReportReloading(true);
            if (Rearmable && owner != null) owner.RequestRearm();

            if (owner != null)
                LaunchTelemetry.RippleEnded(this, station, spawns, fired);
        }

        private IEnumerator ConfirmStillAlive(Missile round)
        {
            yield return new WaitForSeconds(1f);
            LaunchTelemetry.RoundStillAlive(this, round != null);
        }

        private static bool _roleCorrected;

        private static void CorrectRoleIdentity(WeaponInfo? weaponInfo)
        {
            if (weaponInfo == null || _roleCorrected) return;
            if (weaponInfo.effectiveness.antiSurface <= AntiSurface) return;

            _roleCorrected = true;

            float was = weaponInfo.effectiveness.antiSurface;
            RoleIdentity role = weaponInfo.effectiveness;
            role.antiSurface = AntiSurface;
            weaponInfo.effectiveness = role;

            Plugin.Log.LogInfo(
                $"[Tenpin] Anti-surface effectiveness {was:0.00} -> {AntiSurface:0.00}. At {was:0.00} " +
                "this pod scored higher against every surface target than any guided weapon in " +
                "the game, so an AI carrying one reached for it whatever the target was. The " +
                "stock chooser was right and the stat was wrong. Logged once.");
        }

        private const float AntiSurface = 0.55f;

        private bool TriggerStillDown(WeaponStation station)
        {
            if (WeaponManager_Fire_ReleaseAssistPatch.TriggerWatch.Watching(station))
                return WeaponManager_Fire_ReleaseAssistPatch.TriggerWatch.Down(station);

            return _holdUntil.TryGetValue(station, out float until) &&
                   Time.timeSinceLevelLoad < until;
        }

        internal static void PruneSupersededLaunchers(WeaponStation station, bool reaccount = true)
        {
            List<Weapon>? weapons = station.Weapons;
            if (weapons == null) return;

            bool oursHere = false;
            foreach (Weapon w in weapons)
                if (w is TenpinLauncher) { oursHere = true; break; }

            if (!oursHere) return;

            bool removed = false;
            for (int i = weapons.Count - 1; i >= 0; i--)
            {
                Weapon w = weapons[i];

                if (w == null || w is MissileLauncher)
                {
                    weapons.RemoveAt(i);
                    removed = true;
                }
            }

            if (!removed) return;

            if (reaccount)
            {
                station.AccountAmmo();
                station.Updated();
            }

            if (!_prunedLogged)
            {
                _prunedLogged = true;
                Plugin.Log.LogInfo(
                    "[Tenpin] Removed the swapped-out MissileLauncher from the weapon station. " +
                    "Hardpoint.SpawnMount registers every Weapon it finds on the mount AFTER " +
                    "the instantiate that triggers our swap, and Destroy is deferred, so both " +
                    "components were being counted and the ammo read high. Logged once.");
            }
        }

        private static bool _prunedLogged;

        [HarmonyPatch(typeof(WeaponStation), nameof(WeaponStation.AccountAmmo))]
        internal static class WeaponStation_AccountAmmo_PrunePatch
        {
            private static bool _inside;

            [HarmonyPrefix]
            private static void Prefix(WeaponStation __instance)
            {
                if (_inside || __instance == null) return;

                try
                {
                    _inside = true;
                    PruneSupersededLaunchers(__instance, reaccount: false);
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning(
                        $"[Tenpin] Ammo prune failed, the station may read high: {ex.Message}");
                }
                finally
                {
                    _inside = false;
                }
            }
        }

        private TenpinLauncher? NextPodInStation(WeaponStation station)
        {
            if (station?.Weapons == null || station.Weapons.Count == 0)
                return ammo > 0 && IsAttached() ? this : null;

            int count = station.Weapons.Count;
            for (int step = 0; step < count; step++)
            {
                int index = (_podCursor + step) % count;
                if (station.Weapons[index] is not TenpinLauncher pod) continue;
                if (pod == null || pod.ammo <= 0 || !pod.IsAttached()) continue;

                _podCursor = (index + 1) % count;
                return pod;
            }
            return null;
        }

        private int NextTubeIndex()
        {
            if (launchTransforms.Length == 0) return -1;

            for (int attempt = 0; attempt < launchTransforms.Length; attempt++)
            {
                int index = _currentCell;
                _currentCell = (_currentCell + 1) % launchTransforms.Length;
                if (launchTransforms[index] != null) return index;
            }
            return -1;
        }

        private Transform? TubeAt(int index)
            => index >= 0 && index < launchTransforms.Length
                ? launchTransforms[index]
                : _fallbackTransform;

        private static bool _flashBindLogged;
        private static bool _flashEmitLogged;

        private void LogFlashBind(ParticleSystem.MainModule main)
        {
            if (_flashBindLogged) return;
            _flashBindLogged = true;

            Transform? p = launchParticles!.transform.parent;
            bool oursToo = p != null && p.IsChildOf(transform);

            Plugin.Log.LogInfo(
                "[Tenpin] FLASH BIND: " +
                $"system='{launchParticles.gameObject.name}' " +
                $"parent='{(p == null ? "(none - ROOT)" : p.name)}' " +
                $"parentUnderThisPod={oursToo} " +
                $"simulationSpace={main.simulationSpace} " +
                $"customSpace='{(main.customSimulationSpace == null ? "(null)" : main.customSimulationSpace.name)}' " +
                $"systemWorldPos={launchParticles.transform.position} " +
                $"podWorldPos={transform.position} " +
                $"offsetFromPod={(launchParticles.transform.position - transform.position).magnitude:0.###}m " +
                $"scale={launchParticles.transform.lossyScale}");

            foreach (ParticleSystem child in launchParticles.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (child == launchParticles) continue;
                ParticleSystem.MainModule cm = child.main;
                Plugin.Log.LogInfo(
                    $"[Tenpin] FLASH BIND child: '{child.gameObject.name}' " +
                    $"simulationSpace={cm.simulationSpace} " +
                    $"customSpace='{(cm.customSimulationSpace == null ? "(null)" : cm.customSimulationSpace.name)}' " +
                    $"emitting={child.emission.enabled} " +
                    $"startSpeed={cm.startSpeed.constantMax:0.##} " +
                    $"lifetime={cm.startLifetime.constantMax:0.###}s " +
                    $"active={child.gameObject.activeInHierarchy}");
            }

            if (!oursToo)
                Plugin.Log.LogWarning(
                    "[Tenpin] FLASH BIND: the launch flash is NOT parented under this pod. " +
                    "Under a Local simulation space every particle is then positioned relative " +
                    "to a transform that moves independently, which is the displaced-then-" +
                    "overshooting flash. Reparent it or author the effect in Unity.");
        }

        private void LogFlashEmit(Transform tube, Vector3 localAt)
        {
            if (_flashEmitLogged) return;
            _flashEmitLogged = true;

            Vector3 back = launchParticles!.transform.TransformPoint(localAt);

            Plugin.Log.LogInfo(
                "[Tenpin] FLASH EMIT: " +
                $"tubeWorld={tube.position} " +
                $"convertedLocal={localAt} " +
                $"roundTripWorld={back} " +
                $"roundTripError={(back - tube.position).magnitude:0.###}m " +
                $"systemWorldPos={launchParticles.transform.position} " +
                $"systemParent='{(launchParticles.transform.parent == null ? "(none - ROOT)" : launchParticles.transform.parent.name)}'");
        }

        private void PlayTubeEffects(Transform tube)
        {

            LaunchAudio.Report(this, tube);

            if (launchParticles != null)
            {
                if (!launchParticles.isPlaying) launchParticles.Play();

                Vector3 at = launchParticles.transform.InverseTransformPoint(tube.position);

                LogFlashEmit(tube, at);

                launchParticles.Emit(new ParticleSystem.EmitParams { position = at },
                                     LaunchFlashParticles);
            }
            if (launchSound != null)
            {
                launchSound.pitch = UnityEngine.Random.Range(0.95f, 1.05f);
                launchSound.Play();
            }
        }

        private bool _reloadInProgress;
        private int _reloadingAmmo;
        private float _startedReloadTime;

        public override void Rearm(int ammoToRearm, WeaponStation station)
        {
            weaponStation = station;
            _reloadingAmmo += ammoToRearm;
            if (!_reloadInProgress) StartCoroutine(Reload());
        }

        private IEnumerator Reload()
        {
            _startedReloadTime = Time.timeSinceLevelLoad;
            _reloadInProgress = true;

            while (Time.timeSinceLevelLoad < Mathf.Max(_startedReloadTime, lastFired + reloadTime))
            {
                weaponStation?.Updated();
                yield return new WaitForSeconds(1f);
            }

            ammo += _reloadingAmmo;
            _currentCell = 0;
            _reloadingAmmo = 0;
            weaponStation?.AccountAmmo();
            weaponStation?.Updated();
            _reloadInProgress = false;
            ReportReloading(false);
        }

        public override int GetAmmoLoaded() => ammo;
        public override int GetAmmoTotal() => ammo + _reloadingAmmo;
        public override int GetFullAmmo() => _maxAmmo;

        public override float GetReloadProgress()
            => _reloadInProgress
                ? (Time.timeSinceLevelLoad - Mathf.Max(_startedReloadTime, lastFired)) / reloadTime
                : 0f;
    }
}
