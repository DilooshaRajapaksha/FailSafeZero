using System.Collections;
using FailSafeZero.AI.Illusion;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

namespace FailSafeZero.Core
{
    public class EnemyHealth : MonoBehaviour, IDamageable
    {
        [SerializeField, Min(1f)] float maxHealth = 100f;
        [SerializeField, Min(0f)] float defeatDuration = 0.8f;
        [SerializeField] bool showHealthBar = true;
        [SerializeField] bool hideWhenFull = true;
        [SerializeField] string healthBarLabel = "ENEMY";

        public UnityEvent onDefeated = new UnityEvent();

        static Texture2D whiteTexture;

        public float MaxHealth => maxHealth;
        public float Current { get; private set; }
        public bool IsDead { get; private set; }
        public float Health01 => Current / maxHealth;

        public float DamageMultiplier { get; set; } = 1f;

        void Awake()
        {
            Current = maxHealth;
            if (whiteTexture == null)
                whiteTexture = Texture2D.whiteTexture;
        }

        public void ReceiveHit(float amount, Vector3 hitPoint, GameObject source)
        {
            if (IsDead || amount <= 0f)
            {
                return;
            }

            Current = Mathf.Max(0f, Current - amount * DamageMultiplier);
            if (Current <= 0f)
            {
                Defeat();
            }
        }

        public void TakeDamage(float amount)
        {
            ReceiveHit(amount, transform.position, null);
        }

        void Defeat()
        {
            IsDead = true;

            // Play the defeat pose before disabling the enemy scripts.
            gameObject.BroadcastMessage("OnDefeatedAnimation", SendMessageOptions.DontRequireReceiver);

            // Disable the enemy scripts and legs after the defeat pose starts.
            foreach (MonoBehaviour b in GetComponents<MonoBehaviour>())
            {
                if (b != this) b.enabled = false;
            }

            NavMeshAgent agent = GetComponent<NavMeshAgent>();
            if (agent != null) agent.enabled = false;

            // The Maze Ghost moves with a Rigidbody: freeze it so it does not fall through the floor.
            Rigidbody body = GetComponent<Rigidbody>();
            if (body != null) body.isKinematic = true;

            foreach (Collider c in GetComponentsInChildren<Collider>())
            {
                c.enabled = false;
            }

            onDefeated?.Invoke();
            if (defeatDuration > 0f)
                StartCoroutine(ShrinkAway());
        }

        IEnumerator ShrinkAway()
        {
            Vector3 start = transform.localScale;
            float t = 0f;
            while (t < defeatDuration)
            {
                t += Time.deltaTime;
                transform.localScale = Vector3.Lerp(start, Vector3.zero, t / defeatDuration);
                yield return null;
            }
            gameObject.SetActive(false);
        }

        void OnGUI()
        {
            if (whiteTexture == null)
                whiteTexture = Texture2D.whiteTexture;

            if (IsDead || !showHealthBar || (hideWhenFull && Current >= maxHealth) || !IsVisibleToPlayer())
                return;

            Vector3 screen = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 2.3f);
            if (screen.z <= 0f)
                return;

            float width = 110f;
            float x = screen.x - width * 0.5f;
            float y = Screen.height - screen.y;
            float fill = Mathf.Clamp01(Health01);

            GUI.Box(new Rect(x, y, width, 10f), GUIContent.none);

            Color previous = GUI.color;
            GUI.color = Color.Lerp(Color.red, Color.green, fill);
            GUI.DrawTexture(new Rect(x + 2f, y + 2f, (width - 4f) * fill, 6f), whiteTexture);
            GUI.color = previous;

            GUI.Label(new Rect(x, y - 18f, width, 18f), $"{healthBarLabel} {Current:0}/{maxHealth:0}");
        }

        bool IsVisibleToPlayer()
        {
            Camera cam = Camera.main;
            if (cam == null)
                return false;

            Vector3 focus = transform.position + Vector3.up * 1f;
            Vector3 viewport = cam.WorldToViewportPoint(focus);
            if (viewport.z <= 0f || viewport.x < -0.05f || viewport.x > 1.05f ||
                viewport.y < -0.05f || viewport.y > 1.05f)
                return false;

            Vector3 origin = cam.transform.position + cam.transform.forward * 0.3f;
            Vector3 toFocus = focus - origin;
            float distance = toFocus.magnitude;
            if (distance < 0.01f)
                return true;

            if (Physics.Raycast(origin, toFocus / distance, out RaycastHit hit, distance,
                                ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform != transform && !hit.transform.IsChildOf(transform))
                    return false;
            }

            return true;
        }
    }
}
