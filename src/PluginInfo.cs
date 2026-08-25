using System;
namespace RocketPod
{
    internal static class PluginInfo
    {

        internal const string GUID = "com.tenpin";
        internal const string Name = "AGR-31 Tenpin";

        internal const string Version = "1.1.0";

        internal const string MountKey = "AGR31_Tenpin";
        internal const string MountKey19 = "AGR31_Tenpin_19";

        internal const string MountKey18 = "AGR31_Tenpin_18";
        internal const string MissileKey = "AGR31_Tenpin_Rocket";
        internal const string WeaponInfoName = "AGR-31 Tenpin";
        internal const string ShortName = "AGR-31";
        internal const string MissileUnitName = "AGR-31 Tenpin";

        internal const int Rounds = 7;

        internal const int Rounds19 = 19;

        internal const int Rounds18 = 18;

        internal readonly struct MountSpec
        {
            internal readonly string JsonKey;
            internal readonly int Rounds;
            internal readonly float FlushOffset;
            internal readonly string Shape;

            internal MountSpec(string jsonKey, int rounds, float flush, string shape)
            {
                JsonKey = jsonKey; Rounds = rounds; FlushOffset = flush; Shape = shape;
            }
        }

        internal static readonly MountSpec[] Mounts =
        {
            new MountSpec(MountKey,   Rounds,   -0.3332f / 2f, "7-tube hex, 333 mm across flats"),
            new MountSpec(MountKey19, Rounds19, -0.5141f / 2f, "19-tube hex, 514 mm across flats"),
            new MountSpec(MountKey18, Rounds18, -0.570f / 2f,  "18-tube fast-jet drum, 570 mm diameter"),
        };

        internal static MountSpec? SpecFor(string? jsonKey)
        {
            if (jsonKey == null) return null;
            foreach (MountSpec m in Mounts)
                if (m.JsonKey == jsonKey) return m;
            return null;
        }

        internal static string MountKeyList =>
            string.Join(", ", Array.ConvertAll(Mounts, m => $"'{m.JsonKey}'"));

        internal static bool IsOurMount(string? jsonKey) => SpecFor(jsonKey) != null;

        internal static int RoundsFor(string? jsonKey) => SpecFor(jsonKey)?.Rounds ?? -1;

        internal const string BundleName = "tenpin.nobp";
        internal const string MountAssetFragment = "tenpin_weaponmount";
        internal const string MissileDefAssetFragment = "tenpin_missiledefinition";
    }
}
