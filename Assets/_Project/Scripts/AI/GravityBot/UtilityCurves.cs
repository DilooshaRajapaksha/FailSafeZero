using UnityEngine;

namespace FailSafeZero.AI.Gravity
{
    /// <summary>
    /// Response curves turn a raw game value (distance, energy...) into a 0..1 "desire".
    /// Every consideration in the Gravity bot goes through one of these, so all scores
    /// are on the same scale and can be multiplied together fairly.
    /// </summary>
    public static class UtilityCurves
    {
        /// <summary>0 at min, 1 at max, straight line between.</summary>
        public static float Linear(float x, float min, float max) => Mathf.Clamp01((x - min) / (max - min));

        /// <summary>1 at min, 0 at max.</summary>
        public static float Inverse(float x, float min, float max) => 1f - Linear(x, min, max);

        /// <summary>Bell curve: 1 at the ideal value, falling off with "width". exp(-((x-ideal)/width)^2)</summary>
        public static float Bell(float x, float ideal, float width)
        {
            float d = (x - ideal) / width;
            return Mathf.Exp(-d * d);
        }

        /// <summary>S-curve: slow start, fast middle, slow end. Good for "urgency" values.</summary>
        public static float Logistic(float x, float midpoint, float steepness) => 1f / (1f + Mathf.Exp(-steepness * (x - midpoint)));

        public static float Bool(bool b) => b ? 1f : 0f;

        /// <summary>
        /// Multiplies considerations, then applies a compensation factor.
        /// Plain multiplication punishes actions with many considerations
        /// (0.9 * 0.9 * 0.9 * 0.9 = 0.66 even though every input is good).
        /// The compensation (from Dave Mark's Infinite Axis Utility System) adds back
        /// part of the lost score: makeUp = (1 - s) * (1 - 1/n); s += makeUp * s.
        /// Any single zero still vetoes the action, which acts as a hard requirement.
        /// </summary>
        public static float Combine(params float[] considerations)
        {
            if (considerations.Length == 0) return 0f;
            float score = 1f;
            foreach (float c in considerations)
            {
                score *= Mathf.Clamp01(c);
                if (score <= 0f) return 0f;
            }
            float modification = 1f - 1f / considerations.Length;
            float makeUp = (1f - score) * modification;
            return score + makeUp * score;
        }
    }
}
