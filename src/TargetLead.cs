using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace RocketPod
{

    internal static class TargetLead
    {
        private static readonly FieldInfo? PathfinderField =
            AccessTools.Field(typeof(GroundVehicle), "pathfinder");
        private static readonly FieldInfo? WaypointsField =
            AccessTools.Field(typeof(PathfindingAgent), "waypoints");

        private const float MovingSpeed = 1f;

        internal static Vector3 PredictPosition(Unit target, float seconds, out bool routed)
        {
            routed = false;

            Vector3 now = target.GlobalPosition().AsVector3();
            Vector3 velocity = target.rb != null ? target.rb.velocity : Vector3.zero;

            if (seconds <= 0f || velocity.sqrMagnitude < MovingSpeed * MovingSpeed) return now;

            List<GlobalPosition>? route = RouteOf(target);
            if (route == null || route.Count == 0) return now + velocity * seconds;

            float remaining = velocity.magnitude * seconds;
            Vector3 at = now;

            foreach (GlobalPosition wp in route)
            {
                Vector3 next = wp.AsVector3();
                float leg = Vector3.Distance(at, next);

                if (leg <= 0.01f) continue;
                if (leg >= remaining)
                {
                    routed = true;
                    return Vector3.Lerp(at, next, remaining / leg);
                }

                remaining -= leg;
                at = next;
            }

            routed = true;
            return at;
        }

        internal static bool IsMoving(Unit? target) =>
            target != null && target.rb != null &&
            target.rb.velocity.sqrMagnitude >= MovingSpeed * MovingSpeed;

        private static List<GlobalPosition>? RouteOf(Unit target)
        {
            if (PathfinderField == null || WaypointsField == null) return null;
            if (!(target is GroundVehicle vehicle)) return null;

            object? agent = PathfinderField.GetValue(vehicle);
            if (agent == null) return null;

            return WaypointsField.GetValue(agent) as List<GlobalPosition>;
        }
    }
}
