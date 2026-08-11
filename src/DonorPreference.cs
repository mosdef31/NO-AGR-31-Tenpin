using System;
using System.Collections.Generic;
using System.Linq;

namespace RocketPod
{

    internal static class DonorPreference
    {

        internal static string[] Parse(string preference) =>
            string.IsNullOrWhiteSpace(preference)
                ? Array.Empty<string>()
                : preference.Split(',')
                            .Select(s => s.Trim())
                            .Where(s => s.Length > 0)
                            .ToArray();

        internal static bool TryBest<T>(
            IEnumerable<T> candidates,
            Func<T, string> keyOf,
            string preference,
            out T match,
            out string why)
        {
            match = default!;
            why = "no preference matched";

            List<T> list = candidates.ToList();
            if (list.Count == 0) return false;

            foreach (string fragment in Parse(preference))
            {
                foreach (T candidate in list)
                {
                    string key = keyOf(candidate) ?? string.Empty;
                    if (key.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) < 0) continue;

                    match = candidate;
                    why = $"matched preference '{fragment}'";
                    return true;
                }
            }

            return false;
        }
    }
}
