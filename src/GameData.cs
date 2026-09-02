using System.Reflection;
using HarmonyLib;

namespace RocketPod
{

    internal static class GameData
    {
        private static bool _resolved;
        private static object? _loader;
        private static PropertyInfo? _isLoaded;
        private static bool _warned;

        internal static Encyclopedia? EncyclopediaOrNull()
        {
            try
            {
                if (!Resolve())
                {

                    return Encyclopedia.i;
                }

                if (_isLoaded!.GetValue(_loader) is not true) return null;

                return Encyclopedia.i;
            }
            catch
            {

                return null;
            }
        }

        private static bool Resolve()
        {
            if (_resolved) return _loader != null && _isLoaded != null;
            _resolved = true;

            FieldInfo? fLoader = AccessTools.Field(typeof(Encyclopedia), "loader");
            _loader = fLoader?.GetValue(null);

            if (_loader != null)
                _isLoaded = AccessTools.Property(_loader.GetType(), "IsLoaded");

            if ((_loader == null || _isLoaded == null) && !_warned)
            {
                _warned = true;

                Plugin.Log.LogWarning(
                    "[Tenpin] Encyclopedia's loader is unreachable, so early lookups " +
                    "read it directly.");
            }

            return _loader != null && _isLoaded != null;
        }
    }
}
