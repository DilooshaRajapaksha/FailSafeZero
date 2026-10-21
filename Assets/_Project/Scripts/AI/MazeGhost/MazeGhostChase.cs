using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Makes bot chase player smoothly with rotation toward player (eye tracking)
/// Disables agent's default rotation, handles manual smoothing toward player
/// </summary>
public class BotChase : MonoBehaviour
{
    public Transform player; // Drag player transform here in Inspector
    private NavMeshAgent agent;
    private Animator anim;

    [Header("Looking Settings")]
    public bool lookAtPlayer = true;  // Make bot look at player
    public float lookSmoothTime = 0.1f;  // Smooth rotation time
    private Vector3 _lookVelocity; // Smoothing velocity

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponent<Animator>();

        // Disable agent's default rotation control
        agent.updateRotation = false;

        // Set agent settings
        agent.speed = 6.0f;  // Normal ghost speed
        agent.stoppingDistance = 0.5f;
    }

    void Update()
    {
        if (player == null) return;

        // 1. Set destination to player position (chase)
        agent.SetDestination(player.position);

        // 2. Rotate toward player (eye tracking)
        if (lookAtPlayer && player != null)
        {
            // Calculate direction to player
            Vector3 direction = (player.position - transform.position).normalized;
            
            // Smoothly rotate toward player using Quaternion.Slerp with velocity tracking
            // Instead of SmoothDamp (which may not be available), we use Slerp with time damping
            float smooth = 1f / (lookSmoothTime * 10f + 1f); // Calculated smooth factor
            transform.rotation = Quaternion.Slerp(
                transform.rotation, 
                Quaternion.LookRotation(direction), 
                Time.deltaTime * (1f / lookSmoothTime)
            );
        }
        else
        {
            // OPTIONAL: Fallback to velocity-based rotation if not looking at player
            if (agent.velocity.sqrMagnitude > 0.1f)
            {
                Quaternion lookRotation = Quaternion.LookRotation(agent.velocity.normalized);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, 
                    lookRotation, 
                    Time.deltaTime * 5f
                );
            }
        }

        // 3. Update animator speed parameter
        if (anim != null)
        {
            float velocityMagnitude = agent.velocity.magnitude;
            anim.SetFloat("Speed", velocityMagnitude / 6.0f); // Normalized to 0-1
        }
    }

    // Call this to set the player reference from other scripts
    public void SetPlayerReference(Transform playerTransform)
    {
        player = playerTransform;
    }
}