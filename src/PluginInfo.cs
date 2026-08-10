namespace RocketPod
{
    internal static class PluginInfo
    {

        internal const string GUID = "com.tenpin";
        internal const string Name = "AGR-31 Tenpin";
        internal const string Version = "0.9.0";

        internal const string MountKey = "AGR31_Tenpin";
        internal const string MountKey19 = "AGR31_Tenpin_19";
        internal const string MissileKey = "AGR31_Tenpin_Rocket";
        internal const string WeaponInfoName = "AGR-31 Tenpin";
        internal const string ShortName = "AGR-31";
        internal const string MissileUnitName = "AGR-31 Tenpin";

        internal const int Rounds = 7;

        internal const int Rounds19 = 19;

        internal static bool IsOurMount(string? jsonKey) =>
            jsonKey == MountKey || jsonKey == MountKey19;

        internal static int RoundsFor(string? jsonKey) =>
            jsonKey == MountKey ? Rounds :
            jsonKey == MountKey19 ? Rounds19 : -1;

        internal const string BundleName = "tenpin.nobp";
        internal const string MountAssetFragment = "tenpin_weaponmount";
        internal const string MissileDefAssetFragment = "tenpin_missiledefinition";
    }
}
