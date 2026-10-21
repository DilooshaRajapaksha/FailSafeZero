using UnityEngine;

namespace FailSafeZero.Core
{
    /// <summary>
    /// An invisible box (trigger). When the player walks in, the objective at the top of
    /// the screen changes. Put one at the entrance of each room.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ObjectiveZone : MonoBehaviour
    {
        [TextArea(1, 3)]
        [SerializeField] string objective = "Keep moving north.";
        [SerializeField] bool onlyOnce = true;

        bool used;

        void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        void OnTriggerEnter(Collider other)
        {
            if ((onlyOnce && used) || GameFlow.Instance == null) return;
            if (!IsPlayer(other)) return;

            used = true;
            GameFlow.Instance.SetObjective(objective);
        }

        public static bool IsPlayer(Collider other)
        {
            return other.CompareTag("Player") ||
                   (other.attachedRigidbody != null && other.attachedRigidbody.CompareTag("Player")) ||
                   other.transform.root.CompareTag("Player");
        }
    }
}
