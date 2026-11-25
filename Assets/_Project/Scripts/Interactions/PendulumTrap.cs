using FailSafeZero.AI.Illusion;
using UnityEngine;

namespace FailSafeZero.Interactions
{
    [RequireComponent(typeof(Rigidbody))]
    public class PendulumTrap : MonoBehaviour
    {
        [SerializeField] private float maxAngle = 60f;
        [SerializeField] private float period = 2.6f;
        [SerializeField] private float timeOffset = 0f;
        [SerializeField] private float damage = 20f;
        [SerializeField] private float knockback = 10f;
        [SerializeField] private float hitCooldown = 0.8f;

        private Rigidbody body;
        private Quaternion baseRotation;
        private float lastHit = -999f;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            baseRotation = transform.rotation;
        }

        private void FixedUpdate()
        {
            float angle = maxAngle * Mathf.Sin((Time.time + timeOffset) * 2f * Mathf.PI / period);
            body.MoveRotation(baseRotation * Quaternion.Euler(0f, 0f, angle));
        }

        private void OnTriggerEnter(Collider other) => Hit(other);
        private void OnTriggerStay(Collider other) => Hit(other);

        private void Hit(Collider other)
        {
            if (Time.time - lastHit < hitCooldown) return;
            var target = other.GetComponentInParent<IDamageable>();
            if (target == null) return;
            lastHit = Time.time;
            target.ReceiveHit(damage, other.ClosestPoint(transform.position), gameObject);

            Vector3 dir = other.transform.position - transform.position;
            dir.y = 0f;
            other.GetComponentInParent<IKnockbackable>()?.ApplyKnockback(dir.normalized * knockback + Vector3.up * 3f);
        }
    }
}
