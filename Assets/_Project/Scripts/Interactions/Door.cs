using UnityEngine;

namespace FailSafeZero.Interactions
{
    [RequireComponent(typeof(Rigidbody))]
    public class Door : MonoBehaviour, IInteractable
    {
        [SerializeField] private float openAngle = 100f;
        [Tooltip("Degrees per second.")]
        [SerializeField] private float swingSpeed = 160f;
        [SerializeField] private bool startLocked = false;
        [Tooltip("Seconds before the door closes by itself. 0 = never.")]
        [SerializeField] private float autoCloseDelay = 0f;
        [Tooltip("The other leaf of a double door. Opening one opens both.")]
        [SerializeField] private Door partner;
        [SerializeField] private AudioClip openSound;
        [SerializeField] private AudioClip lockedSound;

        private Rigidbody body;
        private Quaternion closedLocalRotation;
        private float targetAngle;   // signed: which way depends on the player's side
        private float currentAngle;
        private float openedAt;
        private bool locked;

        public bool IsOpen => Mathf.Abs(targetAngle) > 0.1f;
        public bool IsLocked => locked;
        public string Prompt => locked ? "Locked" : (IsOpen ? "Close door" : "Open door");

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            closedLocalRotation = transform.localRotation;
            locked = startLocked;
        }

        public void Interact(PlayerInteractor interactor)
        {
            if (locked) { Play(lockedSound); return; }
            Vector3 from = interactor != null ? interactor.transform.position : transform.position - transform.forward;
            if (IsOpen) { Close(); if (partner != null) partner.Close(); }
            else { Open(from); if (partner != null) partner.Open(from); }
        }

        public void Open(Vector3 fromPosition)
        {
            if (locked) return;
            float side = Vector3.Dot(transform.forward, fromPosition - transform.position) >= 0f ? 1f : -1f;
            targetAngle = openAngle * side;
            openedAt = Time.time;
            Play(openSound);
        }

        public void Close() => targetAngle = 0f;

        public void SetLocked(bool value)
        {
            locked = value;
            if (locked) Close();
        }

        private void FixedUpdate()
        {
            if (autoCloseDelay > 0f && IsOpen && Time.time - openedAt > autoCloseDelay) Close();
            if (Mathf.Approximately(currentAngle, targetAngle)) return;

            currentAngle = Mathf.MoveTowards(currentAngle, targetAngle, swingSpeed * Time.fixedDeltaTime);
            Quaternion local = closedLocalRotation * Quaternion.Euler(0f, currentAngle, 0f);
            body.MoveRotation(transform.parent != null ? transform.parent.rotation * local : local);
        }

        private void Play(AudioClip clip)
        {
            if (clip != null) AudioSource.PlayClipAtPoint(clip, transform.position);
        }
    }
}
