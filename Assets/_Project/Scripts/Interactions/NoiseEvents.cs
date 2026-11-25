using System;
using UnityEngine;

namespace FailSafeZero.Interactions
{
   
    public static class NoiseEvents
    {
        public static event Action<Vector3, float> NoiseMade;

        public static void Emit(Vector3 position, float radius) => NoiseMade?.Invoke(position, radius);
    }
}
