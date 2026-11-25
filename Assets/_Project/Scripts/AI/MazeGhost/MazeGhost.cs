using System.Collections.Generic;
using FailSafeZero.AI.Illusion;
using FailSafeZero.AI.Illusion;
using UnityEngine;
using UnityEngine.AI;

public class MazeGhost : MonoBehaviour
{
    [Header("Chase Settings")]
    public Transform player;
    public float chaseSpeed = 6.0f;
    public float slowedSpeed = 3.0f;
    public float stoppingDistance = 1.4f;
    public float turnSpeed = 3.5f;

    [Header("Chase Activation")]
    public Collider mazeBounds;
    public float chaseTriggerDistance = 12f;
    public bool latchChase = true;

    [Header("Slow Effect")]
    public bool isSlowed = false;
    public float slowDuration = 5.0f;
    private float slowTimer = 0.0f;

    [Header("Attack")]
    public float attackDistance = 2.0f;
    public float attackDamage = 15f;
    public float attackHitTime = 0.35f;
    public float attackCooldown = 2.5f;
    private float nextAttackTime = 0.0f;
    private bool attackPending;

    [Header("Animation - Locomotion")]
    public Animator anim;
    public string speedParameter = "Speed";
    public string moveDirectionParameter = "MoveDirection";
    public string forwardParameter = "Forward";
    public string runParameter = "Run";
    public float moveThreshold = 0.1f;
    public float strafeThreshold = 0.35f;
    public float backwardThreshold = 0.35f;
    public float runThreshold = 0.75f;

    [Header("Animation - Overrides")]
    public string attackParameter = "Attack";
    public string jumpParameter = "Jump";
    public string fallParameter = "Fall";
    public string waitParameter = "WaitPose";
    public string dieParameter = "Die";
    public string die2Parameter = "Die2";

    [Header("Jump Triggers")]
    public float jumpCooldown = 4f;
    public float blockedTimeBeforeJump = 0.8f;
    public float playerHeightForJump = 0.8f;
    public float jumpToFallDelay = 0.45f;
    public float landDelay = 1.1f;

    [Header("Head Tracking")]
    [Tooltip("Head bone used for the face direction.")]
    public Transform headBone;
    [Tooltip("Visual root rotated around Y to aim the face. Rotating this instead of the head bone keeps the leg chains level.")]
    public Transform facePivot;
    public bool trackHeadToPlayer = true;
    [Tooltip("Local axis on the head bone that points out through the face. This rig uses local -Y.")]
    public Vector3 headFaceAxis = new Vector3(0f, -1f, 0f);
    public float headTurnSpeed = 90f;
    public float headMaxAngle = 180f;
    public float headAimHeight = 1.5f;

    private int speedHash;
    private int moveDirectionHash;
    private int forwardHash;
    private int runHash;
    private int attackHash;
    private int attackStateHash;
    private int jumpHash;
    private int fallHash;
    private int waitHash;
    private int dieHash;
    private int die2Hash;

    [Header("Navigation")]
    private NavMeshAgent agent;

    [Header("Hand-coded A* Pathfinding")]
    public MazeGhostAStar aStar;
    public bool useHandCodedAStar = true;
    [Range(1f, 2f)] public float aStarHeuristicWeight = 1f;
    public float aStarRepathInterval = 0.4f;
    public float aStarRepathDistance = 1.0f;
    public float aStarWaypointReachDistance = 0.6f;
    private List<Vector3> aStarPath;
    private int aStarWaypointIndex;
    private float nextAStarRepathTime;
    private Vector3 lastAStarTarget;
    private Vector3 activeAStarWaypoint;
    private bool hasAStarWaypoint;
    private float originalStoppingDistance;
    private bool chaseActivated;
    private bool isJumping;
    private bool isDead;
    private IDamageable playerDamage;
    private float nextJumpTime;
    private float blockedTimer;
    private float lastMoveDirection;
    private float lastForwardDirection;
    private bool locomotionActive;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (agent == null)
            agent = gameObject.AddComponent<NavMeshAgent>();

        agent.speed = chaseSpeed;
        originalStoppingDistance = stoppingDistance;
        agent.stoppingDistance = useHandCodedAStar ? 0f : originalStoppingDistance;
        agent.updateRotation = false;

