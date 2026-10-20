using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FailSafeZero.AI.Illusion.Testing
{
    /// <summary>
    /// TEST-ONLY first-person player, so the Illusion Bot can be tested before
    /// the team's real player is ready. WASD to move, mouse to look, left click to
    /// shoot, Esc to free the mouse.
    ///
    /// When the real player exists, delete this from the scene. The bot only needs
    /// an object tagged "Player".
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class IllusionTestPlayer : MonoBehaviour, IDamageable
    {
        [SerializeField] Camera playerCamera;
        [SerializeField] float moveSpeed = 5f;
        [SerializeField] float lookSensitivity = 0.12f;
        [SerializeField] float gunDamage = 25f;
        [SerializeField] float fireInterval = 0.25f;
        [SerializeField] float maxHealth = 100f;

        CharacterController controller;
        float pitch;
        float verticalSpeed;
        float fireTimer;
        float health;
        Vector3 spawnPoint;
        string lastHitText = "";

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (playerCamera == null)
            {
                playerCamera = GetComponentInChildren<Camera>();
            }
            health = maxHealth;
            spawnPoint = transform.position;
        }

        void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void Update()
        {
            float dt = Time.deltaTime;

            // ---- look
            Vector2 look = ReadLook();
            transform.Rotate(0f, look.x, 0f);
            pitch = Mathf.Clamp(pitch - look.y, -80f, 80f);
            if (playerCamera != null)
            {
                playerCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }

            // ---- move (with simple gravity)
            Vector2 input = ReadMove();
            Vector3 move = (transform.right * input.x + transform.forward * input.y) * moveSpeed;
            verticalSpeed = controller.isGrounded ? -1f : verticalSpeed - 9.81f * dt;
            move.y = verticalSpeed;
            controller.Move(move * dt);

            // ---- shoot (hitscan from the centre of the screen)
            fireTimer -= dt;
            if (ReadFire() && fireTimer <= 0f && playerCamera != null)
            {
                fireTimer = fireInterval;
                Shoot();
            }

            if (ReadEscape())
            {
                Cursor.lockState = Cursor.lockState == CursorLockMode.Locked ? CursorLockMode.None : CursorLockMode.Locked;
                Cursor.visible = Cursor.lockState != CursorLockMode.Locked;
            }
        }

        void Shoot()
        {
            Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            if (!Physics.Raycast(ray, out RaycastHit hit, 100f, ~0, QueryTriggerInteraction.Ignore))
            {
                lastHitText = "miss";
                return;
            }

            IDamageable target = hit.collider.GetComponentInParent<IDamageable>();
            if (target != null)
            {
                target.ReceiveHit(gunDamage, hit.point, gameObject);
                lastHitText = hit.collider.GetComponentInParent<IllusionMirage>() != null ? "MIRAGE (fake!)" : "REAL BOT!";
            }
            else
            {
                lastHitText = hit.collider.name;
            }
        }

        public void ReceiveHit(float amount, Vector3 hitPoint, GameObject source)
        {
            health -= amount;
            if (health <= 0f)
            {
                // Test respawn, so the session keeps going.
                health = maxHealth;
                controller.enabled = false;
                transform.position = spawnPoint;
                controller.enabled = true;
            }
        }

        void OnGUI()
        {
            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;
            GUI.Label(new Rect(cx - 5f, cy - 12f, 20f, 24f), "+");
            GUI.Label(new Rect(Screen.width - 230f, 10f, 220f, 22f), $"TEST PLAYER  HP {health:0}/{maxHealth:0}");
            GUI.Label(new Rect(Screen.width - 230f, 32f, 220f, 22f), $"Last shot: {lastHitText}");
        }

        // ------------------------------------------------------------ input (works with both input systems)

        static Vector2 ReadMove()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard k = Keyboard.current;
            if (k == null) return Vector2.zero;
            float x = (k.dKey.isPressed ? 1f : 0f) - (k.aKey.isPressed ? 1f : 0f);
            float y = (k.wKey.isPressed ? 1f : 0f) - (k.sKey.isPressed ? 1f : 0f);
            return Vector2.ClampMagnitude(new Vector2(x, y), 1f);
#else
            return Vector2.ClampMagnitude(new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")), 1f);
#endif
        }

        Vector2 ReadLook()
        {
            if (Cursor.lockState != CursorLockMode.Locked) return Vector2.zero;
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null ? Mouse.current.delta.ReadValue() * lookSensitivity : Vector2.zero;
#else
            return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * (lookSensitivity * 20f);
#endif
        }

        static bool ReadFire()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null && Mouse.current.leftButton.isPressed;
#else
            return Input.GetMouseButton(0);
#endif
        }

        static bool ReadEscape()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }
    }
}
