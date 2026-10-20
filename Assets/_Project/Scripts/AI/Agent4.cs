using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// SE3062 Assignment - Agent 4
/// Uses A* pathfinding via Unity NavMeshAgent
/// Goes beyond basic uninformed search with decision-making architecture
/// </summary>
public class Agent4 : MonoBehaviour
{
    [Header("References")]
    public Transform player; // Assign in Inspector or find via tag
    public NavMeshAgent navAgent; // Auto-added if missing
    
    [Header("Behavior Settings")]
    public float chaseRadius = 20f;  // Chase player when within 20 units
    public float patrolRadius = 15f; // Patrol within 15 units when no player
    public float patrolSpeed = 3f;   // Normal walking speed
    public float chaseSpeed = 6f;    // Running speed when chasing
    public float updateInterval = 0.5f; // How often to reevaluate decision
    
    void Awake()
    {
        navAgent = GetComponent<NavMeshAgent>();
        if (navAgent == null)
            navAgent = gameObject.AddComponent<NavMeshAgent>();
        
        navAgent.speed = patrolSpeed;
        navAgent.stoppingDistance = 1.0f;
        navAgent.updateRotation = true;
    }
    
    void Start()
    {
        if (player == null)
            player = GameObject.FindGameObjectWithTag("Player")?.transform;
        
        InvokeRepeating("ReevaluateDecision", 0f, updateInterval);
    }
    
    void ReevaluateDecision()
    {
        if (player == null) return;
        
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        
        if (distanceToPlayer <= chaseRadius)
        {
            navAgent.SetDestination(player.position);
            navAgent.speed = chaseSpeed;
        }
        else
        {
            Patrol();
        }
    }
    
    void Patrol()
    {
        Vector3 randomPos = transform.position + Random.insideUnitSphere * patrolRadius;
        if (NavMesh.SamplePosition(randomPos, out NavMeshHit hit, patrolRadius, NavMesh.AllAreas))
        {
            navAgent.SetDestination(hit.position);
        }
        else
        {
            Invoke("Patrol", 1f);
        }
    }
    
    void Update()
    {
        if (navAgent.pathStatus == NavMeshPathStatus.PathInvalid || 
            navAgent.remainingDistance > 10f && navAgent.velocity.sqrMagnitude < 0.1f)
        {
            Invoke("RecoverFromStuck", 3f);
        }
    }
    
    void RecoverFromStuck()
    {
        Patrol();
        navAgent.ResetPath();
    }
    
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, chaseRadius);
        
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, patrolRadius);
    }
}
