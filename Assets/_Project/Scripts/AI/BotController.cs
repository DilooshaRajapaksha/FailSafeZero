using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Bot controller - handles slow effect and movement
/// </summary>
public class BotController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float startSpeed = 3.0f;      // Normal speed for bot
    public float slowedSpeed = 1.0f;     // Slow speed (3 * 0.2 = 1)
    
    [Header("Slow Effect")]
    public bool isSlowed = false;        // Is currently slowed?
    public float originalSpeed = 3.0f; // Original speed before slow (for restoration)
    
    private NavMeshAgent _nav;
    
    void Start()
    {
        _nav = GetComponent<NavMeshAgent>();
        originalSpeed = startSpeed;
    }
    
    void Update()
    {
        // Apply slow effect
        if (isSlowed)
        {
            _nav.speed = slowedSpeed;
            // Optional: make bot look "scary" - red outline, etc.
        }
        else
        {
            _nav.speed = originalSpeed;
        }
    }
}