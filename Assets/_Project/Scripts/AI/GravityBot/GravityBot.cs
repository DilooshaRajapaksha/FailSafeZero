using System.Collections.Generic;
using System.Text;
using FailSafeZero.Core;
using FailSafeZero.Interactions;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

namespace FailSafeZero.AI.Gravity
{
    
    [RequireComponent(typeof(NavMeshAgent), typeof(GravityHands), typeof(EnemyHealth))]
    public class GravityBot : MonoBehaviour
    {
        [Header("Senses")]
        [SerializeField] private Transform eye;
        [SerializeField] private float viewDistance = 25f;
        [Tooltip("Half-angle of the view cone in degrees.")]
        [SerializeField] private float viewAngle = 75f;
        [SerializeField] private float hearingMultiplier = 1f;
        [SerializeField] private float liftRadius = 9f;

        [Header("Territory")]
        [Tooltip("The bot guards the area around where it starts (its arena) and never leaves it. Arena 2 is 20 x 20 m, so ~9 m.")]
        [SerializeField] private float homeRadius = 9f;
        [Tooltip("It 'feels' the mass of anything inside this radius of home, even without seeing it.")]
        [SerializeField] private float gravitySenseRadius = 11f;

        [Header("Brain")]
        [Tooltip("Seconds between decisions. 0.2 = 5 decisions per second: responsive and cheap.")]
        [SerializeField] private float brainInterval = 0.2f;
        [SerializeField] private float momentumBonus = 1.2f;

        [Header("Energy (the weakness)")]
        [SerializeField] private float passiveEnergyRegen = 0.02f;   // per second
        [SerializeField] private float exposedDamageMultiplier = 3f;

        [Header("Debug")]
        [Tooltip("Shows live action scores on screen. Great for the viva; turn off for the final video.")]
        [SerializeField] private bool showDebug = true;

        public UnityEvent<bool> OnCoreExposedChanged = new UnityEvent<bool>();
        public UnityEvent OnPlayerFirstSeen = new UnityEvent();

        public float LiftRadius => liftRadius;
        public float Energy01 => energy;
        public bool CoreExposed { get; private set; }
        public string CurrentActionName => current != null ? current.Name : "Idle";

        private readonly GravityBotContext ctx = new GravityBotContext();
        private readonly List<UtilityAction> actions = new List<UtilityAction>();
        private readonly Collider[] overlapBuffer = new Collider[48];
        private readonly RaycastHit[] rayBuffer = new RaycastHit[16];
        private EnemyHealth health;
        private UtilityAction current;
        private float energy = 1f;
        private float nextThink;
        private float lastSeenTime = -999f;
        private float lastDamagedTime = -999f;
        private float lastHealth;
        private float releasePlayerAt = -1f;
        private bool everSeenPlayer;
        private string sightInfo = "";
        private string lastBlocker = "";
        private Vector3 home;

        public Vector3 Home => home;

        private void Awake()
        {
            health = GetComponent<EnemyHealth>();
            if (eye == null) eye = transform;

            ctx.Bot = this;
            ctx.Agent = GetComponent<NavMeshAgent>();
            ctx.Hands = GetComponent<GravityHands>();

            actions.Add(new SearchAction());
            actions.Add(new RepositionAction());
            actions.Add(new FlingAction());
            actions.Add(new LevitatePlayerAction());
            actions.Add(new ShieldAction());
            actions.Add(new RechargeAction());
        }

        private void Start()
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
            {
                ctx.Player = p.transform;
                ctx.PlayerController = p.GetComponentInParent<FirstPersonController>();
            }
            lastHealth = health.Current;

            // Snap onto the floor's NavMesh right where it was placed. The lab NavMesh also
            // covers tops of walls/ceilings, and an agent can otherwise spawn on the wrong one.
            home = transform.position;
            if (NavMesh.SamplePosition(home, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                home = hit.position;
                ctx.Agent.Warp(home);
            }
        }

        private void OnEnable() => NoiseEvents.NoiseMade += OnNoise;

        private void OnDisable()
        {
            // Runs on defeat too (EnemyHealth disables us): never leave the player floating.
            NoiseEvents.NoiseMade -= OnNoise;
            if (ctx.PlayerController != null) ctx.PlayerController.GravityScale = 1f;
            if (ctx.Hands != null) ctx.Hands.DropAll();
            if (health != null) health.DamageMultiplier = 1f;
        }

