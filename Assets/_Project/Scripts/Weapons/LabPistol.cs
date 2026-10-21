using FailSafeZero.AI.Illusion;
using UnityEngine;
using UnityEngine.Rendering;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FailSafeZero.Weapons
{
    /// <summary>
    /// The player's Lab Pistol (custom Blender model, 848 triangles).
    ///
    /// Put the Lab_Pistol model under the player's CAMERA and add this script to it.
    /// It shows the gun in first person and handles:
    ///   - hitscan shooting from the centre of the screen (left click)
    ///   - a 12-round magazine and reload (R key), with a dip animation
    ///   - a muzzle flash (light) at the Muzzle point from Blender
    ///   - a bullet tracer line from the muzzle to the hit point
    ///   - recoil kick that springs back smoothly
    ///   - a crosshair with a red hit marker and shot spread feedback
    ///   - wall avoidance: near a wall the gun pulls back and lowers, so it never
    ///     sticks through the geometry (and cannot fire while pressed against it)
    ///
    /// Damage works on anything with IDamageable (the Illusion Bot and its mirages),
    /// and on any teammate's enemy that has a TakeDamage(float) method.
    /// </summary>
    public class LabPistol : MonoBehaviour
    {
        [Header("Shooting")]
        [SerializeField] float damage = 25f;
        [SerializeField] float range = 100f;
        [Tooltip("Seconds between shots (0.25 = 4 shots per second).")]
        [SerializeField] float fireInterval = 0.25f;

        [Header("Magazine")]
        [SerializeField] int magazineSize = 12;
        [SerializeField] float reloadTime = 1.2f;

        [Header("Feel")]
        [SerializeField] float recoilKickBack = 0.06f;
        [SerializeField] float recoilKickUpDegrees = 8f;
        [SerializeField] float recoilReturnSpeed = 12f;
        [SerializeField] Color flashColor = new Color(0.55f, 0.9f, 1f); // cool lab-blue, matches the teal theme

        [Header("Wall avoidance (stops the gun going through walls)")]
        [Tooltip("How far ahead of the camera to check for walls (about the gun's reach).")]
        [SerializeField] float wallCheckDistance = 0.9f;
        [Tooltip("How far the gun pulls back when touching a wall.")]
        [SerializeField] float wallPullBack = 0.3f;
        [Tooltip("How much the barrel tilts down when blocked (like holding a gun against your chest).")]
        [SerializeField] float wallLowerDegrees = 40f;
        [SerializeField] float wallBlendSpeed = 14f;

        [Header("Optional")]
        [Tooltip("Material for the tracer line. Leave empty to use a default.")]
        [SerializeField] Material tracerMaterial;
        [Tooltip("Show ammo and the last hit in the top-right corner.")]
        [SerializeField] bool showHud = true;

        [Header("Crosshair")]
        [SerializeField] bool showCrosshair = true;
        [SerializeField] Color crosshairColor = new Color(0.55f, 0.9f, 1f, 0.9f);
        [Tooltip("Colour of the cross for a moment after hitting an enemy.")]
        [SerializeField] Color hitMarkerColor = new Color(1f, 0.25f, 0.25f, 1f);
        [SerializeField] float crosshairSize = 9f;
        [SerializeField] float crosshairGap = 4f;
        [SerializeField] float crosshairThickness = 2f;

        Camera aimCamera;
        Transform muzzle;
        Light flash;
        LineRenderer tracer;
        Material runtimeTracerMaterial;

        Vector3 restPosition;
        Quaternion restRotation;
        float kick;          // 0..1 recoil amount, springs back to 0
        float wallBlend;     // 0 = free, 1 = fully pulled back against a wall
        float fireTimer;
        float flashTimer;
        float tracerTimer;
        float reloadTimer;
        int ammo;
        float hitMarkerTimer;  // > 0 while the cross shows the "you hit something" colour
        float spread;          // 0..1, the cross opens up after each shot and closes again

        public string LastHitText { get; private set; } = "";
        public bool IsReloading => reloadTimer > 0f;

        void Awake()
        {
            aimCamera = GetComponentInParent<Camera>();
            restPosition = transform.localPosition;
            restRotation = transform.localRotation;
            ammo = magazineSize;

            // The Blender file has a marker at the barrel tip. Find it by name.
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name.Contains("Muzzle"))
                {
                    muzzle = t;
                    break;
                }
            }

            // First-person guns should not cast shadows (no floating gun shadow on the floor,
            // and one less object in the shadow pass).
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
            }

            // Stop the gun from being cut off when it is very close to the camera.
            if (aimCamera != null && aimCamera.nearClipPlane > 0.05f)
            {
                aimCamera.nearClipPlane = 0.02f;
            }

            CreateFlash();
            CreateTracer();
        }

        void OnDestroy()
        {
            if (runtimeTracerMaterial != null) Destroy(runtimeTracerMaterial);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            fireTimer -= dt;

            if (reloadTimer > 0f)
            {
                reloadTimer -= dt;
                if (reloadTimer <= 0f)
                {
                    ammo = magazineSize;
                }
            }
            else if (ReadReload() && ammo < magazineSize)
            {
                reloadTimer = reloadTime;
            }
            else if (ReadFire() && fireTimer <= 0f && !IsBlockedByWall)
            {
                if (ammo > 0)
                {
                    Fire();
                }
                else
                {
                    reloadTimer = reloadTime; // auto-reload on empty
                }
            }

            UpdateEffects(dt);
            UpdatePose(dt);
        }

        void Fire()
        {
            fireTimer = fireInterval;
            ammo--;
            spread = 1f;
            kick = 1f;
            flashTimer = 0.05f;
            if (flash != null) flash.enabled = true;

            Vector3 muzzlePos = muzzle != null ? muzzle.position : transform.position + transform.forward * 0.3f;

            // Aim from the centre of the screen so the bullet goes where the crosshair is.
            Ray ray = aimCamera != null
                ? new Ray(aimCamera.transform.position, aimCamera.transform.forward)
                : new Ray(muzzlePos, transform.forward);

            Vector3 end = ray.origin + ray.direction * range;

            if (Physics.Raycast(ray, out RaycastHit hit, range, ~0, QueryTriggerInteraction.Ignore))
            {
                end = hit.point;
                GameObject shooter = transform.root.gameObject;

                IDamageable target = hit.collider.GetComponentInParent<IDamageable>();
                if (target != null)
                {
                    target.ReceiveHit(damage, hit.point, shooter);
                    hitMarkerTimer = 0.15f;
                    LastHitText = hit.collider.GetComponentInParent<IllusionMirage>() != null ? "MIRAGE (fake!)" : "HIT: " + hit.collider.transform.root.name;
                }
                else
                {
                    // Teammates' enemies (Zombie, Maze Ghost...) may use TakeDamage(float).
                    hit.collider.SendMessageUpwards("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);
                    LastHitText = hit.collider.name;
                }
            }
            else
            {
                LastHitText = "miss";
            }

            if (tracer != null)
            {
                tracer.SetPosition(0, muzzlePos);
                tracer.SetPosition(1, end);
                tracer.enabled = true;
                tracerTimer = 0.04f;
            }
        }

        void UpdateEffects(float dt)
        {
            if (hitMarkerTimer > 0f) hitMarkerTimer -= dt;
            spread = Mathf.Lerp(spread, 0f, 1f - Mathf.Exp(-10f * dt));

            if (flashTimer > 0f)
            {
                flashTimer -= dt;
                if (flashTimer <= 0f && flash != null) flash.enabled = false;
            }

            if (tracerTimer > 0f)
            {
                tracerTimer -= dt;
                if (tracerTimer <= 0f && tracer != null) tracer.enabled = false;
            }
        }

        /// <summary>Recoil + reload dip, layered on the rest pose. Springs back with an exponential ease.</summary>
        void UpdatePose(float dt)
        {
            kick = Mathf.Lerp(kick, 0f, 1f - Mathf.Exp(-recoilReturnSpeed * dt));

            float reloadDip = 0f;
            if (reloadTimer > 0f)
            {
                // 0 -> 1 -> 0 over the reload time (a smooth "lower and raise").
                float t = 1f - reloadTimer / reloadTime;
                reloadDip = Mathf.Sin(t * Mathf.PI);
            }

            // WALL AVOIDANCE: a thin sphere-cast "feels" ahead of the camera.
            // closeness = 0 when nothing is within reach, 1 when the wall is touching the camera.
            float target = 0f;
            if (aimCamera != null &&
                Physics.SphereCast(aimCamera.transform.position, 0.05f, aimCamera.transform.forward,
                                   out RaycastHit wallHit, wallCheckDistance, ~0, QueryTriggerInteraction.Ignore))
            {
                target = 1f - Mathf.Clamp01(wallHit.distance / wallCheckDistance);
            }
            // Ease towards the target so the gun moves smoothly, never snaps.
            wallBlend = Mathf.Lerp(wallBlend, target, 1f - Mathf.Exp(-wallBlendSpeed * dt));

            Vector3 offset = new Vector3(0f, -0.12f * reloadDip - 0.05f * wallBlend,
                                         -recoilKickBack * kick - wallPullBack * wallBlend);
            transform.localPosition = restPosition + offset;
            float pitch = -recoilKickUpDegrees * kick + 35f * reloadDip + wallLowerDegrees * wallBlend;
            transform.localRotation = restRotation * Quaternion.Euler(pitch, 0f, 0f);
        }

        /// <summary>True while the gun is pressed against a wall (it cannot fire into the wall).</summary>
        public bool IsBlockedByWall => wallBlend > 0.6f;

        void CreateFlash()
        {
            var go = new GameObject("MuzzleFlash");
            go.transform.SetParent(muzzle != null ? muzzle : transform, false);
            flash = go.AddComponent<Light>();
            flash.type = LightType.Point;
            flash.color = flashColor;
            flash.range = 4f;
            flash.intensity = 3f;
            flash.shadows = LightShadows.None; // a shadow-casting flash would cost a shadow map every shot
            flash.enabled = false;
        }

        void CreateTracer()
        {
            tracer = gameObject.AddComponent<LineRenderer>();
            tracer.positionCount = 2;
            tracer.useWorldSpace = true;
            tracer.startWidth = 0.015f;
            tracer.endWidth = 0.005f;
            tracer.shadowCastingMode = ShadowCastingMode.Off;
            tracer.receiveShadows = false;
            tracer.startColor = flashColor;
            tracer.endColor = new Color(flashColor.r, flashColor.g, flashColor.b, 0f);

            if (tracerMaterial != null)
            {
                tracer.sharedMaterial = tracerMaterial;
            }
            else
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    runtimeTracerMaterial = new Material(shader);
                    tracer.sharedMaterial = runtimeTracerMaterial;
                }
            }

            tracer.enabled = false;
        }

        void OnGUI()
        {
            if (showCrosshair && !IsBlockedByWall)
            {
                DrawCrosshair();
            }

            if (!showHud)
            {
                return;
            }

            string ammoText = IsReloading ? "RELOADING..." : $"AMMO {ammo}/{magazineSize}   (R = reload)";
            GUI.Label(new Rect(Screen.width - 230f, 54f, 220f, 22f), ammoText);
        }

        /// <summary>
        /// A 4-line cross in the centre of the screen. That is exactly where the bullet
        /// ray goes, because Fire() shoots from the camera centre. It opens slightly
        /// after each shot (recoil feedback) and flashes red on a hit (hit marker).
        /// </summary>
        void DrawCrosshair()
        {
            Texture2D tex = Texture2D.whiteTexture;
            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;
            float gap = crosshairGap + 6f * spread;
            float len = crosshairSize;
            float t = crosshairThickness;

            Color old = GUI.color;
            GUI.color = hitMarkerTimer > 0f ? hitMarkerColor : crosshairColor;
            GUI.DrawTexture(new Rect(cx - t * 0.5f, cy - gap - len, t, len), tex); // up
            GUI.DrawTexture(new Rect(cx - t * 0.5f, cy + gap, t, len), tex);       // down
            GUI.DrawTexture(new Rect(cx - gap - len, cy - t * 0.5f, len, t), tex); // left
            GUI.DrawTexture(new Rect(cx + gap, cy - t * 0.5f, len, t), tex);       // right
            GUI.DrawTexture(new Rect(cx - 1f, cy - 1f, 2f, 2f), tex);              // centre dot
            GUI.color = old;
        }

        // ------------------------------------------------------------ input (works with both input systems)

        static bool ReadFire()
        {
            if (Cursor.lockState != CursorLockMode.Locked) return false; // do not shoot while clicking Unity UI
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null && Mouse.current.leftButton.isPressed;
#else
            return Input.GetMouseButton(0);
#endif
        }

        static bool ReadReload()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.R);
#endif
        }
    }
}
