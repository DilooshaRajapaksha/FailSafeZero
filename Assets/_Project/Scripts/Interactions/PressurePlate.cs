using UnityEngine;
using UnityEngine.Events;

namespace FailSafeZero.Interactions
{
    [RequireComponent(typeof(Collider))]
    public class PressurePlate : MonoBehaviour
    {
        [SerializeField] private float minMass = 5f;
        [SerializeField] private bool triggerOnce = false;
        [SerializeField] private float cooldown = 2f;
        public UnityEvent OnPressed = new UnityEvent();

        private float lastPressed = -999f;
        private bool used;

        private void Reset() => GetComponent<Collider>().isTrigger = true;

        private void OnTriggerEnter(Collider other)
        {
            if (used || Time.time - lastPressed < cooldown) return;

            bool heavyEnough = other.GetComponentInParent<CharacterController>() != null
                               || other.GetComponentInParent<UnityEngine.AI.NavMeshAgent>() != null
                               || (other.attachedRigidbody != null && !other.attachedRigidbody.isKinematic && other.attachedRigidbody.mass >= minMass);
            if (!heavyEnough) return;

            lastPressed = Time.time;
            if (triggerOnce) used = true;
            OnPressed?.Invoke();
        }
    }
}