        private void Update()
        {
            if (ctx.Player == null || health.IsDead) return;
            if (!ctx.Agent.isOnNavMesh)
            {
                // Recover if it ever ends up off the NavMesh (e.g. knocked or spawned badly).
                if (NavMesh.SamplePosition(transform.position, out NavMeshHit fix, 3f, NavMesh.AllAreas)) ctx.Agent.Warp(fix.position);
                return;
            }
            ctx.Now = Time.time;

            if (!CoreExposed) energy = Mathf.Min(1f, energy + passiveEnergyRegen * Time.deltaTime);
            if (releasePlayerAt > 0f && Time.time >= releasePlayerAt) ReleasePlayerNow();
            TrackDamage();

            if (Time.time >= nextThink)
            {
                nextThink = Time.time + brainInterval;
                Sense();
                Think();
            }

            if (current != null && current.Tick(ctx, Time.deltaTime) == ActionStatus.Done)
            {
                current.Exit(ctx, Time.time);
                current = null;
                nextThink = 0f; 
            }
        }

        private void Sense()
        {
            Vector3 playerChest = ctx.Player.position + Vector3.up * 0.5f;
            Vector3 toPlayer = ctx.Player.position - transform.position;
            ctx.DistanceToPlayer = toPlayer.magnitude;

            bool inRange = ctx.DistanceToPlayer <= viewDistance;
            bool inTerritory = FlatDistance(ctx.Player.position, home) <= gravitySenseRadius;
            bool inCone = ctx.DistanceToPlayer < 4f || inTerritory || Vector3.Angle(transform.forward, toPlayer) <= viewAngle;
            bool los = inRange && HasLineOfSight(eye.position, playerChest);
            ctx.CanSeePlayer = inRange && inCone && los;

            sightInfo = ctx.CanSeePlayer ? "sees you" :
                        !inRange ? $"too far ({ctx.DistanceToPlayer:0} m)" :
                        !los ? $"view blocked by {lastBlocker}" : "you are behind it";

            if (ctx.CanSeePlayer)
            {
                lastSeenTime = Time.time;
                ctx.LastKnownPlayerPosition = ctx.Player.position;
                ctx.HasLastKnownPosition = true;
                if (!everSeenPlayer) { everSeenPlayer = true; OnPlayerFirstSeen?.Invoke(); }
            }

            
            if (!ctx.CanSeePlayer && FlatDistance(ctx.Player.position, home) <= gravitySenseRadius)
            {
                ctx.LastKnownPlayerPosition = ctx.Player.position;
                ctx.HasLastKnownPosition = true;
            }

            ctx.TimeSinceSeen = Time.time - lastSeenTime;
            ctx.TimeSinceDamaged = Time.time - lastDamagedTime;
            ctx.Energy01 = energy;
            ctx.Health01 = health.Health01;
            ctx.PlayerIsFloating = ctx.PlayerController != null && ctx.PlayerController.GravityScale < 0.5f;

            ctx.NearbyThrowables.Clear();
            int n = Physics.OverlapSphereNonAlloc(transform.position, liftRadius, overlapBuffer, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var t = overlapBuffer[i].GetComponentInParent<Throwable>();
                if (t != null && !t.IsHeld && t.gameObject.activeInHierarchy && !ctx.NearbyThrowables.Contains(t))
                    ctx.NearbyThrowables.Add(t);
            }
        }

