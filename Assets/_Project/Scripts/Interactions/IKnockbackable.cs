using UnityEngine;

namespace FailSafeZero.Interactions
{
    public interface IKnockbackable
    {
        void ApplyKnockback(Vector3 velocity);
    }
}
