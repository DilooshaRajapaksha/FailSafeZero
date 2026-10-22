using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Maze Ghost AI behavior script
/// Handles navigation, slow effect, and chase behavior in the maze.
/// Drives the Animator "Speed" float so the walk animation matches movement.
/// </summary>
public class MazeGhost : MonoBehaviour
{
    [Header("Chase Settings")]
    public Transform player;          // Drag player Transform here
    public float chaseSpeed = 6.0f;   // Normal chasing speed
    public float slowedSpeed = 3.0f;  // Speed when slowed by player
    public float stoppingDistance = 0.5f;

    [Header("Slow Effect")]
    public bool isSlowed = false;     // Current slow state
    public float slowDuration = 5.0f; // How long slow lasts
    private float slowTimer = 0.0f;

    [Header("Animation")]
    public Animator anim;
    private int speedHash;

    [Header("Navigation")]
    private NavMeshAgent agent;

    private void Awake()
    {
        // Get (or add) NavMeshAgent
        agent = GetComponent<NavMeshAgent>();
        if (agent == null)
            agent = gameObject.AddComponent<NavMeshAgent>();

        agent.speed = chaseSpeed;
        agent.stoppingDistance = stoppingDistance;
        agent.updateRotation = false; // rotation handled elsewhere / by agent

        // Cache animator hash for the "Speed" float parameter
        speedHash = Animator.StringToHash("Speed");

        // Auto-assign animator if not dragged in the Inspector
        if (anim == null)
            anim = GetComponent<Animator>();
    }

    private void Update()
    {
        // 1. Handle slow effect timer
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

        // 2. Chase player if player reference exists
        if (player == null || agent == null)
            return;

        agent.SetDestination(player.position);

        // 3. Update animator based on velocity (0 = idle, 1 = full walk)
        if (anim != null)
        {
            float velocityMagnitude = agent.velocity.magnitude;
            float currentSpeed = isSlowed ? slowedSpeed : chaseSpeed;
            float normalizedSpeed = currentSpeed > 0.0f
                ? Mathf.Clamp01(velocityMagnitude / currentSpeed)
                : 0.0f;
            anim.SetFloat(speedHash, normalizedSpeed);
        }
    }

    /// <summary>
    /// Set the slow state on the maze ghost
    /// </summary>
    /// <param name="slow">true to slow, false to resume normal speed</param>
    public void SetSlowed(bool slow)
    {
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

    /// <summary>
    /// Set player reference from other scripts
    /// </summary>
    public void SetPlayerReference(Transform newPlayer)
    {
        player = newPlayer;
    }
}
