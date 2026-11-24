using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Maze Ghost AI behaviour.
/// Handles navigation, slow effect and drives every animation in the ghost controller:
///   Speed, MoveDirection, Forward, Run - locomotion selection
///   Attack, Jump, Fall, WaitPose       - one-shot overrides
///   Die, Die2                          - defeat overrides
/// </summary>
public class MazeGhost : MonoBehaviour
{
    [Header("Chase Settings")]
    public Transform player;          // Drag player Transform here
    public float chaseSpeed = 6.0f;   // Normal chasing speed
    public float slowedSpeed = 3.0f;  // Speed when slowed by player
    public float stoppingDistance = 0.5f;
    public float turnSpeed = 3.5f;    // Slow turn lets strafe/back animations show while cornering

    [Header("Chase Activation")]
    public Collider mazeBounds;       // Assign Maze_floor's collider
    public float chaseTriggerDistance = 12f; // Chase when player is this close to the maze bounds
    public bool latchChase = true;    // Once triggered, keep chasing

    [Header("Slow Effect")]
    public bool isSlowed = false;     // Current slow state
    public float slowDuration = 5.0f; // How long slow lasts
    private float slowTimer = 0.0f;

    [Header("Attack")]
    public float attackDistance = 2.0f;
    public float attackCooldown = 2.5f;
    private float nextAttackTime = 0.0f;

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

    [Header("Trick Triggers")]
    public float jumpCooldown = 4f;
    public float blockedTimeBeforeJump = 0.8f;
    public float playerHeightForJump = 0.8f;
    public float jumpToFallDelay = 0.45f;
    public float landDelay = 1.1f;

    [Header("Head Tracking")]
    [Tooltip("The head/face bone. Auto-found as Spider_3/Spider_Armature/Body.003/Body.002/Body if empty.")]
    public Transform headBone;
    [Tooltip("Visual root rotated around Y to aim the face. Rotating this instead of the head bone keeps the leg chains level.")]
    public Transform facePivot;
    public bool trackHeadToPlayer = true;
    [Tooltip("Fallback local axis on the head bone that points out through the face. This rig uses local -Y.")]
    public Vector3 headFaceAxis = new Vector3(0f, -1f, 0f);
    public float headTurnSpeed = 90f;
    public float headMaxAngle = 180f;
    public float headAimHeight = 1.5f;

    private int speedHash;
    private int moveDirectionHash;
    private int forwardHash;
    private int runHash;
    private int attackHash;
    private int jumpHash;
    private int fallHash;
    private int waitHash;
    private int dieHash;
    private int die2Hash;

    [Header("Navigation")]
    private NavMeshAgent agent;
    private bool chaseActivated;
    private bool isJumping;
    private bool isDead;
    private float nextJumpTime;
    private float blockedTimer;

    private void Awake()
    {
        // Get (or add) NavMeshAgent
        agent = GetComponent<NavMeshAgent>();
        if (agent == null)
            agent = gameObject.AddComponent<NavMeshAgent>();

        agent.speed = chaseSpeed;
        agent.stoppingDistance = stoppingDistance;
        agent.updateRotation = false; // rotation is smoothed below

        // Cache animator hashes.
        speedHash = Animator.StringToHash(speedParameter);
        moveDirectionHash = Animator.StringToHash(moveDirectionParameter);
        forwardHash = Animator.StringToHash(forwardParameter);
        runHash = Animator.StringToHash(runParameter);
        attackHash = Animator.StringToHash(attackParameter);
        jumpHash = Animator.StringToHash(jumpParameter);
        fallHash = Animator.StringToHash(fallParameter);
        waitHash = Animator.StringToHash(waitParameter);
        dieHash = Animator.StringToHash(dieParameter);
        die2Hash = Animator.StringToHash(die2Parameter);

        // Auto-assign animator if not dragged in the Inspector.
        if (anim == null)
            anim = GetComponentInChildren<Animator>();

        // Auto-find the head bone if it was not assigned in the Inspector.
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
    }

    private void Update()
    {
        if (isDead)
        {
            SetAnimator(0f, 0f, 0f, false);
            return;
        }

        // Handle slow effect timer.
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
            if (agent.hasPath || agent.pathPending)
                agent.ResetPath();

            SetAnimator(0f, 0f, 0f, false);
            blockedTimer = 0f;
            return;
        }

        chaseActivated = latchChase || playerNearMaze;
        agent.SetDestination(player.position);

