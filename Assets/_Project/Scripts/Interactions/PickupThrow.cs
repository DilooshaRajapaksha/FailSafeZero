using UnityEngine;

namespace FailSafeZero.Interactions
{
  
    public class PickupThrow : MonoBehaviour
    {
        [SerializeField] private float reach = 3f;
        [SerializeField] private float holdDistance = 1.8f;
        [Tooltip("How strongly the object is pulled to the hold point.")]
        [SerializeField] private float followStrength = 15f;
        [SerializeField] private float maxFollowSpeed = 12f;
        [SerializeField] private float throwSpeed = 15f;
        [SerializeField] private float breakDistance = 3f;
        [SerializeField] private KeyCode grabKey = KeyCode.E;

        private Throwable held;
        private float savedAngularDamping;
        private int busyFrame = -1;
        private Behaviour gun;       
        private bool waitForMouseUp;

        public bool IsHolding => held != null;
        public bool IsBusy => held != null || Time.frameCount == busyFrame;

        private void Start()
        {
            foreach (var b in transform.root.GetComponentsInChildren<MonoBehaviour>(true))
                if (b.GetType().Name == "LabPistol") { gun = b; break; }
        }

        private void Update()
        {
            if (waitForMouseUp && !Input.GetMouseButton(0)) { waitForMouseUp = false; SetGun(true); }

            if (held == null)
            {
                if (Input.GetKeyDown(grabKey)) TryPickUp();
                return;
            }

            if (held == null || !held.gameObject.activeInHierarchy) { Drop(); return; }
            if (Input.GetMouseButtonDown(0)) Throw();
            else if (Input.GetKeyDown(grabKey)) Drop();
        }

        private void FixedUpdate()
        {
            if (held == null) return;
            Vector3 holdPoint = transform.position + transform.forward * holdDistance;
            Vector3 toTarget = holdPoint - held.Body.position;
            if (toTarget.magnitude > breakDistance) { Drop(); return; }

            held.Body.linearVelocity = Vector3.ClampMagnitude(toTarget * followStrength, maxFollowSpeed);
            held.Body.angularVelocity *= 0.8f;
        }

        private void TryPickUp()
        {
            if (!Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, reach, ~0, QueryTriggerInteraction.Ignore))
                return;

            var t = hit.collider.GetComponentInParent<Throwable>();
            if (t == null || t.IsHeld || !t.CanBeCarried) return;

            held = t;
            busyFrame = Time.frameCount;
            held.IsHeld = true;
            held.Body.isKinematic = false;
            held.Body.useGravity = false;
            held.Body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            savedAngularDamping = held.Body.angularDamping;
            held.Body.angularDamping = 10f;
            SetGun(false);
        }

        private Throwable Release()
        {
            var t = held;
            held = null;
            busyFrame = Time.frameCount;
            if (t != null)
            {
                t.Body.useGravity = true;
                t.Body.angularDamping = savedAngularDamping;
                t.IsHeld = false;
            }
            waitForMouseUp = true;
            return t;
        }

        private void Drop() => Release();

        private void Throw()
        {
            var t = Release();
            if (t == null) return;
            float speed = throwSpeed * Mathf.Clamp(Mathf.Sqrt(5f / Mathf.Max(t.Body.mass, 0.1f)), 0.4f, 1.5f);
            t.Launch(transform.forward * speed, transform.root.gameObject);
        }

        private void SetGun(bool on)
        {
            if (gun != null) gun.enabled = on;
        }

        private void OnGUI()
        {
            if (held == null) return;
            var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 16 };
            GUI.Label(new Rect(Screen.width / 2f - 150f, Screen.height / 2f + 30f, 300f, 30f), "[LMB] Throw   [E] Drop", style);
        }
    }
}
