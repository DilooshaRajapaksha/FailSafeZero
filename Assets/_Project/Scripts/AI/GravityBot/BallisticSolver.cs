using UnityEngine;

namespace FailSafeZero.AI.Gravity
{
    /// <summary>
    /// Aims a straight-line throw at a moving target ("leading the shot").
    /// We want a time t where the projectile and the target are in the same place:
    ///   |D + V t| = s t,   where D = target - origin, V = target velocity, s = projectile speed.
    /// Squaring both sides gives a quadratic:  (V·V - s²) t² + 2 (D·V) t + D·D = 0.
    /// The smallest positive root is the earliest possible hit; aim at D + V t.
    /// If there is no solution (target faster than projectile) we aim straight at it.
    /// </summary>
    public static class BallisticSolver
    {
        public static Vector3 InterceptDirection(Vector3 origin, Vector3 target, Vector3 targetVelocity, float speed, out float time)
        {
            Vector3 d = target - origin;
            float a = Vector3.Dot(targetVelocity, targetVelocity) - speed * speed;
            float b = 2f * Vector3.Dot(d, targetVelocity);
            float c = Vector3.Dot(d, d);

            time = -1f;
            if (Mathf.Abs(a) < 0.0001f)
            {
                if (Mathf.Abs(b) > 0.0001f) time = -c / b;   // linear case
            }
            else
            {
                float disc = b * b - 4f * a * c;
                if (disc >= 0f)
                {
                    float sq = Mathf.Sqrt(disc);
                    float t1 = (-b - sq) / (2f * a);
                    float t2 = (-b + sq) / (2f * a);
                    time = Mathf.Min(t1, t2);
                    if (time < 0f) time = Mathf.Max(t1, t2);
                }
            }

            if (time <= 0f) { time = d.magnitude / speed; return d.normalized; }
            return (d + targetVelocity * time).normalized;
        }
    }
}
