using FailSafeZero.AI.Illusion;
using UnityEngine;

namespace FailSafeZero.Interactions
{
    [RequireComponent(typeof(Rigidbody))]
    public class FallingWeightTrap : MonoBehaviour
    {
        [SerializeField] private float damage = 40f;
        [SerializeField] private float resetDelay = 4f;
        [SerializeField] private float riseSpeed = 2f;

        private enum State { Armed, Falling, Landed, Rising }
        private State state = State.Armed;
        private Rigidbody body;
        private Vector3 startPos;
        private Quaternion startRot;
        private float landedAt;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            startPos = transform.position;
            startRot = transform.rotation;
            Arm();
        }

        public void Drop()
        {
            if (state != State.Armed) return;
            state = State.Falling;
            body.isKinematic = false;
            body.useGravity = true;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; // fast + heavy: don't tunnel through heads
        }

        private void Arm()
        {
            body.isKinematic = true;
            body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            state = State.Armed;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (state != State.Falling) return;
            collision.collider.GetComponentInParent<IDamageable>()?.ReceiveHit(damage, collision.GetContact(0).point, gameObject);
            NoiseEvents.Emit(transform.position, 15f);
            state = State.Landed;
            landedAt = Time.time;
        }

        private void FixedUpdate()
        {
            if (state == State.Landed && Time.time - landedAt > resetDelay)
            {
                state = State.Rising;
                body.isKinematic = true;
                body.useGravity = false;
                body.MoveRotation(startRot);
            }
            else if (state == State.Rising)
            {
                body.MovePosition(Vector3.MoveTowards(body.position, startPos, riseSpeed * Time.fixedDeltaTime));
                if ((body.position - startPos).sqrMagnitude < 0.0001f) Arm();
            }
        }
    }
}
