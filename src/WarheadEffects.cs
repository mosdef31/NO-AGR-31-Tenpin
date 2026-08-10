using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace RocketPod
{

    internal static class WarheadEffects
    {
        private const BindingFlags Inst =
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        private static readonly string[] EffectFields =
        {
            "terrainEffect",
            "armorEffect",
            "underwaterEffect",
            "airEffect",
            "waterSurfaceEffect",
            "fizzleEffect",
        };

        private static bool _ran;

        internal static void RunOnce()
        {
            if (_ran) return;
            _ran = true;

            try
            {
                Apply();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[Tenpin] Warhead effect borrowing failed: {ex.Message}");
            }
        }

        private static void Apply()
        {
            MissileDefinition? def = EncyclopediaRegistration.ResolvedMissile;
            GameObject? ourPrefab = def != null ? def.unitPrefab : null;
            if (ourPrefab == null)
            {
                Plugin.Log.LogWarning(
                    "[Tenpin] Warhead effects: our rocket prefab is not resolved yet, so nothing " +
                    "to fill. This is expected if the bundle failed to load.");
                return;
            }

            var ourMissile = ourPrefab.GetComponent<Missile>();
            if (ourMissile == null)
            {
                Plugin.Log.LogWarning("[Tenpin] Warhead effects: the rocket prefab has no Missile.");
                return;
            }

            FieldInfo? fWarhead = typeof(Missile).GetField("warhead", Inst);
            if (fWarhead == null)
            {
                Plugin.Log.LogError(
                    "[Tenpin] Warhead effects: Missile has no 'warhead' field in this build. " +
                    "Re-check the decompile.");
                return;
            }

            object? ourWarhead = fWarhead.GetValue(ourMissile);
            if (ourWarhead == null)
            {
                Plugin.Log.LogWarning("[Tenpin] Warhead effects: our warhead is null.");
                return;
            }

            Type warheadType = ourWarhead.GetType();
            var fields = EffectFields
                .Select(n => warheadType.GetField(n, Inst))
                .Where(f => f != null)
                .Cast<FieldInfo>()
                .ToArray();

            if (fields.Length == 0)
            {
                Plugin.Log.LogError("[Tenpin] Warhead effects: none of the effect fields resolved.");
                return;
            }

            string[] missing = fields
                .Where(f => f.GetValue(ourWarhead) as GameObject == null)
                .Select(f => f.Name)
                .ToArray();

            if (missing.Length == 0)
            {
                Plugin.Log.LogInfo(
                    "[Tenpin] Warhead effects: all set in the bundle, nothing borrowed.");
                return;
            }

            object? donorWarhead = FindDonor(fWarhead, fields, out string donorName);
            if (donorWarhead == null)
            {
                Plugin.Log.LogError(
                    "[Tenpin] Warhead effects: could not find a stock rocket to borrow from, and " +
                    $"{missing.Length} field(s) are unset ({string.Join(", ", missing)}). " +
                    "terrainEffect, armorEffect and underwaterEffect are NOT null-checked by " +
                    "Missile+Warhead.Detonate, so the rocket will throw on impact and do nothing. " +
                    "Assign them in Unity.");
                return;
            }

            int filled = 0;
            foreach (FieldInfo f in fields)
            {
                if (f.GetValue(ourWarhead) as GameObject != null) continue;
                var donated = f.GetValue(donorWarhead) as GameObject;
                if (donated == null) continue;
                f.SetValue(ourWarhead, donated);
                filled++;
            }

            fWarhead.SetValue(ourMissile, ourWarhead);

            Plugin.Log.LogInfo(
                $"[Tenpin] Warhead effects: filled {filled} of {missing.Length} unset " +
                $"({string.Join(", ", missing)}) from '{donorName}'. Assign real ones in Unity " +
                "when convenient - borrowed effects are tuned for the donor's yield, not ours.");

            string[] stillNull = fields
                .Take(3)
                .Where(f => f.GetValue(ourWarhead) as GameObject == null)
                .Select(f => f.Name)
                .ToArray();
            if (stillNull.Length > 0)
            {
                Plugin.Log.LogError(
                    $"[Tenpin] STILL UNSET after borrowing: {string.Join(", ", stillNull)}. These " +
                    "are not null-checked by the game, so the rocket will throw on impact and do " +
                    "no damage. Assign them in Unity.");
            }
        }

        private static object? FindDonor(FieldInfo fWarhead, FieldInfo[] fields, out string donorName)
        {
            donorName = "(none)";
            Encyclopedia? enc = null;
            try { enc = Encyclopedia.i; } catch {  }
            if (enc?.missiles == null) return null;

            object? best = null;
            int bestScore = -1;

            foreach (MissileDefinition d in enc.missiles)
            {
                if (d == null || d.unitPrefab == null) continue;
                if (d == EncyclopediaRegistration.ResolvedMissile) continue;

                var m = d.unitPrefab.GetComponent<Missile>();
                if (m == null) continue;

                object? w = fWarhead.GetValue(m);
                if (w == null) continue;

                int score = fields.Count(f => f.GetValue(w) as GameObject != null);
                if (score == 0) continue;

                string name = d.unitName ?? string.Empty;
                if (name.IndexOf("AGR", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    score += 10;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    best = w;
                    donorName = string.IsNullOrEmpty(name) ? d.name : name;
                }
            }

            return best;
        }
    }
}
