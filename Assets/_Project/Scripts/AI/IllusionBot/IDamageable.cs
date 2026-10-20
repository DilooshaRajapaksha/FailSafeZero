using UnityEngine;

namespace FailSafeZero.AI.Illusion
{
    /// <summary>
    /// Anything that can be shot: the real Illusion Bot, its mirages and the test player.
    /// The gun only needs to find an IDamageable on what it hit, so it never needs
    /// to know *what* it hit. This keeps the weapon code and the AI code separate.
    /// </summary>
    public interface IDamageable
    {
        /// <param name="amount">Damage before any multipliers.</param>
        /// <param name="hitPoint">World position of the hit (for effects).</param>
        /// <param name="source">Who fired the shot. Can be null.</param>
        void ReceiveHit(float amount, Vector3 hitPoint, GameObject source);
    }
}
