using System;
using UnityEngine;

namespace FailSafeZero.AI.Illusion
{
    /// <summary>
    /// The energy bolt fired by the bot and its mirages.
    ///
    /// Optimisation choices (useful for the GV viva):
    ///  - No Rigidbody and no collider. Each frame it raycasts only the distance it is
    ///    about to travel. That is cheaper than physics, and a fast bolt can never
    ///    "tunnel" through a thin wall.
    ///  - Pooled. Bolts are reused and never Instantiated or Destroyed during the
    ///    fight, so there are no garbage-collection spikes.
    ///  - Mirage bolts look identical but do 0 damage. Only real bolts hurt.
    /// </summary>
    public class IllusionProjectile : MonoBehaviour
    {
        static readonly RaycastHit[] Hits = new RaycastHit[8];

        Vector3 direction;
        float speed;
        float damage;
        float life;
        GameObject shooter;
        Action<IllusionProjectile> release;

        public void Launch(Vector3 position, Vector3 dir, float projectileSpeed, float projectileDamage,
                           GameObject source, Action<IllusionProjectile> onRelease)
        {
            transform.position = position;
            direction = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;
            transform.rotation = Quaternion.LookRotation(direction);
            speed = projectileSpeed;
            damage = projectileDamage;
            shooter = source;
            release = onRelease;
            life = 4f;
            gameObject.SetActive(true);
        }

        void Update()
        {
            float step = speed * Time.deltaTime;
            life -= Time.deltaTime;

            if (life <= 0f)
            {
                Release();
                return;
            }

            if (TryFindHit(step, out RaycastHit hit))
            {
                if (damage > 0f)
                {
                    IDamageable target = hit.collider.GetComponentInParent<IDamageable>();
                    if (target != null)
                    {
                        target.ReceiveHit(damage, hit.point, shooter);
                    }
                    else
                    {
                        // Teammate's player script may use a plain TakeDamage(float).
                        hit.collider.SendMessageUpwards("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);
                    }
                }

                Release();
                return;
            }

            transform.position += direction * step;
        }

        bool TryFindHit(float distance, out RaycastHit closest)
        {
            closest = default;
            int count = Physics.RaycastNonAlloc(transform.position, direction, Hits, distance, ~0,
                                                QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            bool found = false;

            for (int i = 0; i < count; i++)
            {
                Collider c = Hits[i].collider;

                // Bolts pass through every Illusion Bot body (real or fake).
                if (c.GetComponentInParent<IllusionBotBrain>() != null || c.GetComponentInParent<IllusionMirage>() != null)
                {
                    continue;
                }

                if (Hits[i].distance < best)
                {
                    best = Hits[i].distance;
                    closest = Hits[i];
                    found = true;
                }
            }

            return found;
        }

        void Release()
        {
            gameObject.SetActive(false);
            release?.Invoke(this);
        }
    }
}
