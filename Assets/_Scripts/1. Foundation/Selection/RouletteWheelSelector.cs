using System;
using System.Collections.Generic;

namespace Foundation
{
    public static class RouletteWheelSelector
    {
        /// <summary>
        /// Selects one outcome from a weighted list.
        /// </summary>
        /// <typeparam name="T">Any type: enum, string, int, object, etc.</typeparam>
        /// <param name="options">
        ///   List of (outcome, weight) tuples.
        ///   Weights must be non-negative; at least one must be positive.
        /// </param>
        public static T Select<T>(IList<(T outcome, float weight)> options)
        {
            if (options == null || options.Count == 0)
                throw new ArgumentException("[RouletteWheel] Options list must not be null or empty.");

            // Step 1: Validate and sum weights
            float totalWeight = 0f;
            foreach (var (_, w) in options)
            {
                if (w < 0f)
                    throw new ArgumentException(
                        $"[RouletteWheel] Negative weight ({w}) detected. All weights must be ≥ 0.");
                totalWeight += w;
            }

            if (totalWeight <= 0f)
                throw new InvalidOperationException(
                    "[RouletteWheel] Total weight must be > 0. At least one option must have a positive weight.");

            // Step 2: Random pick uniformly across the total weight span
            float pick = UnityEngine.Random.Range(0f, totalWeight);

            // Step 3: Walk until the cumulative sum exceeds the pick
            float cumulative = 0f;
            for (int i = 0; i < options.Count; i++)
            {
                cumulative += options[i].weight;
                if (pick < cumulative)
                    return options[i].outcome;
            }

            // Floating-point safety fallback: return last item
            return options[^1].outcome;
        }
    }
}