        speedHash = Animator.StringToHash(speedParameter);
        moveDirectionHash = Animator.StringToHash(moveDirectionParameter);
        forwardHash = Animator.StringToHash(forwardParameter);
        runHash = Animator.StringToHash(runParameter);
        attackHash = Animator.StringToHash(attackParameter);
        attackStateHash = Animator.StringToHash("Attack");
        jumpHash = Animator.StringToHash(jumpParameter);
        fallHash = Animator.StringToHash(fallParameter);
        waitHash = Animator.StringToHash(waitParameter);
        dieHash = Animator.StringToHash(dieParameter);
        die2Hash = Animator.StringToHash(die2Parameter);

        if (anim == null)
            anim = GetComponentInChildren<Animator>();

        if (headBone == null)
            headBone = transform.Find("Spider_3/Spider_Armature/Body.003/Body.002/Body");

        // Rotate the visual root, never the head bone: the leg chains are children of the face bone.
        if (facePivot == null)
            facePivot = transform.Find("Spider_3");

        // Fall back to the maze floor collider if it was not assigned in the Inspector.
        if (mazeBounds == null)
        {
            GameObject mazeFloor = GameObject.Find("Maze_floor");
            if (mazeFloor != null)
                mazeBounds = mazeFloor.GetComponent<Collider>();
        }

