using System;
using UnityEngine;
using UnityEngine.Events;

namespace FailSafeZero.AI.Illusion
{
    /// <summary>
    /// Health of the REAL Illusion Bot. It only stores numbers and raises events.
    /// It does not decide anything (the brain does) and draws nothing (the visuals do).
    /// </summary>
    public class IllusionBotHealth : MonoBehaviour, IDamageable
    {
        [SerializeField, Min(1f)] float maxHealth = 150f;

        [Tooltip("Runs once when the bot is defeated. Teammates can hook a door, a score or the next arena here without touching this code.")]
        public UnityEvent onDefeated = new UnityEvent();

        /// <summary>(damage actually taken, who shot)</summary>
        public event Action<float, GameObject> Damaged;
        public event Action Died;

        public float MaxHealth => maxHealth;
        public float Current { get; private set; }
        public float Health01 => Current / maxHealth;
        public bool IsDead { get; private set; }

        /// <summary>Set by the brain: 2x while stunned (the weakness window).</summary>
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

            float taken = amount * DamageMultiplier;
            Current = Mathf.Max(0f, Current - taken);
            Damaged?.Invoke(taken, source);

            if (Current <= 0f)
            {
                IsDead = true;
                Died?.Invoke();
                onDefeated?.Invoke();
            }
        }

        /// <summary>
        /// Compatibility for a teammate's gun that uses SendMessage("TakeDamage", 25f).
        /// </summary>
        public void TakeDamage(float amount)
        {
            ReceiveHit(amount, transform.position, null);
        }
    }
}
