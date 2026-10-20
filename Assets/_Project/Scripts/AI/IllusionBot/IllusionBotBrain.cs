using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace FailSafeZero.AI.Illusion
{
    /// <summary>
    /// THE ILLUSION BOT'S BRAIN. Architecture: FSM + Utility AI + Player Model.
    ///
    ///  1. PERCEPTION (10 times per second): vision cone + line-of-sight ray,
    ///     plus "close sense" (hears you within 3 m) and "shot reveals shooter".
    ///  2. FINITE STATE MACHINE: the big modes the bot can be in.
    ///        Patrol -> Combat -> Search -> Patrol,   any -> Stunned,   any -> Dead
    ///  3. UTILITY AI (inside Combat, 4 times per second): scores 5 tactics and
    ///     picks the best (see IllusionUtility).
    ///  4. PLAYER MODEL: learns how often the player is fooled and changes how
    ///     many mirages to use and how often to swap (see PlayerModel).
    ///  5. POSITION SEARCH: when placing mirages or strafing, it samples candidate
    ///     points on the NavMesh and scores them with a heuristic (spread, range,
    ///     visibility, reachability). Pathfinding itself is Unity's NavMesh A*.
    ///
    /// WEAKNESS (from the story): only the REAL bot casts a shadow. Hitting the
    /// real one shatters every mirage and STUNS it for a short time (2x damage).
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(IllusionBotHealth))]
    [RequireComponent(typeof(CapsuleCollider))]
    public class IllusionBotBrain : MonoBehaviour
    {
        public enum State { Patrol, Combat, Search, Stunned, Dead }

        [Header("References")]
        [Tooltip("Leave empty: finds the object tagged 'Player', or 'Player_TestTarget'.")]
        [SerializeField] Transform player;
        [Tooltip("The Illusion_Bot model (child object). Mirages are cloned from it.")]
        [SerializeField] GameObject modelRoot;
        [Tooltip("Optional material for the bolts. If empty, a violet one is made.")]
        [SerializeField] Material projectileMaterial;

        [Header("Perception")]
        [SerializeField] float viewDistance = 25f;
        [SerializeField, Range(10f, 360f)] float viewAngle = 120f;
        [Tooltip("Within this distance the bot 'hears' the player even without sight.")]
        [SerializeField] float closeSenseRadius = 3f;
        [Tooltip("Seconds the bot keeps chasing a remembered position after losing sight.")]
        [SerializeField] float memoryDuration = 4f;
        [SerializeField] float perceptionInterval = 0.1f;
        [SerializeField] float eyeHeight = 1.2f;

        [Header("Patrol & Search")]
        [SerializeField] float patrolRadius = 10f;
        [SerializeField] float patrolWait = 1.5f;
        [SerializeField] float searchDuration = 6f;

        [Header("Combat")]
        [SerializeField] float decisionInterval = 0.25f;
        [SerializeField] float attackCooldown = 1.8f;
        [Tooltip("Telegraph: the eye glows violet this long before firing. Makes dodging fair.")]
        [SerializeField] float chargeTime = 0.4f;
        [SerializeField] float projectileSpeed = 16f;
        [SerializeField] float projectileDamage = 10f;
        [SerializeField] float turnSpeed = 360f;

        [Header("Weakness")]
        [SerializeField] float stunDuration = 1.5f;
        [SerializeField] float stunnedDamageMultiplier = 2f;
        [Tooltip("After a stun ends, the bot cannot be stunned again for this long (no stun-lock).")]
        [SerializeField] float stunImmunity = 3f;

        [Header("Mirages")]
        [SerializeField, Range(1, 8)] int poolSize = 5;
        [SerializeField] float mirageLifetime = 12f;
        [Tooltip("How many candidate points are tested when choosing a position.")]
        [SerializeField, Range(4, 32)] int candidateCount = 12;

        [Header("Learning")]
        [Tooltip("How fast the player model adapts (EMA alpha). 0.25 = the last ~4 hits matter most.")]
        [SerializeField, Range(0.05f, 0.8f)] float learningRate = 0.25f;

        [Header("Utility AI weights")]
        [SerializeField] UtilitySettings utility = new UtilitySettings();

        [Header("Debug")]
        [SerializeField] bool showDebugPanel = true;

        // ---------------------------------------------------------------- events (for visuals / game)
        public event Action<BotMood> MoodChanged;
        public event Action Swapped;
        public event Action<State> StateChanged;

        // ---------------------------------------------------------------- public read-only
        public State CurrentState { get; private set; } = State.Patrol;
        public IllusionAction CurrentAction { get; private set; } = IllusionAction.Reposition;
        public bool CanSeePlayer { get; private set; }
        public PlayerModel Model => playerModel;
        public float AttackCooldown => attackCooldown;
        public float ChargeTime => chargeTime;

        /// <summary>Where the bot believes the player is (live if seen, else remembered).</summary>
        public Vector3 PlayerPosition => player != null && CanSeePlayer ? player.position : lastKnownPlayerPos;

        /// <summary>Chest height of the player: what bolts aim at.</summary>
        public Vector3 PlayerAimPoint => PlayerPosition + Vector3.up * 1.0f;

        // ---------------------------------------------------------------- private
        NavMeshAgent agent;
        IllusionBotHealth health;
        IllusionBotVisuals visuals;
        PlayerModel playerModel;
        IllusionUtility utilityAI;
        MiragePool mirages;
        Transform playerAim;

        readonly Queue<IllusionProjectile> projectilePool = new Queue<IllusionProjectile>();
        readonly List<IllusionProjectile> allProjectiles = new List<IllusionProjectile>();
        readonly RaycastHit[] rayHits = new RaycastHit[8];
        NavMeshPath scratchPath;
        Material sharedBoltMaterial;

        Vector3 home;
        Vector3 lastKnownPlayerPos;
        float lastSeenTime = -999f;
        BotMood mood = BotMood.Calm;

        float perceptionTimer;
        float decisionTimer;
        float attackTimer;
        float chargeTimer;
        bool charging;
        float stateTimer;
        float waitTimer;
        float moveLegTimer;
        float stuckTimer;
        float findPlayerTimer;
        bool warnedNoPlayer;
        float stunImmuneUntil;

        float timeSinceSwap = 999f;
        float timeSinceSpawn = 999f;
        float timeSinceHit = 999f;

        // =====================================================================
        // Unity lifecycle
        // =====================================================================

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            health = GetComponent<IllusionBotHealth>();
            visuals = GetComponent<IllusionBotVisuals>();
            playerModel = new PlayerModel(learningRate);
            utilityAI = new IllusionUtility(utility);
            scratchPath = new NavMeshPath();
            home = transform.position;
            lastKnownPlayerPos = transform.position + transform.forward * 5f;
        }

        void Start()
        {
            ResolvePlayer();

            if (!EnsureOnNavMesh())
            {
                return;
            }

            if (modelRoot == null)
            {
                modelRoot = FindModelChild();
            }

            if (modelRoot != null)
            {
                mirages = new MiragePool(this, modelRoot, poolSize);
            }
            else
            {
                Debug.LogWarning($"[IllusionBot] '{name}': no modelRoot assigned, so the bot will fight without mirages.", this);
            }

            if (visuals != null)
            {
                visuals.SetShadows(true); // the REAL bot always casts a shadow
            }

            health.Damaged += OnDamaged;
            health.Died += OnDied;

            ChangeState(State.Patrol);
        }

        void OnDestroy()
        {
            if (health != null)
            {
                health.Damaged -= OnDamaged;
                health.Died -= OnDied;
            }

            mirages?.DestroyAll();

            foreach (IllusionProjectile p in allProjectiles)
            {
                if (p != null) Destroy(p.gameObject);
            }

            if (sharedBoltMaterial != null) Destroy(sharedBoltMaterial);
        }

        void Update()
        {
            if (CurrentState == State.Dead)
            {
                return;
            }

            float dt = Time.deltaTime;
            timeSinceSwap += dt;
            timeSinceSpawn += dt;
            timeSinceHit += dt;
            attackTimer -= dt;

            // Perception runs at 10 Hz, not every frame. Raycasts are not free,
            // and humans do not react faster than ~0.1 s either.
            perceptionTimer -= dt;
            if (perceptionTimer <= 0f)
            {
                perceptionTimer = perceptionInterval;
                Perceive(perceptionInterval);
            }

            switch (CurrentState)
            {
                case State.Patrol: UpdatePatrol(dt); break;
                case State.Combat: UpdateCombat(dt); break;
                case State.Search: UpdateSearch(dt); break;
                case State.Stunned: UpdateStunned(dt); break;
            }
        }

        // =====================================================================
        // Perception
        // =====================================================================

        void Perceive(float dt)
        {
            if (player == null)
            {
                CanSeePlayer = false;
                findPlayerTimer -= dt;
                if (findPlayerTimer <= 0f)
                {
                    findPlayerTimer = 1f; // do not search the scene every frame
                    ResolvePlayer();
                }
                return;
            }

            Vector3 eye = transform.position + Vector3.up * eyeHeight;
            Vector3 toPlayer = player.position + Vector3.up * 1.0f - eye;
            float distance = toPlayer.magnitude;

            bool seen;
            if (distance <= closeSenseRadius)
            {
                seen = true; // too close to sneak past
            }
            else if (distance > viewDistance || Vector3.Angle(transform.forward, toPlayer) > viewAngle * 0.5f)
            {
                seen = false; // outside the vision cone
            }
            else
            {
                seen = HasLineOfSight(eye, toPlayer / distance, distance);
            }

            CanSeePlayer = seen;

            if (seen)
            {
                lastKnownPlayerPos = player.position;
                lastSeenTime = Time.time;
                playerModel.ObservePosition(player.position, dt);
            }
            else
            {
                playerModel.ForgetMotion();
            }
        }

        /// <summary>
        /// True if nothing solid is between the eye and the player.
        /// Ignores our own body and all mirages, because they do not block our view.
        /// </summary>
        bool HasLineOfSight(Vector3 from, Vector3 direction, float distance)
        {
            int count = Physics.RaycastNonAlloc(from, direction, rayHits, distance, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            Collider blocker = null;

            for (int i = 0; i < count; i++)
            {
                Collider c = rayHits[i].collider;
                if (c.transform.IsChildOf(transform) || c.GetComponentInParent<IllusionMirage>() != null)
                {
                    continue;
                }

                if (rayHits[i].distance < nearest)
                {
                    nearest = rayHits[i].distance;
                    blocker = c;
                }
            }

            return blocker == null || blocker.transform.IsChildOf(player);
        }

        void ResolvePlayer()
        {
            if (player == null)
            {
                GameObject tagged = GameObject.FindWithTag("Player");
                if (tagged != null)
                {
                    player = tagged.transform;
                }
                else
                {
                    GameObject testTarget = GameObject.Find("Player_TestTarget");
                    if (testTarget != null) player = testTarget.transform;
                }
            }

            if (player == null)
            {
                if (!warnedNoPlayer)
                {
                    Debug.LogWarning("[IllusionBot] No player found. Tag your player 'Player' or drag it into the Player field.", this);
                    warnedNoPlayer = true;
                }
                return;
            }

            // Where is the player LOOKING? Use their camera if they have one.
            Camera cam = player.GetComponentInChildren<Camera>();
            playerAim = cam != null ? cam.transform : player;
        }

        // =====================================================================
        // Finite State Machine
        // =====================================================================

        void ChangeState(State next)
        {
            CurrentState = next;
            charging = false;
            stuckTimer = 0f;

            if (agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.updateRotation = true;
            }

            switch (next)
            {
                case State.Patrol:
                    SetMood(BotMood.Calm);
                    waitTimer = 0f;
                    break;

                case State.Combat:
                    SetMood(BotMood.Alert);
                    decisionTimer = 0f; // decide immediately
                    CurrentAction = IllusionAction.Reposition;
                    break;

                case State.Search:
                    SetMood(BotMood.Alert);
                    stateTimer = searchDuration;
                    MoveTo(lastKnownPlayerPos);
                    break;

                case State.Stunned:
                    SetMood(BotMood.Stunned);
                    stateTimer = stunDuration;
                    health.DamageMultiplier = stunnedDamageMultiplier;
                    if (agent.isOnNavMesh) agent.isStopped = true;
                    mirages?.ShatterAll(); // the illusion breaks
                    break;

                case State.Dead:
                    SetMood(BotMood.Dead);
                    mirages?.ShatterAll();
                    if (agent.isOnNavMesh) agent.isStopped = true;
                    agent.enabled = false;
                    GetComponent<Collider>().enabled = false;
                    if (visuals != null) visuals.PlayDeath(() => gameObject.SetActive(false));
                    break;
            }

            StateChanged?.Invoke(next);
        }

        void UpdatePatrol(float dt)
        {
            if (CanSeePlayer)
            {
                ChangeState(State.Combat);
                return;
            }

            if (Arrived())
            {
                waitTimer -= dt;
                if (waitTimer <= 0f)
                {
                    waitTimer = patrolWait;
                    MoveTo(RandomNavPoint(home, patrolRadius));
                }
            }

            CheckStuck(dt);
        }

        void UpdateCombat(float dt)
        {
            // Lost the player for longer than our memory: go and look for them.
            if (!CanSeePlayer && Time.time - lastSeenTime > memoryDuration)
            {
                ChangeState(State.Search);
                return;
            }

            // While moving, keep facing the player (strafe), like a real fighter.
            agent.updateRotation = false;
            FaceTowards(PlayerPosition, dt);

            if (charging)
            {
                chargeTimer -= dt;
                if (chargeTimer <= 0f)
                {
                    charging = false;
                    agent.isStopped = false;
                    SetMood(BotMood.Alert);
                    FireAtPlayer();
                }
                return;
            }

            moveLegTimer += dt;
            decisionTimer -= dt;
            if (decisionTimer <= 0f)
            {
                decisionTimer = decisionInterval;
                Decide();
            }

            CheckStuck(dt);
        }

        void UpdateSearch(float dt)
        {
            if (CanSeePlayer)
            {
                ChangeState(State.Combat);
                return;
            }

            // Arrived at the last known position: look around.
            if (Arrived())
            {
                transform.Rotate(0f, 120f * dt, 0f);
            }

            stateTimer -= dt;
            if (stateTimer <= 0f)
            {
                ChangeState(State.Patrol);
            }

            CheckStuck(dt);
        }

        void UpdateStunned(float dt)
        {
            stateTimer -= dt;
            if (stateTimer > 0f)
            {
                return;
            }

            health.DamageMultiplier = 1f;
            stunImmuneUntil = Time.time + stunImmunity;
            timeSinceSpawn = 999f; // allow an immediate re-cast of the illusion
            bool rememberPlayer = Time.time - lastSeenTime <= memoryDuration;
            ChangeState(rememberPlayer ? State.Combat : State.Search);
        }

        // =====================================================================
        // Utility AI: decide and act
        // =====================================================================

        void Decide()
        {
            UtilityContext context = BuildContext();
            IllusionAction chosen = utilityAI.Choose(context, CurrentAction);
            bool changed = chosen != CurrentAction;
            CurrentAction = chosen;
            Execute(chosen, changed);
        }

        UtilityContext BuildContext()
        {
            return new UtilityContext
            {
                CanSeePlayer = CanSeePlayer,
                Distance = Vector3.Distance(transform.position, PlayerPosition),
                Health01 = health.Health01,
                ActiveMirages = mirages != null ? mirages.ActiveCount : 0,
                DesiredMirages = DesiredMirageCount(),
                AttackReady = attackTimer <= 0f,
                TimeSinceSwap = timeSinceSwap,
                TimeSinceSpawn = timeSinceSpawn,
                TimeSinceHit = timeSinceHit,
                PlayerSkill = playerModel.Skill
            };
        }

        /// <summary>
        /// Adaptive difficulty from the player model:
        /// desired = lerp(minMirages, maxMirages, skill), +1 when below half health.
        /// A player who keeps finding the real bot faces more copies.
        /// </summary>
        int DesiredMirageCount()
        {
            if (mirages == null)
            {
                return 0;
            }

            int desired = Mathf.RoundToInt(Mathf.Lerp(utility.minMirages, utility.maxMirages, playerModel.Skill));
            if (health.Health01 < 0.5f)
            {
                desired++;
            }
            return Mathf.Clamp(desired, 0, mirages.Capacity);
        }

        void Execute(IllusionAction action, bool changed)
        {
            switch (action)
            {
                case IllusionAction.Attack:
                    charging = true;
                    chargeTimer = chargeTime;
                    attackTimer = attackCooldown;
                    agent.isStopped = true;
                    SetMood(BotMood.Charging);
                    break;

                case IllusionAction.SpawnMirages:
                    SpawnMirages();
                    timeSinceSpawn = 0f;
                    break;

                case IllusionAction.Swap:
                    DoSwap();
                    timeSinceSwap = 0f;
                    break;

                case IllusionAction.Reposition:
                    if (changed || Arrived() || moveLegTimer > 3f)
                    {
                        MoveTo(FindStrafePoint(transform.position, transform));
                    }
                    break;

                case IllusionAction.Retreat:
                    if (changed || Arrived() || moveLegTimer > 3f)
                    {
                        MoveTo(FindRetreatPoint());
                    }
                    break;
            }
        }

        void SpawnMirages()
        {
            if (mirages == null)
            {
                return;
            }

            int needed = DesiredMirageCount() - mirages.ActiveCount;
            for (int i = 0; i < needed; i++)
            {
                if (!mirages.TryGetFree(out IllusionMirage mirage))
                {
                    break; // pool exhausted: never create extra objects mid-fight
                }

                mirage.Spawn(FindMiragePoint(), mirageLifetime);
            }
        }

        void DoSwap()
        {
            if (mirages == null || player == null)
            {
                return;
            }

            Vector3 aimForward = playerAim != null ? playerAim.forward : player.forward;
            IllusionMirage target = mirages.PickSwapTarget(player.position, aimForward);
            if (target == null)
            {
                return;
            }

            Vector3 myPosition = transform.position;
            Vector3 theirPosition = target.transform.position;

            agent.Warp(theirPosition);
            agent.ResetPath();
            target.WarpTo(myPosition);
            Swapped?.Invoke();
        }

        void FireAtPlayer()
        {
            Vector3 origin = visuals != null ? visuals.MuzzlePosition : transform.position + Vector3.up * eyeHeight;

            // LEAD TARGETING: aim where the player WILL be.
            // time = distance / boltSpeed,  predicted = position + velocity x time
            // (clamped to 1 s so a sudden dash does not throw the aim miles off).
            float travelTime = Mathf.Min(1f, Vector3.Distance(origin, PlayerAimPoint) / projectileSpeed);
            Vector3 predicted = PlayerAimPoint + playerModel.EstimatedVelocity * travelTime;

            FireProjectile(origin, predicted - origin, projectileDamage, gameObject);
        }

        /// <summary>Used by the bot (real damage) and by mirages (0 damage).</summary>
        public void FireProjectile(Vector3 origin, Vector3 direction, float damage, GameObject shooter)
        {
            IllusionProjectile bolt = projectilePool.Count > 0 ? projectilePool.Dequeue() : CreateProjectile();
            bolt.Launch(origin, direction, projectileSpeed, damage, shooter, projectilePool.Enqueue);
        }

        IllusionProjectile CreateProjectile()
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = $"{name}_Bolt";
            Destroy(go.GetComponent<Collider>()); // raycast-based, no collider needed
            go.transform.localScale = Vector3.one * 0.25f;

            var r = go.GetComponent<Renderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            if (projectileMaterial != null)
            {
                r.sharedMaterial = projectileMaterial;
            }
            else
            {
                // ONE shared material for every bolt, so they can be batched together.
                if (sharedBoltMaterial == null)
                {
                    sharedBoltMaterial = new Material(r.sharedMaterial) { color = new Color(0.54f, 0.36f, 0.96f) };
                }
                r.sharedMaterial = sharedBoltMaterial;
            }

            go.SetActive(false);
            var bolt = go.AddComponent<IllusionProjectile>();
            allProjectiles.Add(bolt);
            return bolt;
        }

        // =====================================================================
        // Reactions to being shot
        // =====================================================================

        void OnDamaged(float amount, GameObject source)
        {
            timeSinceHit = 0f;
            playerModel.RecordRealHit(); // the player found the REAL one

            // A shot reveals where the shooter is, even from behind.
            if (source != null)
            {
                lastKnownPlayerPos = source.transform.position;
                lastSeenTime = Time.time;
            }

            if (CurrentState == State.Stunned || CurrentState == State.Dead)
            {
                return;
            }

            if (Time.time >= stunImmuneUntil)
            {
                ChangeState(State.Stunned);
            }
            else if (CurrentState != State.Combat)
            {
                ChangeState(State.Combat);
            }
        }

        void OnDied()
        {
            ChangeState(State.Dead);
        }

        /// <summary>Called by a mirage when the player shoots it (the player was fooled).</summary>
        public void NotifyMirageHit(GameObject source)
        {
            playerModel.RecordMirageHit();

            if (source != null)
            {
                lastKnownPlayerPos = source.transform.position;
                lastSeenTime = Time.time;
            }

            if (CurrentState == State.Patrol || CurrentState == State.Search)
            {
                ChangeState(State.Combat);
            }
        }

        // =====================================================================
        // Position search (heuristic scoring of candidate points)
        // =====================================================================

        /// <summary>
        /// Mirage spawn point. Score = 0.45 x spread + 0.35 x rangeFit + 0.20 x visible, x reachable.
        ///  spread    : far from the other bodies, so the copies do not stack (most important).
        ///  rangeFit  : near the preferred fighting distance from the player.
        ///  visible   : the player can see it (a hidden decoy fools nobody).
        ///  reachable : NavMesh path exists. This MULTIPLIES, so it vetoes islands.
        /// A weighted SUM is used here because these are "nice to haves". One weak
        /// factor should lower the score, not reject the point.
        /// </summary>
        Vector3 FindMiragePoint()
        {
            Vector3 best = transform.position;
            float bestScore = float.MinValue;
            Vector3 centre = PlayerPosition;

            for (int i = 0; i < candidateCount; i++)
            {
                Vector3 raw = centre + RandomFlatDirection() * UnityEngine.Random.Range(utility.preferredRange * 0.6f, utility.preferredRange * 1.3f);
                if (!NavMesh.SamplePosition(raw, out NavMeshHit hit, 2f, agent.areaMask))
                {
                    continue;
                }

                float score = ScorePoint(hit.position, null, 0.45f, 0.35f, 0.20f, utility.preferredRange);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = hit.position;
                }
            }

            return best;
        }

        /// <summary>
        /// Strafe point for the bot or a mirage: rotate 30 to 75 degrees around the
        /// player (left or right) at about the preferred range. That is hard to hit,
        /// and it keeps the copies circling like the real one.
        /// </summary>
        public Vector3 FindStrafePoint(Vector3 from, Transform self)
        {
            Vector3 centre = PlayerPosition;
            Vector3 away = from - centre;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f)
            {
                away = RandomFlatDirection();
            }
            away.Normalize();

            Vector3 best = from;
            float bestScore = float.MinValue;
            int tries = Mathf.Max(4, candidateCount / 2);

            for (int i = 0; i < tries; i++)
            {
                float side = UnityEngine.Random.value < 0.5f ? -1f : 1f;
                float angle = UnityEngine.Random.Range(30f, 75f) * side;
                Vector3 dir = Quaternion.Euler(0f, angle, 0f) * away;
                Vector3 raw = centre + dir * utility.preferredRange * UnityEngine.Random.Range(0.8f, 1.2f);

                if (!NavMesh.SamplePosition(raw, out NavMeshHit hit, 2f, agent.areaMask))
                {
                    continue;
                }

                float score = ScorePoint(hit.position, self, 0.6f, 0.4f, 0f, utility.preferredRange);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = hit.position;
                }
            }

            return best;
        }

        /// <summary>Retreat: points away from the player at 1.5x the preferred range.</summary>
        Vector3 FindRetreatPoint()
        {
            Vector3 centre = PlayerPosition;
            Vector3 away = transform.position - centre;
            away.y = 0f;
            away = away.sqrMagnitude < 0.01f ? -transform.forward : away.normalized;

            Vector3 best = transform.position;
            float bestScore = float.MinValue;
            float targetRange = utility.preferredRange * 1.5f;

            for (int i = 0; i < candidateCount; i++)
            {
                Vector3 dir = Quaternion.Euler(0f, UnityEngine.Random.Range(-60f, 60f), 0f) * away;
                Vector3 raw = centre + dir * targetRange;
                if (!NavMesh.SamplePosition(raw, out NavMeshHit hit, 2f, agent.areaMask))
                {
                    continue;
                }

                float score = ScorePoint(hit.position, transform, 0.3f, 0.7f, 0f, targetRange);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = hit.position;
                }
            }

            return best;
        }

        float ScorePoint(Vector3 p, Transform self, float wSpread, float wRange, float wVisible, float idealRange)
        {
            // Spread: distance to the nearest other body, capped at 6 m, then normalised to 0..1.
            float nearest = 6f;
            if (self != transform)
            {
                nearest = Mathf.Min(nearest, Vector3.Distance(p, transform.position));
            }
            if (mirages != null)
            {
                foreach (IllusionMirage m in mirages.All)
                {
                    if (m == null || !m.IsActive || m.transform == self) continue;
                    nearest = Mathf.Min(nearest, Vector3.Distance(p, m.transform.position));
                }
            }
            float spread = nearest / 6f;

            // Range fit: 1 at the ideal range, 0 when off by a full idealRange.
            float d = Vector3.Distance(p, PlayerPosition);
            float rangeFit = 1f - Mathf.Clamp01(Mathf.Abs(d - idealRange) / Mathf.Max(0.01f, idealRange));

            // Visibility from the player's eyes (only computed when it has a weight).
            float visible = 0f;
            if (wVisible > 0f)
            {
                Vector3 eye = PlayerPosition + Vector3.up * 1.6f;
                visible = Physics.Linecast(eye, p + Vector3.up, ~0, QueryTriggerInteraction.Ignore) ? 0.4f : 1f;
            }

            // Reachability veto: a point on a separate NavMesh island scores 20%.
            float reachable = 1f;
            if (agent.isOnNavMesh && NavMesh.CalculatePath(transform.position, p, agent.areaMask, scratchPath))
            {
                reachable = scratchPath.status == NavMeshPathStatus.PathComplete ? 1f : 0.2f;
            }

            return (wSpread * spread + wRange * rangeFit + wVisible * visible) * reachable;
        }

        // =====================================================================
        // Movement helpers and edge cases
        // =====================================================================

        bool EnsureOnNavMesh()
        {
            if (agent.isOnNavMesh)
            {
                return true;
            }

            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
                home = hit.position;
                return true;
            }

            Debug.LogError($"[IllusionBot] '{name}' is not on a NavMesh. Bake a NavMesh (NavMeshSurface > Bake) under the bot.", this);
            enabled = false;
            return false;
        }

        void MoveTo(Vector3 destination)
        {
            if (agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.SetDestination(destination);
                moveLegTimer = 0f;
            }
        }

        bool Arrived()
        {
            return agent.isOnNavMesh && !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.3f;
        }

        /// <summary>Stuck = wants to move but has barely moved for 1.5 s. Then pick a new nearby point.</summary>
        void CheckStuck(float dt)
        {
            bool tryingToMove = agent.isOnNavMesh && agent.hasPath && !agent.isStopped &&
                                agent.remainingDistance > agent.stoppingDistance + 0.5f;

            if (tryingToMove && agent.velocity.sqrMagnitude < 0.01f)
            {
                stuckTimer += dt;
                if (stuckTimer > 1.5f)
                {
                    stuckTimer = 0f;
                    MoveTo(RandomNavPoint(transform.position, 5f));
                }
            }
            else
            {
                stuckTimer = 0f;
            }
        }

        Vector3 RandomNavPoint(Vector3 centre, float radius)
        {
            for (int i = 0; i < 8; i++)
            {
                Vector3 raw = centre + RandomFlatDirection() * UnityEngine.Random.Range(radius * 0.3f, radius);
                if (NavMesh.SamplePosition(raw, out NavMeshHit hit, 2f, agent.areaMask))
                {
                    return hit.position;
                }
            }
            return transform.position;
        }

        static Vector3 RandomFlatDirection()
        {
            float a = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
        }

        void FaceTowards(Vector3 target, float dt)
        {
            Vector3 d = target - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude < 0.01f)
            {
                return;
            }
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), turnSpeed * dt);
        }

        void SetMood(BotMood next)
        {
            if (next == mood)
            {
                return;
            }
            mood = next;
            MoodChanged?.Invoke(next);
        }

        GameObject FindModelChild()
        {
            foreach (Transform child in transform)
            {
                if (child.GetComponentInChildren<Renderer>() != null)
                {
                    return child.gameObject;
                }
            }
            return null;
        }

        // =====================================================================
        // Debug (great for the demo video and the viva)
        // =====================================================================

        void OnGUI()
        {
            if (!showDebugPanel || utilityAI == null)
            {
                return;
            }

            GUILayout.BeginArea(new Rect(10, 10, 290, 250), GUI.skin.box);
            GUILayout.Label($"ILLUSION BOT   state: {CurrentState}   HP: {health.Current:0}/{health.MaxHealth:0}");
            GUILayout.Label($"Action: {CurrentAction}   Sees player: {CanSeePlayer}");

            for (int i = 0; i < IllusionUtility.ActionCount; i++)
            {
                float s = utilityAI.LastScores[i];
                string bar = new string('|', Mathf.RoundToInt(Mathf.Clamp01(s) * 20f));
                GUILayout.Label($"{(IllusionAction)i,-13} {s:0.00} {bar}");
            }

            GUILayout.Label($"Player fooled: {playerModel.FooledRate:P0}  (fake hits {playerModel.MirageHits} / real hits {playerModel.RealHits})");
            int active = mirages != null ? mirages.ActiveCount : 0;
            GUILayout.Label($"Mirages: {active} active / {DesiredMirageCount()} wanted");
            GUILayout.EndArea();
        }

        void OnDrawGizmosSelected()
        {
            Vector3 eye = transform.position + Vector3.up * eyeHeight;
            Gizmos.color = Color.yellow;
            Vector3 left = Quaternion.Euler(0f, -viewAngle * 0.5f, 0f) * transform.forward;
            Vector3 right = Quaternion.Euler(0f, viewAngle * 0.5f, 0f) * transform.forward;
            Gizmos.DrawLine(eye, eye + left * viewDistance);
            Gizmos.DrawLine(eye, eye + right * viewDistance);
            Gizmos.DrawWireSphere(transform.position, closeSenseRadius);

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(lastKnownPlayerPos, 0.4f);

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(Application.isPlaying ? home : transform.position, patrolRadius);
        }
    }
}
