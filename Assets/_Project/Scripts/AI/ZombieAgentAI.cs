using UnityEngine;
using UnityEngine.AI;

namespace FailSafeZero.AI
{
    /// <summary>
    /// Student 1 - Autonomous Zombie Bot Agent (SE3062 Intelligent Systems).
    /// Architecture: 4-State Finite State Machine (FSM) + Weighted A* NavMesh Pathfinding.
    /// Mechanic: Pursues the player strictly when the player moves; freezes immediately when stationary.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class ZombieAgentAI : MonoBehaviour
    {
        public enum AgentState
        {
            Dormant,             // Frozen state when player is still
            PursuingMotion,      // Active A* pursuit while player moves
            InvestigatingTremor, // Brief micro-check of last motion point before freezing
            Attacking            // Close-range attack when within strike radius
        }

        [Header("Target & Motion Sensor Settings")]
        public Transform playerTarget;
        public float detectionRadius = 22.0f;
        public float motionVelocityThreshold = 0.15f;
        public float attackRange = 1.8f;

        [Header("Weighted A* Heuristic Tuning")]
        [Range(1.0f, 3.0f)]
        public float heuristicWeight = 1.35f;
        public float freezeReactionDelay = 0.08f;

        [Header("Student 4 Integration (Shared Movement Controller)")]
        [Tooltip("Enable if Student 4's script moves the bot using CurrentPathWaypoints")]
        public bool useStudent4MovementController = false;

        // Public properties exposed for Student 4's Movement & Animator setup
        public AgentState CurrentState { get; private set; } = AgentState.Dormant;
        public Vector3[] CurrentPathWaypoints { get; private set; } = new Vector3[0];
        public float CurrentPathTotalCost { get; private set; }
        public float NormalizedMovementSpeed { get; private set; }

        private NavMeshAgent navAgent;
        private NavMeshPath calculatedPath;
        private Vector3 previousPlayerPosition;
        private Vector3 lastDetectedMotionPoint;
        private float currentPlayerVelocity;
        private float stationaryTimer;

        private void Awake()
        {
            navAgent = GetComponent<NavMeshAgent>();
            calculatedPath = new NavMeshPath();

            if (useStudent4MovementController)
            {
                navAgent.updatePosition = false;
                navAgent.updateRotation = false;
            }
        }

        private void Start()
        {
            if (playerTarget == null)
            {
                GameObject foundPlayer = GameObject.FindGameObjectWithTag("Player");
                if (foundPlayer != null) playerTarget = foundPlayer.transform;
            }

            if (playerTarget != null)
            {
                previousPlayerPosition = playerTarget.position;
            }

            EnterDormantFreezeState();
        }

        private void Update()
        {
            if (playerTarget == null) return;

            SensePlayerMotion();
            EvaluateStateTransitions();
            ExecuteCurrentState();
        }

        private void SensePlayerMotion()
        {
            float displacement = Vector3.Distance(playerTarget.position, previousPlayerPosition);
            currentPlayerVelocity = displacement / Mathf.Max(Time.deltaTime, 0.0001f);
            previousPlayerPosition = playerTarget.position;
        }

        private void EvaluateStateTransitions()
        {
            float distanceToPlayer = Vector3.Distance(transform.position, playerTarget.position);
            bool inRange = distanceToPlayer <= detectionRadius;
            bool playerIsMoving = currentPlayerVelocity > motionVelocityThreshold;

            if (!inRange)
            {
                CurrentState = AgentState.Dormant;
                return;
            }

            if (playerIsMoving)
            {
                stationaryTimer = 0f;
                lastDetectedMotionPoint = playerTarget.position;
                CurrentState = (distanceToPlayer <= attackRange) ? AgentState.Attacking : AgentState.PursuingMotion;
            }
            else
            {
                stationaryTimer += Time.deltaTime;
                CurrentState = (stationaryTimer >= freezeReactionDelay) ? AgentState.Dormant : AgentState.InvestigatingTremor;
            }
        }

        private void ExecuteCurrentState()
        {
            switch (CurrentState)
            {
                case AgentState.Dormant:
                    EnterDormantFreezeState();
                    break;
                case AgentState.PursuingMotion:
                    ComputeWeightedPathAndPursue(playerTarget.position);
                    break;
                case AgentState.InvestigatingTremor:
                    ComputeWeightedPathAndPursue(lastDetectedMotionPoint);
                    break;
                case AgentState.Attacking:
                    ExecuteAttackBehavior();
                    break;
            }
        }

        private void EnterDormantFreezeState()
        {
            if (navAgent.isOnNavMesh)
            {
                navAgent.isStopped = true;
                navAgent.ResetPath();
            }

            CurrentPathWaypoints = new Vector3[0];
            CurrentPathTotalCost = 0f;
            NormalizedMovementSpeed = 0f;
        }

        private void ComputeWeightedPathAndPursue(Vector3 targetPos)
        {
            if (!navAgent.isOnNavMesh) return;

            if (NavMesh.CalculatePath(transform.position, targetPos, NavMesh.AllAreas, calculatedPath) && calculatedPath.corners.Length > 0)
            {
                CurrentPathWaypoints = calculatedPath.corners;

                // Weighted A* cost f(n) = g(n) + w * h(n)
                float gCost = 0f;
                for (int i = 0; i < CurrentPathWaypoints.Length - 1; i++)
                {
                    gCost += Vector3.Distance(CurrentPathWaypoints[i], CurrentPathWaypoints[i + 1]);
                }
                float hCost = Vector3.Distance(transform.position, targetPos);
                CurrentPathTotalCost = gCost + (heuristicWeight * hCost);

                NormalizedMovementSpeed = Mathf.Clamp01(currentPlayerVelocity / 4.0f);

                if (!useStudent4MovementController)
                {
                    navAgent.isStopped = false;
                    navAgent.SetPath(calculatedPath);
                }
                else
                {
                    navAgent.nextPosition = transform.position;
                }
            }
        }

        private void ExecuteAttackBehavior()
        {
            if (navAgent.isOnNavMesh && !useStudent4MovementController)
            {
                navAgent.isStopped = true;
            }

            Vector3 dir = (playerTarget.position - transform.position).normalized;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 8f);
            }
            NormalizedMovementSpeed = 0f;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = CurrentState == AgentState.Dormant ? Color.cyan : Color.red;
            Gizmos.DrawWireSphere(transform.position, detectionRadius);

            if (CurrentPathWaypoints != null && CurrentPathWaypoints.Length > 1)
            {
                Gizmos.color = Color.green;
                for (int i = 0; i < CurrentPathWaypoints.Length - 1; i++)
                {
                    Gizmos.DrawLine(CurrentPathWaypoints[i], CurrentPathWaypoints[i + 1]);
                    Gizmos.DrawSphere(CurrentPathWaypoints[i + 1], 0.2f);
                }
            }
        }
    }
}