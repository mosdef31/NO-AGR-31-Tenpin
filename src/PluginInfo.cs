using System;
using UnityEngine;

namespace RocketPod
{
    internal static class PluginInfo
    {

        internal const string GUID = "com.tenpin";
        internal const string Name = "AGR-31 Tenpin";

        internal const string Version = "1.2.0";

        internal const string MountKey = "AGR31_Tenpin";
        internal const string MountKey19 = "AGR31_Tenpin_19";

        internal const string MountKey18 = "AGR31_Tenpin_18";
        internal const string MissileKey = "AGR31_Tenpin_Rocket";
        internal const string WeaponInfoName = "AGR-31 Tenpin";
        internal const string ShortName = "AGR-31";
        internal const string MissileUnitName = "AGR-31 Tenpin";

        internal const string MountKey51 = "AGR51_Strike";
        internal const string MissileKey51 = "AGR51_Strike_Rocket";
        internal const string WeaponInfoName51 = "AGR-51 Strike";
        internal const string ShortName51 = "AGR-51";
        internal const string MissileUnitName51 = "AGR-51 Strike";

        internal const int Rounds51 = 4;

        internal readonly struct WeaponSpec
        {

            internal readonly string RoundKey;

            internal readonly string WeaponName;

            internal readonly string UnitName;
            internal readonly string ShortName;

            internal readonly bool Spins;

            internal readonly float MapMarkAspect;

            internal WeaponSpec(string roundKey, string weaponName, string unitName,
                                string shortName, bool spins, float mapMarkAspect)
            {
                RoundKey = roundKey; WeaponName = weaponName;
                UnitName = unitName; ShortName = shortName;
                Spins = spins; MapMarkAspect = mapMarkAspect;
            }
        }

        internal static readonly WeaponSpec[] Weapons =
        {

            new WeaponSpec(MissileKey,   WeaponInfoName,   MissileUnitName,   ShortName,   true,  0.19f),
            new WeaponSpec(MissileKey51, WeaponInfoName51, MissileUnitName51, ShortName51, false, 0.34f),
        };

        internal static bool IsOurRound(string? jsonKey)
        {
            if (jsonKey == null) return false;
            foreach (WeaponSpec w in Weapons)
                if (w.RoundKey == jsonKey) return true;
            return false;
        }

        internal static bool IsOurWeaponName(string? weaponName)
        {
            if (weaponName == null) return false;
            foreach (WeaponSpec w in Weapons)
                if (w.WeaponName == weaponName) return true;
            return false;
        }

        internal static bool IsOurUnitName(string? unitName)
        {
            if (unitName == null) return false;
            foreach (WeaponSpec w in Weapons)
                if (w.UnitName == unitName) return true;
            return false;
        }

        internal static WeaponSpec? WeaponForRound(string? jsonKey)
        {
            if (jsonKey == null) return null;
            foreach (WeaponSpec w in Weapons)
                if (w.RoundKey == jsonKey) return w;
            return null;
        }

        internal static WeaponSpec? WeaponForUnitName(string? unitName)
        {
            if (unitName == null) return null;
            foreach (WeaponSpec w in Weapons)
                if (w.UnitName == unitName) return w;
            return null;
        }

        internal static WeaponSpec? WeaponForName(string? weaponName)
        {
            if (weaponName == null) return null;
            foreach (WeaponSpec w in Weapons)
                if (w.WeaponName == weaponName) return w;
            return null;
        }

        internal const int Rounds = 7;

        internal const int Rounds19 = 19;

        internal const int Rounds18 = 18;

        internal readonly struct MountSpec
        {
            internal readonly string JsonKey;
            internal readonly int Rounds;
            internal readonly float FlushOffset;
            internal readonly string Shape;

            internal readonly string RoundKey;

            internal readonly bool HexFamily;

            internal MountSpec(string jsonKey, int rounds, float flush, string shape,
                               string roundKey, bool hexFamily)
            {
                JsonKey = jsonKey; Rounds = rounds; FlushOffset = flush; Shape = shape;
                RoundKey = roundKey; HexFamily = hexFamily;
            }
        }

        internal static readonly MountSpec[] Mounts =
        {
            new MountSpec(MountKey,   Rounds,   -0.3332f / 2f, "7-tube hex, 333 mm across flats",   MissileKey,   true),
            new MountSpec(MountKey19, Rounds19, -0.5141f / 2f, "19-tube hex, 514 mm across flats",  MissileKey,   true),
            new MountSpec(MountKey18, Rounds18, -0.570f / 2f,  "18-tube fast-jet drum, 570 mm diameter", MissileKey, false),

            new MountSpec(MountKey51, Rounds51, -0.406f / 2f,  "4-tube square, 406 mm tall x 357 mm wide", MissileKey51, false),
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
