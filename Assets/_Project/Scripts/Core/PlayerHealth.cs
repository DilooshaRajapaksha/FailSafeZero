using FailSafeZero.Weapons;
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
        [SerializeField] bool showHealthBar = true;

        [Header("Death animation")]
        [SerializeField] float deathAnimationDuration = 0.9f;
        [SerializeField] float deathCameraDrop = 1.4f;
        [SerializeField] float deathCameraPitch = 15f;
        [SerializeField] float deathCameraRoll = 75f;
        static Texture2D whiteTexture;

        float health;
        float flashTimer;
        float deathTimer = -1f;

        Camera deathCamera;
        Vector3 deathStartPosition;
        Quaternion deathStartRotation;
        bool deathAnimating;
        float deathAnimationTime;
        public float Current => health;
        public float MaxHealth => maxHealth;
        public bool IsDead => deathTimer >= 0f;

        void Awake()
        {
            health = maxHealth;
            deathCamera = Camera.main;
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
                StartDeath();
            }
        }

        /// <summary>Compatibility with SendMessage("TakeDamage", x).</summary>
        public void TakeDamage(float amount)
        {
            ReceiveHit(amount, transform.position, null);
        }

        /// <summary>Freeze gameplay and play a short first-person fall when the player dies.</summary>
        void StartDeath()
        {
            deathTimer = restartDelay;
            deathAnimating = true;
            deathAnimationTime = 0f;

            if (deathCamera != null)
            {
                deathStartPosition = deathCamera.transform.localPosition;
                deathStartRotation = deathCamera.transform.localRotation;
            }

            FirstPersonController controller = GetComponent<FirstPersonController>();
            if (controller != null) controller.enabled = false;

            LabPistol pistol = GetComponentInChildren<LabPistol>(true);
            if (pistol != null)
            {
                pistol.enabled = false;
                pistol.gameObject.SetActive(false);
            }

            CharacterController characterController = GetComponent<CharacterController>();
            if (characterController != null) characterController.enabled = false;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        /// <summary>Drop the camera and roll it to the side, like the player collapsing.</summary>
        void UpdateDeathAnimation(float dt)
        {
            deathAnimationTime += dt;
            float t = Mathf.Clamp01(deathAnimationTime / Mathf.Max(0.01f, deathAnimationDuration));
            float eased = t * t * (3f - 2f * t);

            if (deathCamera == null) return;

            deathCamera.transform.localPosition = deathStartPosition + Vector3.down * deathCameraDrop * eased;
            deathCamera.transform.localRotation = deathStartRotation *
                Quaternion.Euler(deathCameraPitch * eased, 0f, deathCameraRoll * eased);
        }

        void Update()
        {
            if (flashTimer > 0f)
            {
                flashTimer -= Time.deltaTime;
            }

            if (deathAnimating)
            {
                UpdateDeathAnimation(Time.deltaTime);
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
            if (whiteTexture == null)
            {
                whiteTexture = Texture2D.whiteTexture;
            }

            // Red flash when hit.
            if (flashTimer > 0f || IsDead)
            {
                float alpha = IsDead ? 0.5f : flashTimer / 0.25f * 0.35f;
                GUI.color = new Color(0.8f, 0f, 0f, alpha);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), whiteTexture);
                GUI.color = Color.white;
            }

            // Health bar, bottom left.
            if (showHealthBar)
            {
                float w = 220f;
                var back = new Rect(20f, Screen.height - 40f, w, 18f);
                GUI.Box(back, GUIContent.none);
                GUI.color = Color.Lerp(Color.red, Color.green, health / maxHealth);
                GUI.DrawTexture(new Rect(back.x + 2f, back.y + 2f, (w - 4f) * (health / maxHealth), 14f), whiteTexture);
                GUI.color = Color.white;
                GUI.Label(new Rect(back.x + 6f, back.y - 20f, w, 20f), $"HEALTH {health:0}/{maxHealth:0}");
            }

            if (IsDead)
            {
                var style = new GUIStyle(GUI.skin.label) { fontSize = 40, alignment = TextAnchor.MiddleCenter };
                GUI.Label(new Rect(0, 0, Screen.width, Screen.height), "YOU DIED", style);
            }
        }
    }
}