        // Work out movement direction BEFORE rotating. Slow turning is what exposes
        // the left/right/backward clips while the ghost cuts corners.
        Vector3 planarVelocity = Vector3.ProjectOnPlane(agent.velocity, Vector3.up);
        float velocityMagnitude = planarVelocity.magnitude;
        float normalizedSpeed = Mathf.Clamp01(velocityMagnitude / Mathf.Max(0.01f, chaseSpeed));

        float moveDirection = 0f;
        float forwardDirection = 0f;
        if (normalizedSpeed > moveThreshold && planarVelocity.sqrMagnitude > 0.0001f)
        {
            Vector3 moveDir = planarVelocity.normalized;
            float sideways = Vector3.Dot(moveDir, transform.right);
            float forward = Vector3.Dot(moveDir, transform.forward);

            if (Mathf.Abs(sideways) >= strafeThreshold)
                moveDirection = Mathf.Sign(sideways);
            else if (forward <= -backwardThreshold)
                forwardDirection = -1f;
            else
                forwardDirection = 1f;
        }

        bool running = normalizedSpeed >= runThreshold && !isSlowed && !isJumping;
        SetAnimator(normalizedSpeed, moveDirection, forwardDirection, running);

        // Smoothly turn toward the direction of travel.
        if (planarVelocity.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(planarVelocity.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                turnSpeed * Time.deltaTime
            );
        }

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        if (distanceToPlayer <= attackDistance)
            TriggerAttack();

        UpdateTrickTriggers(distanceToPlayer, velocityMagnitude);
    }

    /// <summary>
    /// Keeps the face aimed at the player by yawing the visual root. The head bone cannot be
    /// rotated directly because the leg chains are parented to it, which pushes legs through the floor.
    /// </summary>
    private void LateUpdate()
    {
        if (!trackHeadToPlayer || headBone == null || facePivot == null || player == null || isDead)
            return;

        Vector3 aimPoint = player.position + Vector3.up * headAimHeight;
        Vector3 toPlayer = Vector3.ProjectOnPlane(aimPoint - headBone.position, Vector3.up);
        if (toPlayer.sqrMagnitude < 0.0001f)
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
        facePivot.localRotation = Quaternion.Slerp(
            facePivot.localRotation,
            targetRotation,
            Mathf.Clamp01(headTurnSpeed * Time.deltaTime)
        );
    }

    private void UpdateTrickTriggers(float distanceToPlayer, float velocityMagnitude)
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

    private void SetAnimator(float speed, float direction, float forward, bool running)
    {
        if (anim == null)
            return;

        anim.SetFloat(speedHash, speed);
        anim.SetFloat(moveDirectionHash, direction);
        anim.SetFloat(forwardHash, forward);
        anim.SetBool(runHash, running);
    }

    /// <summary>Fire the Attack animation (respects the cooldown).</summary>
    public void TriggerAttack()
    {
        if (isDead || Time.time < nextAttackTime || anim == null)
            return;

        nextAttackTime = Time.time + attackCooldown;
        anim.ResetTrigger(jumpHash);
        anim.SetTrigger(attackHash);
    }

    /// <summary>Play Jump, then automatically flow into Fall and land.</summary>
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

    /// <summary>Play the Fall animation on its own.</summary>
    public void TriggerFall()
    {
        if (isDead || anim == null)
            return;

        anim.SetTrigger(fallHash);
    }

    /// <summary>Internal landing callback used after the jump animation.</summary>
    public void Land()
    {
        isJumping = false;
        blockedTimer = 0f;
    }

    /// <summary>Play the idle wait pose once.</summary>
    public void TriggerWait()
    {
        if (isDead || anim == null)
            return;

        anim.SetTrigger(waitHash);
    }

    /// <summary>Play the first death animation.</summary>
    public void TriggerDie()
    {
        if (isDead)
            return;

        isDead = true;
        isJumping = false;
        CancelInvoke();

        if (agent != null)
            agent.isStopped = true;

        if (anim != null)
            anim.SetTrigger(dieHash);
    }

    /// <summary>Play the alternate death animation.</summary>
    public void TriggerDie2()
    {
        if (isDead)
            return;

        isDead = true;
        isJumping = false;
        CancelInvoke();

        if (agent != null)
            agent.isStopped = true;

        if (anim != null)
            anim.SetTrigger(die2Hash);
    }

    /// <summary>Called by EnemyHealth through BroadcastMessage when the ghost is defeated.</summary>
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

    /// <summary>Set the slow state on the maze ghost.</summary>
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

    /// <summary>Set player reference from other scripts.</summary>
    public void SetPlayerReference(Transform newPlayer)
    {
        player = newPlayer;
    }

    private void OnDisable()
    {
        CancelInvoke();
    }
}