        private void TrackDamage()
        {
            if (health.Current < lastHealth)
            {
                lastDamagedTime = Time.time;
                if (ctx.Player != null) { ctx.LastKnownPlayerPosition = ctx.Player.position; ctx.HasLastKnownPosition = true; }
            }
            lastHealth = health.Current;

            
            home = transform.position;
            if (NavMesh.SamplePosition(home, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                home = hit.position;
                ctx.Agent.Warp(home);
            }
        }

        private void OnNoise(Vector3 position, float radius)
        {
            if (ctx.CanSeePlayer) return;
            float r = radius * hearingMultiplier;
            if ((position - transform.position).sqrMagnitude > r * r) return;
            ctx.LastKnownPlayerPosition = position;   
            ctx.HasLastKnownPosition = true;
        }

        private void Think()
        {
            if (current != null && !current.CanBeInterrupted) return;

            UtilityAction best = null;
            float bestScore = 0.01f;
            foreach (var a in actions)
            {
                float s = a.OnCooldown(Time.time) ? 0f : a.Score(ctx);
                if (a == current) s *= momentumBonus;   
                a.LastScore = s;
                if (s > bestScore) { bestScore = s; best = a; }
            }

            if (best == null || best == current) return;
            current?.Exit(ctx, Time.time);
            current = best;
            current.Enter(ctx);
        }

       
        public bool HasLineOfSight(Vector3 from, Vector3 to)
        {
            Vector3 dir = to - from;
            float dist = dir.magnitude;
            int n = Physics.RaycastNonAlloc(from, dir / dist, rayBuffer, dist, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                Transform t = rayBuffer[i].transform;
                if (t.IsChildOf(transform)) continue;
                if (ctx.Player != null && t.IsChildOf(ctx.Player.root)) continue;
                if (rayBuffer[i].collider.GetComponentInParent<Throwable>() != null) continue;
                lastBlocker = rayBuffer[i].collider.name;
                return false;
            }
            return true;
        }

        
        public Vector3 ClampToHome(Vector3 point)
        {
            Vector3 offset = point - home;
            offset.y = 0f;
            if (offset.magnitude > homeRadius) point = home + offset.normalized * homeRadius;
            point.y = home.y;
            return NavMesh.SamplePosition(point, out NavMeshHit hit, 2f, NavMesh.AllAreas) ? hit.position : home;
        }

        public bool IsInsideHome(Vector3 point) => FlatDistance(point, home) <= homeRadius;

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f; b.y = 0f;
            return Vector3.Distance(a, b);
        }

        public int CountThrowablesNear(Vector3 point)
        {
            int n = Physics.OverlapSphereNonAlloc(point, liftRadius * 0.7f, overlapBuffer, ~0, QueryTriggerInteraction.Ignore);
            int count = 0;
            for (int i = 0; i < n; i++)
                if (overlapBuffer[i].GetComponentInParent<Throwable>() != null) count++;
            return count;
        }

        public void SpendEnergy(float amount) => energy = Mathf.Max(0f, energy - amount);
        public void AddEnergy(float amount) => energy = Mathf.Min(1f, energy + amount);

        public void FaceTowards(Vector3 target)
        {
            Vector3 dir = target - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), 360f * Time.deltaTime);
        }

        public void SetCoreExposed(bool value)
        {
            if (CoreExposed == value) return;
            CoreExposed = value;
            health.DamageMultiplier = value ? exposedDamageMultiplier : 1f;
            OnCoreExposedChanged?.Invoke(value);
        }

        public void HoldPlayerFloating() => releasePlayerAt = -1f;
        public void ReleasePlayerAfter(float seconds) => releasePlayerAt = Time.time + seconds;

        private void ReleasePlayerNow()
        {
            releasePlayerAt = -1f;
            if (ctx.PlayerController != null) ctx.PlayerController.GravityScale = 1f;
        }

        private void OnGUI()
        {
            if (!showDebug || health == null || health.IsDead || ctx.Player == null) return;
            if (ctx.DistanceToPlayer > viewDistance + 10f) return;   // only show near its arena

            var sb = new StringBuilder();
            sb.AppendLine($"GRAVITY BOT   energy {energy * 100f:0}%");
            sb.AppendLine($"doing: {CurrentActionName}{(CoreExposed ? "   << CORE EXPOSED x3 >>" : "")}");
            sb.AppendLine(ctx.Agent != null && !ctx.Agent.isOnNavMesh ? "NOT ON NAVMESH" : sightInfo);
            foreach (var a in actions) sb.AppendLine($"{a.Name}: {a.LastScore:0.00}");
            GUI.Box(new Rect(Screen.width - 300, 10, 290, 58 + actions.Count * 16), sb.ToString());
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, liftRadius);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, viewDistance);
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(Application.isPlaying ? home : transform.position, homeRadius);
        }
    }
}