        if (useHandCodedAStar)
        {
            if (aStar == null)
                aStar = GetComponent<MazeGhostAStar>();
            if (aStar == null)
                aStar = gameObject.AddComponent<MazeGhostAStar>();

            aStar.Configure(mazeBounds, transform);
            aStar.heuristicWeight = aStarHeuristicWeight;
        }
    }

    private void Update()
    {
        if (isDead)
        {
            ResetLocomotion();
            SetAnimator(0f, 0f, 0f, false);
            return;
        }

        if (isSlowed)
        {
            slowTimer -= Time.deltaTime;
            if (slowTimer <= 0.0f)
            {
                isSlowed = false;
                slowTimer = 0.0f;
                if (agent != null) agent.speed = chaseSpeed;
            }
        }

        if (player == null || agent == null)
            return;

        bool playerNearMaze = IsPlayerNearMaze();
        if (!chaseActivated && !playerNearMaze)
        {
            agent.isStopped = false;
            if (agent.hasPath || agent.pathPending)
                agent.ResetPath();

            ResetAStarPath();
            ResetLocomotion();
            SetAnimator(0f, 0f, 0f, false);
            blockedTimer = 0f;
            return;
        }

        chaseActivated = latchChase || playerNearMaze;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        float effectiveStoppingDistance = useHandCodedAStar && aStar != null
            ? stoppingDistance
            : agent.stoppingDistance;

        bool holdPosition = distanceToPlayer <= effectiveStoppingDistance + 0.05f;
        agent.isStopped = holdPosition;

        if (holdPosition)
        {
            if (agent.hasPath || agent.pathPending)
                agent.ResetPath();
            ResetAStarPath();
        }
        else
        {
            // Hand-coded A* chooses the route; NavMeshAgent only follows local waypoints.
            bool followedAStar = FollowHandCodedAStarPath(player.position);
            if (!followedAStar)
                agent.SetDestination(player.position);
        }

        // Read travel direction before turning so cornering uses the strafe clips.
        Vector3 planarVelocity = Vector3.ProjectOnPlane(agent.velocity, Vector3.up);
        float velocityMagnitude = planarVelocity.magnitude;
        float normalizedSpeed = Mathf.Clamp01(velocityMagnitude / Mathf.Max(0.01f, chaseSpeed));

        float moveDirection = lastMoveDirection;
        float forwardDirection = lastForwardDirection;
        if (normalizedSpeed > moveThreshold && planarVelocity.sqrMagnitude > 0.0001f)
        {
            Vector3 moveDir = planarVelocity.normalized;
            float sideways = Vector3.Dot(moveDir, transform.right);
            float forward = Vector3.Dot(moveDir, transform.forward);

            moveDirection = 0f;
            forwardDirection = 0f;
            if (Mathf.Abs(sideways) >= strafeThreshold)
                moveDirection = Mathf.Sign(sideways);
            else if (forward <= -backwardThreshold)
                forwardDirection = -1f;
            else
                forwardDirection = 1f;

            lastMoveDirection = moveDirection;
            lastForwardDirection = forwardDirection;
        }

        bool running = normalizedSpeed >= runThreshold && !isSlowed && !isJumping;
        SetAnimator(GetLocomotionSpeed(normalizedSpeed), moveDirection, forwardDirection, running);

        CheckAttackImpact();

        if (normalizedSpeed > moveThreshold)
        {
            Quaternion targetRotation = Quaternion.LookRotation(planarVelocity.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                Mathf.Clamp01(turnSpeed * Time.deltaTime)
            );
        }

        if (distanceToPlayer <= attackDistance)
            TriggerAttack();

        UpdateJumpTriggers(distanceToPlayer, velocityMagnitude);
    }

    private bool FollowHandCodedAStarPath(Vector3 target)
    {
        if (!useHandCodedAStar || aStar == null)
            return false;

        if (!agent.isOnNavMesh)
            return true;

        aStar.seeker = transform;
        if (aStar.mazeBounds != mazeBounds)
            aStar.Configure(mazeBounds, transform);

        bool targetMovedEnough = Vector3.Distance(target, lastAStarTarget) > aStarRepathDistance;
        bool needRepath = aStarPath == null ||
                          aStarWaypointIndex >= aStarPath.Count ||
                          Time.time >= nextAStarRepathTime ||
                          targetMovedEnough;

        if (needRepath)
        {
            List<Vector3> newPath = aStar.FindPath(transform.position, target);
            if (newPath == null || newPath.Count == 0)
            {
                ResetAStarPath();
                return false;
            }

            aStarPath = newPath;
            aStarWaypointIndex = 0;
            lastAStarTarget = target;
            nextAStarRepathTime = Time.time + aStarRepathInterval;
            hasAStarWaypoint = false;
        }

        while (aStarWaypointIndex < aStarPath.Count - 1 &&
               Vector3.Distance(transform.position, aStarPath[aStarWaypointIndex]) <= aStarWaypointReachDistance)
        {
            aStarWaypointIndex++;
        }

        Vector3 waypoint = aStarPath[aStarWaypointIndex];
        if (!hasAStarWaypoint || Vector3.Distance(activeAStarWaypoint, waypoint) > 0.01f)
        {
            agent.isStopped = false;
            agent.SetDestination(waypoint);
            activeAStarWaypoint = waypoint;
            hasAStarWaypoint = true;
        }

        return true;
    }

    private void ResetAStarPath()
    {
        aStarPath = null;
        aStarWaypointIndex = 0;
        nextAStarRepathTime = 0f;
        lastAStarTarget = Vector3.zero;
        activeAStarWaypoint = Vector3.zero;
        hasAStarWaypoint = false;
    }


    private void LateUpdate()
    {
        if (!trackHeadToPlayer || headBone == null || facePivot == null || player == null || isDead)
            return;

        Vector3 aimPoint = player.position + Vector3.up * headAimHeight;
        Vector3 toPlayer = Vector3.ProjectOnPlane(aimPoint - headBone.position, Vector3.up);
        if (toPlayer.sqrMagnitude < 0.04f)
            return;
        toPlayer.Normalize();

        Vector3 currentFace = Vector3.ProjectOnPlane(
            headBone.TransformDirection(headFaceAxis.normalized), Vector3.up);
        if (currentFace.sqrMagnitude < 0.0001f)
            return;
        currentFace.Normalize();

        float signedAngle = Mathf.Atan2(
            Vector3.Dot(Vector3.up, Vector3.Cross(currentFace, toPlayer)),
            Vector3.Dot(currentFace, toPlayer)
        ) * Mathf.Rad2Deg;

        signedAngle = Mathf.Clamp(signedAngle, -headMaxAngle, headMaxAngle);
        float targetYaw = facePivot.localEulerAngles.y + signedAngle;
        Quaternion targetRotation = Quaternion.Euler(0f, targetYaw, 0f);
        facePivot.localRotation = Quaternion.RotateTowards(
            facePivot.localRotation,
            targetRotation,
            headTurnSpeed * Time.deltaTime
        );
    }

    private void UpdateJumpTriggers(float distanceToPlayer, float velocityMagnitude)
    {
        if (isJumping || Time.time < nextJumpTime)
            return;

        bool playerAbove = player.position.y - transform.position.y > playerHeightForJump;
        if (playerAbove)
        {
            TriggerJump();
            blockedTimer = 0f;
            return;
        }

        // If the agent wants to move but cannot, hop once as an obstacle workaround.
        bool blocked = distanceToPlayer > attackDistance && velocityMagnitude < 0.15f;
        if (blocked)
        {
            blockedTimer += Time.deltaTime;
            if (blockedTimer >= blockedTimeBeforeJump)
            {
                TriggerJump();
                blockedTimer = 0f;
            }
        }
        else
        {
            blockedTimer = 0f;
        }
    }

    private float GetLocomotionSpeed(float normalizedSpeed)
    {
        if (!locomotionActive && normalizedSpeed > moveThreshold)
            locomotionActive = true;
        else if (locomotionActive && normalizedSpeed < moveThreshold * 0.45f)
            locomotionActive = false;

        if (!locomotionActive)
            return 0f;

        return Mathf.Max(normalizedSpeed, moveThreshold * 1.5f);
    }

    private void ResetLocomotion()
    {
        locomotionActive = false;
        lastMoveDirection = 0f;
        lastForwardDirection = 0f;
    }

    private void SetAnimator(float speed, float direction, float forward, bool running)
    {
        if (anim == null)
            return;

        anim.SetFloat(speedHash, speed);
        anim.SetFloat(moveDirectionHash, direction);
        anim.SetFloat(forwardHash, forward);
        anim.SetBool(runHash, running);
    }

    public void TriggerAttack()
    {
        if (isDead || Time.time < nextAttackTime || anim == null)
            return;

        nextAttackTime = Time.time + attackCooldown;
        attackPending = true;
        anim.ResetTrigger(jumpHash);
        anim.SetTrigger(attackHash);
    }

    private void CheckAttackImpact()
    {
        if (!attackPending || anim == null)
            return;

        AnimatorStateInfo state = anim.GetCurrentAnimatorStateInfo(0);
        if (state.shortNameHash != attackStateHash || state.normalizedTime < attackHitTime)
            return;

        attackPending = false;

        if (player == null || Vector3.Distance(transform.position, player.position) > attackDistance + 0.5f)
            return;

        if (playerDamage == null)
            playerDamage = player.GetComponentInParent<IDamageable>();

        if (playerDamage != null)
            playerDamage.ReceiveHit(attackDamage, player.position, gameObject);
        else
            player.SendMessage("TakeDamage", attackDamage, SendMessageOptions.DontRequireReceiver);
    }

    public void TriggerJump()
    {
        if (isDead || isJumping || Time.time < nextJumpTime || anim == null)
            return;

        isJumping = true;
        nextJumpTime = Time.time + jumpCooldown;
        anim.ResetTrigger(fallHash);
        anim.SetTrigger(jumpHash);

        Invoke(nameof(TriggerFall), jumpToFallDelay);
        Invoke(nameof(Land), landDelay);
    }

    public void TriggerFall()
    {
        if (isDead || anim == null)
            return;

        anim.SetTrigger(fallHash);
    }

    public void Land()
    {
        isJumping = false;
        blockedTimer = 0f;
    }

    public void TriggerWait()
    {
        if (isDead || anim == null)
            return;

        anim.SetTrigger(waitHash);
    }

    public void TriggerDie()
    {
        if (isDead)
            return;

        isDead = true;
        isJumping = false;
        ResetLocomotion();
        ResetAStarPath();
        CancelInvoke();

        if (agent != null)
            agent.isStopped = true;

        if (anim != null)
            anim.SetTrigger(dieHash);
    }

    public void TriggerDie2()
    {
        if (isDead)
            return;

        isDead = true;
        isJumping = false;
        ResetLocomotion();
        ResetAStarPath();
        CancelInvoke();

        if (agent != null)
            agent.isStopped = true;

        if (anim != null)
            anim.SetTrigger(die2Hash);
    }

    public void OnDefeatedAnimation()
    {
        TriggerDie();
    }

    private bool IsPlayerNearMaze()
    {
        if (player == null)
            return false;

        // No bounds assigned means keep the old always-chase behavior.
        if (mazeBounds == null)
            return true;

        return mazeBounds.bounds.SqrDistance(player.position) <=
               chaseTriggerDistance * chaseTriggerDistance;
    }

    public void SetSlowed(bool slow)
    {
        if (isDead)
            return;

        isSlowed = slow;

        if (agent == null)
            return;

        if (slow)
        {
            agent.speed = slowedSpeed;
            slowTimer = slowDuration;
            Debug.Log("Maze Ghost slowed for " + slowDuration + " seconds");
        }
        else
        {
            agent.speed = chaseSpeed;
        }
    }

    public void SetPlayerReference(Transform newPlayer)
    {
        player = newPlayer;
    }

    private void OnDisable()
    {
        CancelInvoke();
    }
}
