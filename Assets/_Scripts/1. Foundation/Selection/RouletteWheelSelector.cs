using System;
using System.Collections.Generic;

namespace Foundation
{
    /// <summary>
    /// Weighted random selection via the Roulette Wheel (Fitness Proportionate) algorithm.
    ///
    /// Algorithm:
    ///   1. Sum all weights → totalWeight. Weights need not sum to 1.0;
    ///      they express relative probability (e.g., {6, 3, 1} = 60%/30%/10%).
    ///   2. Draw a random float r epsilon [0, totalWeight).
    ///   3. Walk through items, accumulating a running sum.
    ///      Select the first item whose cumulative sum exceeds r.
    ///
    /// Why C# ValueTuples?
    ///   (T outcome, float weight) is a value type — no heap allocation per entry.
    ///   The named fields make call sites self-documenting:
    ///     ("LongRest", 0.5f)  reads naturally as "LongRest with 50% probability."
    ///   Using a List rather than params array lets callers build the set
    ///   dynamically or share it as a static field.
    ///
    /// Complexity: O(n) per selection. Fine for sets of 3-10 outcomes.
    ///   For n > 20, prefer a binary search over a prefix-sum array (O(log n)).
    ///   Fine for current scaling. Adjust later in Final Assignment.
    ///
    /// Floating-point safety:
    ///   After the loop, we return the last item rather than throwing.
    ///   Floating-point rounding can leave r marginally above the final
    ///   cumulative sum; this guard ensures we always return a valid result.
    /// </summary>
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
                        $"[RouletteWheel] Negative weight ({w}) detected. All weights must be positive.");
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