using FailSafeZero.AI.Illusion;
using UnityEngine;

namespace FailSafeZero.Interactions
{
    [RequireComponent(typeof(Rigidbody))]
    public class Throwable : MonoBehaviour
    {
        [Tooltip("Objects heavier than this cannot be picked up by the player.")]
        [SerializeField] private float maxCarryMass = 25f;
        [Tooltip("Impacts slower than this (m/s) do no damage.")]
        [SerializeField] private float damageSpeedThreshold = 6f;
        [SerializeField] private float damagePerSpeed = 1.2f;

        public Rigidbody Body { get; private set; }
        public bool IsHeld { get; set; }
        public GameObject LastThrower { get; set; }
        public bool CanBeCarried => Body.mass <= maxCarryMass;

        private float lastHitTime;

        private void Awake()
        {
            Body = GetComponent<Rigidbody>();
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.sleepThreshold = 0.05f;
        }

        public void Launch(Vector3 velocity, GameObject thrower)
        {
            IsHeld = false;
            LastThrower = thrower;
            Body.isKinematic = false;
            Body.useGravity = true;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Body.linearVelocity = velocity;
        }

        private void OnCollisionEnter(Collision collision)
        {
            float speed = collision.relativeVelocity.magnitude;

            if (speed >= damageSpeedThreshold && Time.time - lastHitTime > 0.2f)
            {
                GameObject hitRoot = collision.collider.transform.root.gameObject;
                bool hitThrower = LastThrower != null && hitRoot == LastThrower.transform.root.gameObject;
                var target = collision.collider.GetComponentInParent<IDamageable>();
                if (target != null && !hitThrower)
                {
                    lastHitTime = Time.time;
                    float damage = (speed - damageSpeedThreshold) * damagePerSpeed * Mathf.Sqrt(Body.mass);
                    target.ReceiveHit(damage, collision.GetContact(0).point, LastThrower);
                }
                NoiseEvents.Emit(transform.position, 12f);   // loud crash: bots can hear it
            }

            // Telekinetic throws fly straight (no gravity); the first hit makes it fall normally.
            if (!IsHeld) Body.useGravity = true;
            if (speed < damageSpeedThreshold) Body.collisionDetectionMode = CollisionDetectionMode.Discrete;
        }
    }
}
