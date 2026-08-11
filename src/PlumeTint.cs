using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace RocketPod
{

    internal static class PlumeTint
    {

        internal static readonly Color Flame = Color.white;

        internal static readonly Color WasCyan = new Color(0.353f, 0.847f, 1f);

        internal const float FlameLifetimeSeconds = 0.6f;

        internal static bool IsFlame(ParticleSystem ps) =>
            ps != null && ps.main.startLifetime.constantMax <= FlameLifetimeSeconds;

        private static bool _described;

        private static bool Enabled =>
            Flame.r < 0.999f || Flame.g < 0.999f || Flame.b < 0.999f;

        internal static void Apply(IEnumerable<ParticleSystem> systems)
        {
            if (!Enabled) return;

            foreach (ParticleSystem ps in systems)
            {
                if (ps == null) continue;
                if (ps.main.startLifetime.constantMax > FlameLifetimeSeconds) continue;

                ParticleSystem.MainModule main = ps.main;
                main.startColor = Multiply(main.startColor, Flame);
            }
        }

        private static ParticleSystem.MinMaxGradient Multiply(
            ParticleSystem.MinMaxGradient g, Color by)
        {
            switch (g.mode)
            {
                case ParticleSystemGradientMode.Color:
                    return new ParticleSystem.MinMaxGradient(g.color * by);
                case ParticleSystemGradientMode.TwoColors:
                    return new ParticleSystem.MinMaxGradient(g.colorMin * by, g.colorMax * by);
                default:
                    return g;
            }
        }

        internal static void Describe(string donorKey, IEnumerable<ParticleSystem> systems)
        {
            if (_described) return;
            _described = true;

            List<ParticleSystem> list = systems.Where(p => p != null).ToList();
            if (list.Count == 0) return;

            var sb = new StringBuilder();
            sb.AppendLine($"[Tenpin] -- Borrowed plume from '{donorKey}', {list.Count} system(s) --");

            foreach (ParticleSystem ps in list)
            {
                ParticleSystem.MainModule main = ps.main;
                float life = main.startLifetime.constantMax;
                string kind = life > FlameLifetimeSeconds ? "smoke" : "FLAME";

                sb.AppendLine(
                    $"[Tenpin]   '{ps.name}' {kind} life={life:0.##}s " +
                    $"size={main.startSize.constantMax:0.##} " +
                    $"colour={ColourOf(main.startColor)}");
            }

            sb.AppendLine(
                "[Tenpin]   Only the FLAME systems are tinted. Set PlumeTint.Flame and rebuild - " +
                "it multiplies, so the donor's own fade and alpha survive. No re-export needed.");

            Plugin.Log.LogInfo(sb.ToString().TrimEnd());
        }

        private static string ColourOf(ParticleSystem.MinMaxGradient g) => g.mode switch
        {
            ParticleSystemGradientMode.Color => Hex(g.color),
            ParticleSystemGradientMode.TwoColors => $"{Hex(g.colorMin)}..{Hex(g.colorMax)}",
            _ => $"{g.mode} (a ramp, left alone by the tint)",
        };

        private static string Hex(Color c) =>
            $"#{ColorUtility.ToHtmlStringRGB(c)} a={c.a:0.##}";
    }
}
