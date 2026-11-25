using UnityEngine;

namespace FailSafeZero.Interactions
{
    public class PlayerInteractor : MonoBehaviour
    {
        [Tooltip("How far the player can reach, in metres.")]
        [SerializeField] private float reach = 3f;
        [SerializeField] private KeyCode interactKey = KeyCode.E;
        [SerializeField] private LayerMask interactMask = ~0;

        private IInteractable current;
        private PickupThrow pickup;

        public IInteractable Current => current;

        private void Awake() => pickup = GetComponent<PickupThrow>();

        private void Update()
        {
            current = null;
            if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, reach, interactMask, QueryTriggerInteraction.Ignore))
            {
                // InParent, so the collider can sit on a child (the door leaf) of the scripted object (the hinge).
                current = hit.collider.GetComponentInParent<IInteractable>();
            }

            bool pickupBusy = pickup != null && pickup.IsBusy;
            if (current != null && !pickupBusy && Input.GetKeyDown(interactKey))
                current.Interact(this);
        }

        // Simple on-screen prompt, no UI setup needed.
        private void OnGUI()
        {
            if (current == null || (pickup != null && pickup.IsHolding)) return;
            var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 18 };
            GUI.Label(new Rect(Screen.width / 2f - 150f, Screen.height / 2f + 30f, 300f, 30f), $"[{interactKey}] {current.Prompt}", style);
        }
    }
}
