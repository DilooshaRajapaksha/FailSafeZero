using System.Collections;
using FailSafeZero.AI.Illusion;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

namespace FailSafeZero.Core
{
    /// <summary>
    /// A shared life bar for enemies that do not have their own (Zombie, Maze Ghost).
    ///
    /// It is ADDED next to the enemy's AI script and does not change it.
    /// The Lab Pistol already sends damage to anything with IDamageable, and any old
    /// gun using SendMessage("TakeDamage", x) works too.
    ///
    /// When health reaches 0, it switches off the enemy's AI scripts and NavMeshAgent,
    /// shrinks the body away, and fires On Defeated (hook the exit door here).
    /// </summary>
    public class EnemyHealth : MonoBehaviour, IDamageable
    {
        [SerializeField, Min(1f)] float maxHealth = 100f;
        [Tooltip("Seconds the defeat shrink effect takes.")]
        [SerializeField] float defeatDuration = 0.8f;

        [Tooltip("Runs once when this enemy is defeated (for example, open a door).")]
        public UnityEvent onDefeated = new UnityEvent();

        public float MaxHealth => maxHealth;
        public float Current { get; private set; }
        public bool IsDead { get; private set; }
        public float Health01 => Current / maxHealth;

        /// <summary>Set by an AI to create a weak moment (e.g. 2 = double damage while overheated).</summary>
        public float DamageMultiplier { get; set; } = 1f;

        void Awake()
        {
            Current = maxHealth;
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

        /// <summary>Compatibility with SendMessage("TakeDamage", x).</summary>
        public void TakeDamage(float amount)
        {
            ReceiveHit(amount, transform.position, null);
        }

        void Defeat()
        {
            IsDead = true;

            // Stop the AI: switch off every other script on this enemy, and its legs.
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
            // A small health bar over the enemy while it is hurt and on screen.
            if (IsDead || Current >= maxHealth || Camera.main == null)
            {
                return;
            }

            Vector3 screen = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 2.3f);
            if (screen.z <= 0f)
            {
                return; // behind the camera
            }

            float width = 80f;
            float x = screen.x - width * 0.5f;
            float y = Screen.height - screen.y;
            GUI.Box(new Rect(x, y, width, 8f), GUIContent.none);
            GUI.Box(new Rect(x, y, width * (Current / maxHealth), 8f), GUIContent.none);
        }
    }
}
