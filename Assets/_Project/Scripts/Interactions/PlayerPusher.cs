using UnityEngine;

namespace FailSafeZero.Interactions
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerPusher : MonoBehaviour
    {
        [SerializeField] private float lightPushStrength = 1.5f;

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            Rigidbody rb = hit.collider.attachedRigidbody;
            if (rb == null || rb.isKinematic) return;
            if (hit.moveDirection.y < -0.3f) return;   

            var barricade = rb.GetComponent<Barricade>();
            if (barricade != null) { barricade.PushFrom(hit.moveDirection); return; }

            var throwable = rb.GetComponent<Throwable>();
            if (throwable != null && throwable.IsHeld) return;

            Vector3 push = new Vector3(hit.moveDirection.x, 0f, hit.moveDirection.z);
            rb.AddForce(push * lightPushStrength, ForceMode.Impulse);
        }
    }
}
