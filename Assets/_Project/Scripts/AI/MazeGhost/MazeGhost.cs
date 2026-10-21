using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Maze Ghost AI behavior script
/// Handles navigation, slow effect, and chase behavior in the maze
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
        // Get components
        agent = GetComponent<NavMeshAgent>();
        agent.speed = chaseSpeed;
        agent.stoppingDistance = stoppingDistance;
        
        // Cache animator hash
        speedHash = Animator.StringToHash("Speed");
        
        // Auto-assign animator if not dragged
        if (anim == null) anim = GetComponent<Animator>();
    }
    
    private void Start()
    {
        // Ensure agent settings are correct
        if (agent != null)
        {
            agent.updateRotation = false; // We'll handle rotation manually
        }
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
                // Reset speed when timer ends
                if (agent != null) agent.speed = chaseSpeed;
                if (anim != null) anim.SetFloat(speedHash, 0.0f);
            }
        }
        
        // 2. Chase player if player reference exists
        if (player != null && agent != null)
        {
            // Set destination to player
            agent.SetDestination(player.position);
            
            // Update animator based on velocity
            if (anim != null)
            {
                float velocityMagnitude = agent.velocity.magnitude;
                // Normalize speed (0 to 1 range)
                float normalizedSpeed = velocityMagnitude / chaseSpeed;
                anim.SetFloat(speedHash, Mathf.Clamp01(normalizedSpeed));
            }
        }
    }
    
    /// <summary>
    /// Set the slow state on the maze ghost
    /// </summary>
    /// <param>slow - true to slow, false to resume normal speed</param>
    public void SetSlowed(bool slow)
    {
        isSlowed = slow;
        
        if (slow && agent != null)
        {
            // Apply slow speed
            agent.speed = slowedSpeed;
            slowTimer = slowDuration;
            
            // Update animator to show slowed state
            if (anim != null)
            {
                anim.SetFloat(speedHash, slowedSpeed / chaseSpeed);
            }
            
            Debug.Log("Maze Ghost slowed for " + slowDuration + " seconds");
        }
        else if (!slow && agent != null)
        {
            // Resume normal speed
            agent.speed = chaseSpeed;
            
            // Update animator
            if (anim != null)
            {
                anim.SetFloat(speedHash, 0.0f);
            }
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