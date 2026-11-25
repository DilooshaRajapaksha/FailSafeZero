using System.Collections.Generic;
using UnityEngine;
using FailSafeZero.Interactions;

namespace FailSafeZero.AI.Gravity
{
    /// <summary>
    /// The bot's "telekinesis". Holds Throwables in the air at target points
    /// (above its head, or orbiting it as a shield) and launches them.
    /// Like the player's pickup, objects are moved by setting velocity, never teleported,
    /// so they still collide with walls and the player properly.
    /// </summary>
    public class GravityHands : MonoBehaviour
    {
        [SerializeField] private float followStrength = 8f;
        [SerializeField] private float maxFollowSpeed = 10f;
        [SerializeField] private float hoverHeight = 2.5f;
        [SerializeField] private float orbitRadius = 1.8f;
        [SerializeField] private float orbitSpeed = 140f;  // degrees per second

        private readonly List<Throwable> held = new List<Throwable>();
        private bool orbiting;

        public int HeldCount => held.Count;
        public IReadOnlyList<Throwable> Held => held;
        public Vector3 HoverPoint => transform.position + Vector3.up * hoverHeight;

        public bool Lift(Throwable t)
        {
            if (t == null || t.IsHeld || held.Contains(t)) return false;
            t.IsHeld = true;
            t.LastThrower = gameObject;
            t.Body.isKinematic = false;
            t.Body.useGravity = false;
            t.Body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            held.Add(t);
            return true;
        }

        public void SetOrbit(bool value) => orbiting = value;

        public void Launch(Throwable t, Vector3 velocity)
        {
            if (!held.Remove(t)) return;
            t.Launch(velocity, gameObject);
            t.Body.useGravity = false;       // flies straight; Throwable turns gravity back on at first hit
        }

        public void DropAll()
        {
            foreach (var t in held)
            {
                if (t == null) continue;
                t.IsHeld = false;
                t.Body.useGravity = true;
            }
            held.Clear();
            orbiting = false;
        }

        private void FixedUpdate()
        {
            held.RemoveAll(t => t == null);
            for (int i = 0; i < held.Count; i++)
            {
                Vector3 target = orbiting ? OrbitPoint(i, held.Count) : HoverPoint + Vector3.up * (i * 0.8f);
                Rigidbody rb = held[i].Body;
                rb.linearVelocity = Vector3.ClampMagnitude((target - rb.position) * followStrength, maxFollowSpeed);
                rb.angularVelocity = Vector3.Lerp(rb.angularVelocity, Vector3.up * 2f, 0.1f); // slow eerie spin
            }
        }

        private Vector3 OrbitPoint(int index, int count)
        {
            float angle = Time.time * orbitSpeed + index * (360f / Mathf.Max(1, count));
            Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * orbitRadius;
            return transform.position + Vector3.up * 1.2f + offset;
        }

        private void OnDisable() => DropAll();
    }
}
