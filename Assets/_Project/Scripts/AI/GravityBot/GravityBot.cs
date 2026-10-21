using FailSafeZero.AI.Illusion;
using FailSafeZero.Core;
using UnityEngine;
using UnityEngine.AI;

namespace FailSafeZero.AI.Gravity
{
    /// <summary>
    /// GRAVITY BOT (Arena 2). A failed experiment that bends gravity around itself.
    ///
    /// ARCHITECTURE: a Finite State Machine for the big modes, with a small utility
    /// score to choose its next attack.
    ///
    ///   Idle -> Stalk -> (GravityWell | SlamRise -> SlamFall -> Overheated) -> Stalk ...
    ///
    /// ATTACKS
    ///  - Gravity Well: stops and PULLS the player towards itself. Loose physics crates
    ///    float up, then get THROWN at the player when the well ends. If the player is
    ///    dragged too close, they are crushed (damage).
    ///  - Slam: rises 3 m into the air, then crashes down. The shockwave damages and
    ///    knocks back the player, and blasts crates away.
    ///
    /// WEAKNESS (the story hint): after a slam its core OVERHEATS for 2.5 s. It cannot
    /// move and takes DOUBLE damage. Stay out of the shockwave, then punish it.
    ///
    /// Health comes from the shared EnemyHealth component (On Defeated works too).
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(EnemyHealth))]
    public class GravityBot : MonoBehaviour
    {
        public enum State { Idle, Stalk, GravityWell, SlamRise, SlamFall, Overheated }

        [Header("Target")]
        [Tooltip("Leave empty: finds the object tagged Player.")]
        [SerializeField] Transform player;
        [SerializeField] float detectRange = 22f;
        [SerializeField] float eyeHeight = 1.5f;

        [Header("Stalking")]
        [SerializeField] float preferredRange = 9f;
        [SerializeField] float decisionInterval = 0.3f;

        [Header("Gravity Well")]
        [SerializeField] float wellDuration = 2.5f;
        [SerializeField] float wellCooldown = 7f;
        [SerializeField] float wellMinRange = 5f;
        [SerializeField] float wellMaxRange = 18f;
        [Tooltip("Metres per second the player is dragged towards the bot.")]
        [SerializeField] float wellPullSpeed = 4.5f;
        [SerializeField] float crushRadius = 2.2f;
        [SerializeField] float crushDamage = 20f;
        [SerializeField] float liftRadius = 12f;
        [SerializeField] float liftAcceleration = 12f;
        [SerializeField] float throwSpeed = 14f;

        [Header("Slam")]
        [SerializeField] float slamCooldown = 6f;
        [SerializeField] float slamTriggerRange = 8f;
        [SerializeField] float riseHeight = 3f;
        [SerializeField] float riseTime = 0.8f;
        [SerializeField] float fallTime = 0.15f;
        [SerializeField] float shockRadius = 7f;
        [SerializeField] float shockDamage = 35f;
        [SerializeField] float knockbackSpeed = 9f;

        [Header("Weakness")]
        [SerializeField] float overheatTime = 2.5f;
        [SerializeField] float overheatDamageMultiplier = 2f;

        [Header("Look")]
        [SerializeField] float ringSpinDegreesPerSecond = 60f;
        [SerializeField] Color calmColor = new Color(0.25f, 0.55f, 1f);
        [SerializeField] Color wellColor = new Color(0.6f, 0.3f, 1f);
        [SerializeField] Color slamColor = new Color(1f, 0.85f, 0.2f);
        [SerializeField] Color overheatColor = new Color(1f, 0.15f, 0.1f);

        [Header("Debug")]
        [SerializeField] bool showDebugPanel = false;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        static readonly Collider[] Overlaps = new Collider[32];

        NavMeshAgent agent;
        EnemyHealth health;
        CharacterController playerController;
        IDamageable playerDamage;
        Transform ring;
        Material[] materials;

        public State CurrentState { get; private set; } = State.Idle;

        float baseOffset;
        float stateTimer;
        float decisionTimer;
        float wellReadyAt;
        float slamReadyAt;
        float crushTimer;
        Vector3 knockVelocity;
        float knockTimer;
        float scoreWell, scoreSlam, scoreStalk;

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            health = GetComponent<EnemyHealth>();
            baseOffset = agent.baseOffset;

            foreach (Transform t in GetComponentsInChildren<Transform>())
            {
                if (t.name.Contains("Ring")) { ring = t; break; }
            }

            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            materials = new Material[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                materials[i] = renderers[i].material; // one copy per part, so colour changes stay on this bot
                if (materials[i].HasProperty(EmissionId)) materials[i].EnableKeyword("_EMISSION");
            }
            SetColor(calmColor);
        }

        void Start()
        {
            if (player == null)
            {
                GameObject tagged = GameObject.FindWithTag("Player");
                if (tagged != null) player = tagged.transform;
            }

            if (player != null)
            {
                playerController = player.GetComponent<CharacterController>();
                playerDamage = player.GetComponentInParent<IDamageable>();
            }

            if (!agent.isOnNavMesh && NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
            }
        }

        void OnDestroy()
        {
            if (materials == null) return;
            foreach (Material m in materials)
            {
                if (m != null) Destroy(m);
            }
        }

        void Update()
        {
            if (player == null || !agent.isOnNavMesh) return;

            float dt = Time.deltaTime;
            stateTimer -= dt;
            crushTimer -= dt;

            if (ring != null)
            {
                float spin = CurrentState == State.GravityWell ? ringSpinDegreesPerSecond * 6f : ringSpinDegreesPerSecond;
                if (CurrentState != State.Overheated) ring.Rotate(Vector3.up, spin * dt, Space.World);
            }

            ApplyKnockback(dt);

            switch (CurrentState)
            {
                case State.Idle: UpdateIdle(); break;
                case State.Stalk: UpdateStalk(dt); break;
                case State.GravityWell: UpdateWell(dt); break;
                case State.SlamRise: UpdateSlamRise(); break;
                case State.SlamFall: UpdateSlamFall(); break;
                case State.Overheated: UpdateOverheated(); break;
            }
        }

        // ------------------------------------------------------------------ states

        void UpdateIdle()
        {
            bool shot = health.Current < health.MaxHealth; // being shot wakes it up
            if (shot || (DistanceToPlayer() <= detectRange && HasLineOfSight()))
            {
                Enter(State.Stalk);
            }
        }

        void UpdateStalk(float dt)
        {
            FacePlayer(dt);

            decisionTimer -= dt;
            if (decisionTimer > 0f) return;
            decisionTimer = decisionInterval;

            float distance = DistanceToPlayer();
            bool sees = HasLineOfSight();

            // Utility scores (0..1). The highest wins; 0.3 = keep stalking.
            float wellReady = Time.time >= wellReadyAt ? 1f : 0f;
            float inWellBand = distance >= wellMinRange && distance <= wellMaxRange ? 1f : 0f;
            scoreWell = 0.9f * wellReady * inWellBand * (sees ? 1f : 0f);

            float slamReady = Time.time >= slamReadyAt ? 1f : 0f;
            scoreSlam = slamReady * Mathf.Clamp01(1f - distance / slamTriggerRange);

            scoreStalk = 0.3f;

            if (scoreSlam >= scoreWell && scoreSlam > scoreStalk)
            {
                Enter(State.SlamRise);
            }
            else if (scoreWell > scoreStalk)
            {
                Enter(State.GravityWell);
            }
            else
            {
                // Keep about preferredRange away from the player.
                Vector3 away = transform.position - player.position;
                away.y = 0f;
                if (away.sqrMagnitude < 0.01f) away = transform.forward;
                Vector3 target = player.position + away.normalized * preferredRange;
                if (NavMesh.SamplePosition(target, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                {
                    agent.SetDestination(hit.position);
                }
            }
        }

        void UpdateWell(float dt)
        {
            FacePlayer(dt);

            Vector3 toBot = transform.position - player.position;
            toBot.y = 0f;
            float distance = toBot.magnitude;

            // 1. Drag the player in. Stronger when closer, like real gravity (but capped).
            if (playerController != null && playerController.enabled && distance > 0.8f)
            {
                float strength = Mathf.Lerp(1f, 0.5f, Mathf.Clamp01(distance / wellMaxRange));
                playerController.Move(toBot / distance * (wellPullSpeed * strength * dt));
            }

            // 2. Crush damage if they get dragged right in.
            if (distance < crushRadius && crushTimer <= 0f)
            {
                crushTimer = 1f;
                HurtPlayer(crushDamage);
            }

            // 3. Loose physics objects float up.
            int count = Physics.OverlapSphereNonAlloc(transform.position, liftRadius, Overlaps, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Rigidbody body = Overlaps[i].attachedRigidbody;
                if (body != null && !body.isKinematic && !body.transform.IsChildOf(transform))
                {
                    body.AddForce(Vector3.up * liftAcceleration, ForceMode.Acceleration);
                }
            }

            if (stateTimer <= 0f)
            {
                ThrowFloatingObjects();
                Enter(State.Stalk);
            }
        }

        void UpdateSlamRise()
        {
            float k = 1f - Mathf.Clamp01(stateTimer / riseTime);
            agent.baseOffset = baseOffset + riseHeight * Mathf.SmoothStep(0f, 1f, k);
            if (stateTimer <= 0f) Enter(State.SlamFall);
        }

        void UpdateSlamFall()
        {
            float k = 1f - Mathf.Clamp01(stateTimer / fallTime);
            agent.baseOffset = baseOffset + riseHeight * (1f - k * k); // accelerates downwards
            if (stateTimer <= 0f)
            {
                agent.baseOffset = baseOffset;
                Shockwave();
                Enter(State.Overheated);
            }
        }

        void UpdateOverheated()
        {
            // Blink red: "hit me now".
            SetColor(Mathf.Repeat(Time.time * 6f, 1f) > 0.5f ? overheatColor : Color.black);
            if (stateTimer <= 0f) Enter(State.Stalk);
        }

        void Enter(State next)
        {
            CurrentState = next;
            agent.isStopped = next != State.Stalk;
            agent.updateRotation = next == State.Idle;
            health.DamageMultiplier = 1f;

            switch (next)
            {
                case State.Idle:
                case State.Stalk:
                    SetColor(calmColor);
                    decisionTimer = 0f;
                    break;

                case State.GravityWell:
                    SetColor(wellColor);
                    stateTimer = wellDuration;
                    wellReadyAt = Time.time + wellCooldown;
                    break;

                case State.SlamRise:
                    SetColor(slamColor);
                    stateTimer = riseTime;
                    slamReadyAt = Time.time + slamCooldown;
                    break;

                case State.SlamFall:
                    stateTimer = fallTime;
                    break;

                case State.Overheated:
                    stateTimer = overheatTime;
                    health.DamageMultiplier = overheatDamageMultiplier; // the weakness
                    break;
            }
        }

        // ------------------------------------------------------------------ attacks

        void Shockwave()
        {
            Vector3 centre = transform.position;
            Vector3 fromBot = player.position - centre;
            fromBot.y = 0f;
            float distance = fromBot.magnitude;

            if (distance < shockRadius)
            {
                // Damage falls off linearly with distance: full at the centre, 0 at the edge.
                float falloff = 1f - distance / shockRadius;
                HurtPlayer(shockDamage * falloff);
                knockVelocity = (distance > 0.01f ? fromBot / distance : transform.forward) * knockbackSpeed * falloff;
                knockTimer = 0.35f;
            }

            int count = Physics.OverlapSphereNonAlloc(centre, shockRadius, Overlaps, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Rigidbody body = Overlaps[i].attachedRigidbody;
                if (body != null && !body.isKinematic && !body.transform.IsChildOf(transform))
                {
                    body.AddExplosionForce(10f, centre, shockRadius, 1f, ForceMode.Impulse);
                }
            }
        }

        void ThrowFloatingObjects()
        {
            Vector3 targetPoint = player.position + Vector3.up;
            int count = Physics.OverlapSphereNonAlloc(transform.position, liftRadius, Overlaps, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Rigidbody body = Overlaps[i].attachedRigidbody;
                if (body != null && !body.isKinematic && !body.transform.IsChildOf(transform))
                {
                    Vector3 dir = (targetPoint - body.position).normalized;
                    body.AddForce(dir * throwSpeed, ForceMode.VelocityChange);
                }
            }
        }

        void ApplyKnockback(float dt)
        {
            if (knockTimer <= 0f || playerController == null || !playerController.enabled) return;
            knockTimer -= dt;
            playerController.Move(knockVelocity * dt);
        }

        void HurtPlayer(float amount)
        {
            if (amount <= 0f) return;
            if (playerDamage != null)
            {
                playerDamage.ReceiveHit(amount, player.position, gameObject);
            }
            else
            {
                player.SendMessageUpwards("TakeDamage", amount, SendMessageOptions.DontRequireReceiver);
            }
        }

        // ------------------------------------------------------------------ helpers

        float DistanceToPlayer()
        {
            Vector3 d = player.position - transform.position;
            d.y = 0f;
            return d.magnitude;
        }

        bool HasLineOfSight()
        {
            Vector3 eye = transform.position + Vector3.up * eyeHeight;
            Vector3 target = player.position + Vector3.up;
            if (!Physics.Linecast(eye, target, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore))
            {
                return true;
            }
            return hit.transform.IsChildOf(player) || hit.transform.IsChildOf(transform);
        }

        void FacePlayer(float dt)
        {
            Vector3 d = player.position - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), 240f * dt);
        }

        void SetColor(Color c)
        {
            foreach (Material m in materials)
            {
                if (m == null) continue;
                if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, Color.Lerp(c, Color.white, 0.25f));
                if (m.HasProperty(EmissionId)) m.SetColor(EmissionId, c * 1.5f);
            }
        }

        void OnGUI()
        {
            if (!showDebugPanel) return;
            GUILayout.BeginArea(new Rect(10, 270, 290, 120), GUI.skin.box);
            GUILayout.Label($"GRAVITY BOT  state: {CurrentState}  HP: {health.Current:0}");
            GUILayout.Label($"Well {scoreWell:0.00}   Slam {scoreSlam:0.00}   Stalk {scoreStalk:0.00}");
            GUILayout.Label($"Damage x{health.DamageMultiplier:0.0}");
            GUILayout.EndArea();
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.6f, 0.3f, 1f);
            Gizmos.DrawWireSphere(transform.position, liftRadius);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, shockRadius);
        }
    }
}
