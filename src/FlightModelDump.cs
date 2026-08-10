using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace RocketPod
{

    internal static class FlightModelDump
    {
        private const BindingFlags Inst =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static bool _done;

        private readonly struct Row
        {
            public readonly string Key;
            public readonly float Mass;
            public readonly float FinArea;
            public readonly float Cd0;
            public readonly int DragKeys;
            public readonly float LiftAt10;
            public readonly int LiftKeys;
            public readonly float Supersonic;
            public readonly float Torque;
            public readonly Vector3 PID;
            public readonly float GLimit;

            public Row(string key, float mass, float finArea, float cd0, int dragKeys,
                       float liftAt10, int liftKeys, float supersonic, float torque,
                       Vector3 pid, float gLimit)
            {
                Key = key; Mass = mass; FinArea = finArea; Cd0 = cd0; DragKeys = dragKeys;
                LiftAt10 = liftAt10; LiftKeys = liftKeys; Supersonic = supersonic;
                Torque = torque; PID = pid; GLimit = gLimit;
            }

            public float DragPerMass => Mass > 0f ? Cd0 * FinArea / Mass : 0f;
        }

        internal static void RunOnce()
        {
            if (_done) return;
            _done = true;

            try
            {
                Encyclopedia? enc = null;
                try { enc = Encyclopedia.i; } catch {  }
                if (enc?.missiles == null) return;

                var rows = new List<Row>();
                Row? ours = null;

                foreach (MissileDefinition md in enc.missiles)
                {
                    if (md == null || md.unitPrefab == null) continue;
                    Missile? m = md.unitPrefab.GetComponent<Missile>();
                    if (m == null) continue;

                    Row row = Read(md.jsonKey ?? md.name, m);
                    if (md.jsonKey == PluginInfo.MissileKey) ours = row;
                    else rows.Add(row);
                }

                if (rows.Count == 0) return;

                Plugin.Log.LogInfo(
                    "[Tenpin] -- Flight models, stock rounds vs ours. Copy from a comparable " +
                    "round rather than inventing values. --");
                Plugin.Log.LogInfo(
                    "[Tenpin]   dragPerMass = Cd0 * finArea / mass, which is what actually sets " +
                    "how fast a round bleeds speed. A lighter, smaller round should sit ABOVE the " +
                    "heavy ones here, not below.");

                foreach (Row r in rows.OrderByDescending(r => r.DragPerMass).Take(24))
                    Plugin.Log.LogInfo(Format(r));

                DumpCurveKeys(enc);

                if (ours != null)
                {
                    Plugin.Log.LogInfo("[Tenpin]   ---- ours ----");
                    Plugin.Log.LogInfo(Format(ours.Value));

                    float median = rows.OrderBy(r => r.DragPerMass)
                                       .ElementAt(rows.Count / 2).DragPerMass;
                    Plugin.Log.LogInfo(
                        $"[Tenpin]   Stock median dragPerMass is {median:0.000e+0}; ours is " +
                        $"{ours.Value.DragPerMass:0.000e+0}. At our finArea " +
                        $"{ours.Value.FinArea:0.###} m2 and mass {ours.Value.Mass:0.#} kg, matching " +
                        $"that median needs Cd0 = {(ours.Value.FinArea > 0f && ours.Value.Mass > 0f ? median * ours.Value.Mass / ours.Value.FinArea : 0f):0.###}.");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[Tenpin] Flight model dump failed: {ex.Message}");
            }
        }

        private static void DumpCurveKeys(Encyclopedia enc)
        {
            string[] wanted = (Plugin.FlightModelDonors.Value ?? string.Empty)
                .Split(',')
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToArray();
            if (wanted.Length == 0) return;

            foreach (MissileDefinition md in enc.missiles)
            {
                if (md?.unitPrefab == null) continue;
                string key = md.jsonKey ?? md.name;
                if (!wanted.Any(w => string.Equals(w, key, StringComparison.OrdinalIgnoreCase)))
                    continue;

                Missile? m = md.unitPrefab.GetComponent<Missile>();
                if (m == null) continue;

                Plugin.Log.LogInfo($"[Tenpin]   ---- curve keys: {key} ----");
                DumpOne(key, "dragCurve",
                        typeof(Missile).GetField("dragCurve", Inst)?.GetValue(m) as AnimationCurve);
                DumpOne(key, "liftCurve",
                        typeof(Missile).GetField("liftCurve", Inst)?.GetValue(m) as AnimationCurve);
            }
        }

        private static void DumpOne(string key, string name, AnimationCurve? curve)
        {
            if (curve == null || curve.length == 0)
            {
                Plugin.Log.LogInfo($"[Tenpin]     {key}.{name}: EMPTY");
                return;
            }

            Plugin.Log.LogInfo(
                $"[Tenpin]     {key}.{name}: {curve.length} key(s), " +
                $"preWrap={curve.preWrapMode} postWrap={curve.postWrapMode}");
            for (int i = 0; i < curve.length; i++)
            {
                Keyframe k = curve[i];
                Plugin.Log.LogInfo(
                    $"[Tenpin]       key{i}: time={k.time:0.#####} ({k.time * Mathf.Rad2Deg:0.##} deg) " +
                    $"value={k.value:0.#####} inTangent={k.inTangent:0.#####} " +
                    $"outTangent={k.outTangent:0.#####} " +
                    $"inWeight={k.inWeight:0.###} outWeight={k.outWeight:0.###} " +
                    $"weightedMode={k.weightedMode}");
            }
        }

        private static string Format(Row r) =>
            $"[Tenpin]   {r.Key,-28} mass={r.Mass,7:0.#} finArea={r.FinArea,6:0.###} " +
            $"Cd0={r.Cd0,6:0.###}({r.DragKeys}k) lift@10deg={r.LiftAt10,6:0.###}({r.LiftKeys}k) " +
            $"ssDrag={r.Supersonic,5:0.##} torque={r.Torque,7:0.##} " +
            $"PID=({r.PID.x:0.##},{r.PID.y:0.##},{r.PID.z:0.##}) gLimit={r.GLimit,5:0.#} " +
            $"dragPerMass={r.DragPerMass:0.000e+0}";

        private static Row Read(string key, Missile m)
        {
            var drag = typeof(Missile).GetField("dragCurve", Inst)?.GetValue(m) as AnimationCurve;
            var lift = typeof(Missile).GetField("liftCurve", Inst)?.GetValue(m) as AnimationCurve;

            float mass = Float(m, "mass");
            if (mass <= 0f)
            {
                Rigidbody rb = m.GetComponent<Rigidbody>();
                if (rb != null) mass = rb.mass;
            }

            float finArea = Float(m, "finArea");
            float supersonic = Float(m, "supersonicDrag");
            float torque = Float(m, "torque");
            float gLimit = Float(m, "gLimit");

            Vector3 pid = Vector3.zero;
            object? factors = typeof(Missile).GetField("PIDFactors", Inst)?.GetValue(m);
            if (factors != null &&
                factors.GetType().GetField("PID", Inst)?.GetValue(factors) is Vector3 p)
            {
                pid = p;
            }

            int dragKeys = drag?.length ?? 0;
            int liftKeys = lift?.length ?? 0;

            float cd0 = dragKeys > 0 ? drag!.Evaluate(0f) : 0f;
            float liftAt10 = liftKeys > 0 ? lift!.Evaluate(10f * Mathf.Deg2Rad) : 0f;

            return new Row(key, mass, finArea, cd0, dragKeys, liftAt10, liftKeys,
                           supersonic, torque, pid, gLimit);
        }

        private static float Float(Missile m, string field)
        {
            object? v = typeof(Missile).GetField(field, Inst)?.GetValue(m);
            return v is float f ? f : 0f;
        }
    }
}
