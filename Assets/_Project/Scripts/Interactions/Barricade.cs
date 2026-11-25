using UnityEngine;
using UnityEngine.AI;

namespace FailSafeZero.Interactions
{
    
    [RequireComponent(typeof(Rigidbody))]
    public class Barricade : MonoBehaviour, IInteractable
    {
        [SerializeField] private float mass = 80f;
        [Tooltip("Force each physics step while the player walks into it.")]
        [SerializeField] private float pushForce = 900f;
        [Tooltip("One-off impulse when the player presses E.")]
        [SerializeField] private float shoveImpulse = 250f;
        [SerializeField] private float maxPushSpeed = 2.5f;

        private Rigidbody body;

        public string Prompt => "Shove barricade";

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.mass = mass;
            body.linearDamping = 4f;           
            body.angularDamping = 8f;
            body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;   
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.sleepThreshold = 0.05f;        

            var obstacle = GetComponent<NavMeshObstacle>();
            if (obstacle != null)
            {
                obstacle.carving = true;
                obstacle.carveOnlyStationary = true;  
            }
        }

        public void Interact(PlayerInteractor interactor)
        {
            body.AddForce(Flat(transform.position - interactor.transform.position) * shoveImpulse, ForceMode.Impulse);
        }

      
        public void PushFrom(Vector3 direction)
        {
            Vector3 dir = Flat(direction);
            if (Vector3.Dot(body.linearVelocity, dir) < maxPushSpeed)
                body.AddForce(dir * pushForce, ForceMode.Force);
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.zero;
        }
    }
}
