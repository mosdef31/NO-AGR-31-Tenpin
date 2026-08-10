using UnityEngine;

namespace RocketPod
{

    internal static class AngularDispersion
    {
        private static bool _loggedApplied;
        private static bool _loggedZeroCEP;

        internal static void Apply(InertialSeekerShell seeker, float slantRange)
        {
            if (!Plugin.AngularDispersion.Value) return;
            if (slantRange < 0f) return;

            if (SeekerFields.CEP!.GetValue(seeker) is not float baseCEP) return;
            if (SeekerFields.Error!.GetValue(seeker) is not Vector3 error) return;

            if (baseCEP <= 0f)
            {
                if (!_loggedZeroCEP)
                {
                    _loggedZeroCEP = true;
                    Plugin.Log.LogWarning(
                        "[Tenpin] InertialSeekerShell.CEP is 0 on the flying prefab, so there is " +
                        "no dispersion to scale and angular dispersion does nothing. Set it in " +
                        "Unity - the value itself no longer matters, but it must be non-zero for " +
                        "the offset vector to exist.");
                }
                return;
            }

            float effectiveCEP = slantRange * (Plugin.DispersionMilliradians.Value / 1000f);

            float floor = Plugin.MinDispersionMeters.Value;
            float ceiling = Plugin.MaxDispersionMeters.Value;
            if (floor > 0f) effectiveCEP = Mathf.Max(effectiveCEP, floor);
            if (ceiling > 0f) effectiveCEP = Mathf.Min(effectiveCEP, ceiling);

            SeekerFields.Error.SetValue(seeker, error * (effectiveCEP / baseCEP));

            if (!_loggedApplied)
            {
                _loggedApplied = true;
                Plugin.Log.LogInfo(
                    $"[Tenpin] Angular dispersion active at " +
                    $"{Plugin.DispersionMilliradians.Value:0.##} mrad. First round: slant range " +
                    $"{slantRange:0} m, CEP {baseCEP:0.#} -> {effectiveCEP:0.#} m. Logged once " +
                    "per session.");
            }
        }
    }
}
