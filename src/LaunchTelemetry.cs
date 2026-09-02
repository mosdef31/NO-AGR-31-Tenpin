using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace RocketPod
{

    internal static class LaunchTelemetry
    {

        private sealed class PodLedger
        {
            public string Label = "?";
            public int RipplesStarted;
            public int RipplesCompleted;
            public int RoundsRequested;
            public int RoundsSpawned;
            public int RoundsNull;
            public int RoundsAliveAt1s;
            public int RoundsDiedEarly;
            public readonly HashSet<int> TubesUsed = new HashSet<int>();
            public float FirstFireTime = float.NaN;
            public float LastFireTime = float.NaN;
            public int EffectNullParticles;
        }

        private static readonly Dictionary<int, PodLedger> _pods = new Dictionary<int, PodLedger>();

        private static readonly Queue<float> _commandSends = new Queue<float>();
        private static int _commandsTotal;
        private static int _peakCommandsPerSecond;

        private static float _lastCommandSentAt = float.NaN;
        private static bool _awaitingFirstRound;
        private static readonly List<float> _roundTrips = new List<float>();

        private static int _lastServerAmmo = -1;
        private static int _worstAmmoDivergence;

        private static double _launchPathMillis;
        private static double _worstFrameMillis;
        private static double _firstRoundMillis;
        private static double _worstAfterFirstMillis;
        private static int _framesCosted;

        private static readonly HashSet<int> _rolesLogged = new HashSet<int>();

        private static bool Verbose => Plugin.LaunchTrace?.Value ?? false;

        private static float _lastReportAt = -999f;

        private const float ReportInterval = 30f;

        private static PodLedger LedgerFor(TenpinLauncher pod)
        {
            int id = pod.GetInstanceID();
            if (!_pods.TryGetValue(id, out PodLedger? row))
            {
                row = new PodLedger { Label = DescribePod(pod) };
                _pods[id] = row;
            }
            return row;
        }

        private static string DescribePod(TenpinLauncher pod)
        {
            try
            {
                string mount = pod.info != null && !string.IsNullOrEmpty(pod.info.weaponName)
                    ? pod.info.weaponName : "pod";
                int tubes = pod.launchTransforms?.Length ?? 0;
                return $"{mount} x{tubes}";
            }
            catch { return "pod"; }
        }

        internal static void NoteRole(Unit owner)
        {
            if (owner == null) return;
            int id = owner.GetInstanceID();
            if (!_rolesLogged.Add(id)) return;

            string role =
                owner.IsServer && owner.HasAuthority ? "HOST, own aircraft (or singleplayer)"
              : owner.IsServer && !owner.HasAuthority ? "HOST, a remote client's aircraft"
              : !owner.IsServer && owner.HasAuthority ? "CLIENT, own aircraft"
              : "OBSERVER, somebody else's aircraft";

            Plugin.Log.LogInfo(
                $"[Tenpin/net] {owner.name}: IsServer={owner.IsServer} " +
                $"HasAuthority={owner.HasAuthority} LocalSim={owner.LocalSim} - {role}.");
        }

        internal static void CommandSent()
        {
            float now = Time.timeSinceLevelLoad;
            _commandsTotal++;
            _lastCommandSentAt = now;
            _awaitingFirstRound = true;

            _commandSends.Enqueue(now);
            while (_commandSends.Count > 0 && now - _commandSends.Peek() > 1f)
                _commandSends.Dequeue();

            if (_commandSends.Count > _peakCommandsPerSecond)
                _peakCommandsPerSecond = _commandSends.Count;

            if (_commandSends.Count > 15 && Verbose)

                Plugin.Log.LogWarning(
                    $"[Tenpin/net] {_commandSends.Count} launch commands in a second, " +
                    "against a limiter refilling 15.");
        }

        internal static void RoundLeft(TenpinLauncher pod, WeaponStation station,
                                       bool serverSide, int indexInRipple)
        {
            PodLedger row = LedgerFor(pod);
            float now = Time.timeSinceLevelLoad;

            if (float.IsNaN(row.FirstFireTime)) row.FirstFireTime = now;
            row.LastFireTime = now;

            if (serverSide) row.RoundsRequested++;

            if (pod.launchParticles == null) row.EffectNullParticles++;

            if (_awaitingFirstRound && !float.IsNaN(_lastCommandSentAt))
            {
                _roundTrips.Add(now - _lastCommandSentAt);
                _awaitingFirstRound = false;
            }

            if (Verbose)
                Plugin.Log.LogInfo(
                    $"[Tenpin/shot] {row.Label} round {indexInRipple} " +
                    $"tube={(pod.launchTransforms?.Length ?? 0)} " +
                    $"spawns={serverSide} ammo={station?.Ammo ?? -1}");
        }

        private static bool _launchVelocityLogged;

        internal static void LaunchVelocity(Unit owner, Vector3 inherited, Vector3 total)
        {
            if (_launchVelocityLogged) return;
            _launchVelocityLogged = true;

            float ownerSpeed = owner != null && owner.rb != null ? owner.rb.velocity.magnitude : -1f;

            Plugin.Log.LogInfo(
                $"[Tenpin/shot] First round left at {total.magnitude:0.#} m/s, " +
                $"inherited {inherited.magnitude:0.#} m/s.");
            Plugin.Log.LogInfo(
                $"[Tenpin/shot] rb.velocity {ownerSpeed:0.#} m/s, owner " +
                $"'{(owner != null ? owner.unitName : "null")}', " +
                $"IsServer={(owner != null && owner.IsServer)}, " +
                $"HasAuthority={(owner != null && owner.HasAuthority)}.");
        }

        internal static void SpawnResult(TenpinLauncher pod, Missile? spawned)
        {
            PodLedger row = LedgerFor(pod);
            if (spawned == null)
            {
                row.RoundsNull++;

                Plugin.Log.LogWarning(
                    $"[Tenpin/shot] {row.Label}: SpawnMissile returned NULL, so no round " +
                    "exists.");
                return;
            }
            row.RoundsSpawned++;
        }

        internal static void RoundStillAlive(TenpinLauncher pod, bool alive)
        {
            PodLedger row = LedgerFor(pod);
            if (alive) { row.RoundsAliveAt1s++; return; }

            row.RoundsDiedEarly++;
            if (row.RoundsDiedEarly == 1)

                Plugin.Log.LogWarning(
                    $"[Tenpin/shot] {row.Label}: the round existed at spawn and was gone " +
                    "a second later.");
        }

        internal static void TubeUsed(TenpinLauncher pod, int tubeIndex)
            => LedgerFor(pod).TubesUsed.Add(tubeIndex);

        internal static void RippleStarted(TenpinLauncher pod) => LedgerFor(pod).RipplesStarted++;

        private static int _ripplesSinceReport;

        private const int RipplesPerReport = 5;

        internal static void RippleEnded(TenpinLauncher pod, WeaponStation station,
                                         bool serverSide, int roundsFired)
        {
            PodLedger row = LedgerFor(pod);
            row.RipplesCompleted++;

            float now = Time.timeSinceLevelLoad;
            if (now - _lastReportAt >= ReportInterval)
            {
                _lastReportAt = now;
                Report();
            }

            if (Verbose)
                Plugin.Log.LogInfo(
                    $"[Tenpin/shot] {row.Label}: ripple ended after {roundsFired} round(s), " +
                    $"spawns={serverSide}, ammo now {station?.Ammo ?? -1}.");

            if (++_ripplesSinceReport >= RipplesPerReport)
            {
                _ripplesSinceReport = 0;
                Report();
            }
        }

        internal static void NoteServerAmmo(int serverAmmo, int localAmmo)
        {
            _lastServerAmmo = serverAmmo;
            int divergence = Mathf.Abs(serverAmmo - localAmmo);
            if (divergence > _worstAmmoDivergence)
            {
                _worstAmmoDivergence = divergence;
                if (divergence > 1)

                    Plugin.Log.LogWarning(
                        $"[Tenpin/net] Ammo divergence {divergence}: here {localAmmo}, " +
                        $"server {serverAmmo}.");
            }
        }

        internal static void NoteFrameCost(double millis)
        {
            _launchPathMillis += millis;
            if (millis > _worstFrameMillis) _worstFrameMillis = millis;

            if (_framesCosted == 0) _firstRoundMillis = millis;
            else if (millis > _worstAfterFirstMillis) _worstAfterFirstMillis = millis;

            _framesCosted++;
        }

        internal static void Report()
        {
            if (_pods.Count == 0 && _commandsTotal == 0) return;

            var sb = new StringBuilder();
            sb.AppendLine("[Tenpin/ledger] ── launch path ─────────────────────────────");

            int totalRequested = 0, totalSpawned = 0, totalNull = 0, totalDiedEarly = 0;

            foreach (PodLedger row in _pods.Values)
            {
                totalRequested += row.RoundsRequested;
                totalSpawned += row.RoundsSpawned;
                totalNull += row.RoundsNull;

                sb.AppendLine(
                    $"  {row.Label}: ripples {row.RipplesCompleted}/{row.RipplesStarted}, " +
                    $"rounds requested {row.RoundsRequested}, spawned {row.RoundsSpawned}, " +
                    $"NULL {row.RoundsNull}, alive at 1 s {row.RoundsAliveAt1s}, " +
                    $"died early {row.RoundsDiedEarly}, tubes used {row.TubesUsed.Count}");

                totalDiedEarly += row.RoundsDiedEarly;

                if (row.EffectNullParticles > 0)
                    sb.AppendLine(
                        $"      launch flash was NULL at FIRE time on {row.EffectNullParticles} round(s). " +
                        "A bind-time check passing does not mean it survived: something can " +
                        "re-enable or re-strip the launcher after the borrow.");
            }

            sb.AppendLine(
                $"  commands sent {_commandsTotal}, peak {_peakCommandsPerSecond}/s " +
                "against a 15/s refill and a 45 burst");

            if (_roundTrips.Count > 0)
            {
                float sum = 0f, worst = 0f;
                foreach (float t in _roundTrips) { sum += t; if (t > worst) worst = t; }
                sb.AppendLine(
                    $"  command round-trip: mean {sum / _roundTrips.Count * 1000f:0} ms, " +
                    $"worst {worst * 1000f:0} ms over {_roundTrips.Count} sample(s)");
            }

            if (_worstAmmoDivergence > 0)
                sb.AppendLine($"  worst ammo divergence {_worstAmmoDivergence} (last server figure {_lastServerAmmo})");

            if (_worstFrameMillis > 0)
            {
                sb.AppendLine(
                    $"  launch path cost: {_launchPathMillis:0.0} ms total over {_framesCosted} " +
                    $"round(s), worst single frame {_worstFrameMillis:0.00} ms");
                sb.AppendLine(
                    $"    first round of the session {_firstRoundMillis:0.00} ms " +
                    "(prefab and plume load), " +
                    (_framesCosted > 1
                        ? $"worst after that {_worstAfterFirstMillis:0.00} ms"
                        : "no round after it to compare"));
            }

            if (totalRequested == 0)
                sb.AppendLine("  VERDICT: this machine never had spawn authority. " +
                              "Rounds are the server's to make; nothing here is wrong.");
            else if (totalNull > 0)
                sb.AppendLine($"  VERDICT: {totalNull} of {totalRequested} spawns RETURNED NULL. " +
                              "The rounds were asked for and the game refused to make them. " +
                              "This is a spawn fault, not a networking one.");
            else if (totalDiedEarly > 0 && totalDiedEarly >= totalSpawned / 2)
                sb.AppendLine($"  VERDICT: rounds ARE being created ({totalSpawned}/{totalRequested}) " +
                              $"and {totalDiedEarly} of them were destroyed within a second of " +
                              "leaving the tube. The launch path is working; something downstream " +
                              "is killing the round.");
            else if (totalSpawned == totalRequested)
                sb.AppendLine($"  VERDICT: every requested round was created ({totalSpawned}/{totalRequested}).");
            else
                sb.AppendLine($"  VERDICT: {totalSpawned} of {totalRequested} requested rounds were " +
                              "created and the rest never reached the spawn call at all - " +
                              "look at the ripple counts above, not at the network.");

            LauncherSwap.LogSummary();
            Plugin.Log.LogInfo(sb.ToString());
        }

    }
}
