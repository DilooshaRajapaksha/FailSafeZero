using FailSafeZero.AI.Illusion;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FailSafeZero.Core
{
    /// <summary>
    /// The player's life. Put it on the FirstPersonPlayer.
    ///
    /// - Takes damage from the Zombie's strikes, the Illusion Bot's bolts, and anything
    ///   that uses IDamageable or SendMessage("TakeDamage", x).
    /// - Shows a health bar, and the screen flashes red when hit.
    /// - On death it shows "YOU DIED" and restarts the level after a short delay.
    /// </summary>
    public class PlayerHealth : MonoBehaviour, IDamageable
    {
        [SerializeField, Min(1f)] float maxHealth = 100f;
        [SerializeField] float restartDelay = 2.5f;

        static Texture2D whiteTexture;

        float health;
        float flashTimer;
        float deathTimer = -1f;

        public float Current => health;
        public bool IsDead => deathTimer >= 0f;

        void Awake()
        {
            health = maxHealth;
            if (whiteTexture == null)
            {
                whiteTexture = Texture2D.whiteTexture;
            }
        }

        public void ReceiveHit(float amount, Vector3 hitPoint, GameObject source)
        {
            if (IsDead || amount <= 0f)
            {
                return;
            }

            health = Mathf.Max(0f, health - amount);
            flashTimer = 0.25f;

            if (health <= 0f)
            {
                deathTimer = restartDelay;
            }
        }

        /// <summary>Compatibility with SendMessage("TakeDamage", x).</summary>
        public void TakeDamage(float amount)
        {
            ReceiveHit(amount, transform.position, null);
        }

        void Update()
        {
            if (flashTimer > 0f)
            {
                flashTimer -= Time.deltaTime;
            }

            if (deathTimer >= 0f)
            {
                deathTimer -= Time.deltaTime;
                if (deathTimer < 0f)
                {
                    // The scene must be in File > Build Profiles (scene list) for this to work.
                    SceneManager.LoadScene(SceneManager.GetActiveScene().name);
                }
            }
        }

        void OnGUI()
        {
            // Red flash when hit.
            if (flashTimer > 0f || IsDead)
            {
                float alpha = IsDead ? 0.5f : flashTimer / 0.25f * 0.35f;
                GUI.color = new Color(0.8f, 0f, 0f, alpha);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), whiteTexture);
                GUI.color = Color.white;
            }

            // Health bar, bottom left.
            float w = 220f;
            var back = new Rect(20f, Screen.height - 40f, w, 18f);
            GUI.Box(back, GUIContent.none);
            GUI.color = Color.Lerp(Color.red, Color.green, health / maxHealth);
            GUI.DrawTexture(new Rect(back.x + 2f, back.y + 2f, (w - 4f) * (health / maxHealth), 14f), whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(back.x + 6f, back.y - 20f, w, 20f), $"HEALTH {health:0}/{maxHealth:0}");

            if (IsDead)
            {
                var style = new GUIStyle(GUI.skin.label) { fontSize = 40, alignment = TextAnchor.MiddleCenter };
                GUI.Label(new Rect(0, 0, Screen.width, Screen.height), "YOU DIED", style);
            }
        }
    }
}
