using System;
using UnityEngine;

namespace RocketPod
{
    internal static class PluginInfo
    {

        internal const string GUID = "com.tenpin";
        internal const string Name = "AGR-31 Tenpin";

        internal const string Version = "1.2.3";

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

            internal readonly float RippleInterval;

            internal readonly EmploymentSpec Employment;

            internal WeaponSpec(string roundKey, string weaponName, string unitName,
                                string shortName, bool spins, float mapMarkAspect,
                                float rippleInterval, EmploymentSpec employment)
            {
                RoundKey = roundKey; WeaponName = weaponName;
                UnitName = unitName; ShortName = shortName;
                Spins = spins; MapMarkAspect = mapMarkAspect;
                RippleInterval = rippleInterval;
                Employment = employment;
            }
        }

        internal readonly struct EmploymentSpec
        {

            internal readonly float SalvoNear;
            internal readonly float SalvoFar;

            internal readonly int PodCeiling;

            internal readonly float OverwhelmFactor;

            internal readonly float FullSalvoRange;

            internal readonly float PreferredMinRange;

            internal readonly float GuidanceBudgetMilliradians;

            internal readonly float BurstSeconds;
            internal readonly int BurstsPerApproach;

            internal readonly int TargetsPerPass;

            internal readonly bool Saturation;

            internal EmploymentSpec(float salvoNear, float salvoFar, int podCeiling,
                                    float overwhelmFactor, float fullSalvoRange,
                                    float preferredMinRange,
                                    float guidanceBudgetMilliradians,
                                    float burstSeconds, int burstsPerApproach,
                                    int targetsPerPass, bool saturation)
            {
                SalvoNear = salvoNear; SalvoFar = salvoFar;
                PodCeiling = podCeiling; OverwhelmFactor = overwhelmFactor;
                FullSalvoRange = fullSalvoRange;
                PreferredMinRange = preferredMinRange;
                GuidanceBudgetMilliradians = guidanceBudgetMilliradians;
                BurstSeconds = burstSeconds; BurstsPerApproach = burstsPerApproach;
                TargetsPerPass = targetsPerPass; Saturation = saturation;
            }
        }

        private static readonly EmploymentSpec Employment31 = new EmploymentSpec(
            salvoNear: 12f, salvoFar: 18f, podCeiling: Rounds18,
            overwhelmFactor: 2.0f, fullSalvoRange: 17000f,
            preferredMinRange: 8000f, guidanceBudgetMilliradians: 45f,
            burstSeconds: 2.5f, burstsPerApproach: 3, targetsPerPass: 3,
            saturation: true);

        private static readonly EmploymentSpec Employment51 = new EmploymentSpec(
            salvoNear: 1f, salvoFar: 2f, podCeiling: Rounds51,
            overwhelmFactor: 1.5f, fullSalvoRange: 22000f,
            preferredMinRange: 12000f, guidanceBudgetMilliradians: 25f,
            burstSeconds: 1.2f, burstsPerApproach: 2, targetsPerPass: 2,
            saturation: false);

        internal static readonly WeaponSpec[] Weapons =
        {

            new WeaponSpec(MissileKey,   WeaponInfoName,   MissileUnitName,   ShortName,   true,  0.19f,    0.08f,   Employment31),
            new WeaponSpec(MissileKey51, WeaponInfoName51, MissileUnitName51, ShortName51, false, 0.34f,    0.35f,   Employment51),
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

        internal static float RippleIntervalFor(string? mountJsonKey)
        {
            MountSpec? mount = SpecFor(mountJsonKey);
            if (mount == null) return -1f;

            WeaponSpec? weapon = WeaponForRound(mount.Value.RoundKey);
            return weapon?.RippleInterval ?? -1f;
        }

        internal const string BundleName = "tenpin.nobp";
        internal const string MountAssetFragment = "tenpin_weaponmount";
        internal const string MissileDefAssetFragment = "tenpin_missiledefinition";
    }
}
