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
            int done = 0;
            foreach (MissileDefinition d in EncyclopediaRegistration.ResolvedMissiles)
            {
                if (d == null) continue;
                if (ApplyTo(d)) done++;
            }

            if (done == 0)
                Plugin.Log.LogWarning(
                    "[Tenpin] Warhead effects: no rocket was filled. Every round in this " +
                    "mod will fizzle rather than explode, and one that detonates below " +
                    "sea level will throw inside the engine.");
        }

        private static bool ApplyTo(MissileDefinition def)
        {
            GameObject? ourPrefab = def.unitPrefab;
            if (ourPrefab == null)
            {
                Plugin.Log.LogWarning(
                    "[Tenpin] Warhead effects: our rocket prefab is not resolved yet, so nothing " +
                    "to fill. This is expected if the bundle failed to load.");
                return false;
            }

            var ourMissile = ourPrefab.GetComponent<Missile>();
            if (ourMissile == null)
            {
                Plugin.Log.LogWarning("[Tenpin] Warhead effects: the rocket prefab has no Missile.");
                return false;
            }

            FieldInfo? fWarhead = typeof(Missile).GetField("warhead", Inst);
            if (fWarhead == null)
            {
                Plugin.Log.LogError(
                    "[Tenpin] Warhead effects: Missile has no 'warhead' field in this build. " +
                    "Re-check the decompile.");
                return false;
            }

            object? ourWarhead = fWarhead.GetValue(ourMissile);
            if (ourWarhead == null)
            {
                Plugin.Log.LogWarning("[Tenpin] Warhead effects: our warhead is null.");
                return false;
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
                return false;
            }

            string[] missing = fields
                .Where(f => f.GetValue(ourWarhead) as GameObject == null)
                .Select(f => f.Name)
                .ToArray();

            if (missing.Length == 0)
            {
                Plugin.Log.LogInfo(
                    $"[Tenpin] Warhead effects on '{def.jsonKey}': all set in the bundle, " +
                    "nothing borrowed.");
                return false;
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
                return false;
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
                $"[Tenpin] Warhead effects on '{def.jsonKey}': filled {filled} of " +
                $"{missing.Length} unset ({string.Join(", ", missing)}) from '{donorName}'. " +
                "Assign real ones in Unity when convenient - borrowed effects are tuned " +
                "for the donor's yield, not ours.");

            string[] stillNull = fields
                .Take(3)
                .Where(f => f.GetValue(ourWarhead) as GameObject == null)
                .Select(f => f.Name)
                .ToArray();
            if (stillNull.Length > 0)
            {
                Plugin.Log.LogError(
                    $"[Tenpin] '{def.jsonKey}' STILL UNSET after borrowing: " +
                    $"{string.Join(", ", stillNull)}. These are not null-checked by the game, " +
                    "so the rocket will throw on impact and do no damage. Assign them in Unity.");
                return false;
            }

            return true;
        }

        private static object? FindDonor(FieldInfo fWarhead, FieldInfo[] fields, out string donorName)
        {
            donorName = "(none)";
            Encyclopedia? enc = GameData.EncyclopediaOrNull();
            if (enc?.missiles == null) return null;

            var usable = new List<Candidate>();

            foreach (MissileDefinition d in enc.missiles)
            {
                if (d == null || d.unitPrefab == null) continue;
                if (d == EncyclopediaRegistration.ResolvedMissile) continue;

                var m = d.unitPrefab.GetComponent<Missile>();
                if (m == null) continue;

                object? w = fWarhead.GetValue(m);
                if (w == null) continue;

                int set = fields.Count(f => f.GetValue(w) as GameObject != null);
                if (set == 0) continue;

                usable.Add(new Candidate(d, w, set));
            }

            if (usable.Count == 0) return null;

            if (DonorPreference.TryBest(usable, c => c.MatchKey,
                                        Plugin.WarheadEffectDonor.Value,
                                        out Candidate preferred, out string why))
            {
                donorName = $"{preferred.Name} ({why})";
                return preferred.Warhead;
            }

            Candidate fallback = usable
                .OrderByDescending(c => c.Set +
                    (c.MatchKey.IndexOf("AGR", StringComparison.OrdinalIgnoreCase) >= 0 ? 10 : 0))
                .First();

            Plugin.Log.LogWarning(
                $"[Tenpin] No warhead effect donor matched any of " +
                $"'{Plugin.WarheadEffectDonor.Value}', so '{fallback.Name}' was chosen " +
                "automatically. That is the old behaviour and it is what read as a weak " +
                "detonation. Candidate names are the missiles listed by the effect donor scan.");

            donorName = fallback.Name;
            return fallback.Warhead;
        }

        private sealed class Candidate
        {
            internal Candidate(MissileDefinition def, object warhead, int set)
            {
                Def = def;
                Warhead = warhead;
                Set = set;
            }

            internal MissileDefinition Def { get; }
            internal object Warhead { get; }

            internal int Set { get; }

            internal string MatchKey => $"{Def.jsonKey} {Def.unitName}";

            internal string Name =>
                !string.IsNullOrEmpty(Def.unitName) ? Def.unitName :
                !string.IsNullOrEmpty(Def.jsonKey) ? Def.jsonKey : Def.name;
        }
    }
}
