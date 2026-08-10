using UnityEngine;

namespace RocketPod
{

    internal static class LowSpeedLaunch
    {

        internal const float ReferenceSpeed = 170f;

        private const float SpeedFloor = 20f;

        private const float MaxRelief = 6f;

        internal static float SpeedFraction(float speedMS) =>
            Mathf.Clamp01(Mathf.Max(speedMS, 0f) / ReferenceSpeed);

        internal static float BudgetRelief(float speedMS)
        {
            if (speedMS >= ReferenceSpeed) return 1f;
            float s = Mathf.Max(speedMS, SpeedFloor);
            return Mathf.Clamp(ReferenceSpeed / s, 1f, MaxRelief);
        }

        internal static float PipperLerpRate(float speedMS)
        {
            const float StockRate = 5f;
            const float FloorRate = 1.2f;
            return Mathf.Max(FloorRate, StockRate * SpeedFraction(speedMS));
        }
    }
}
