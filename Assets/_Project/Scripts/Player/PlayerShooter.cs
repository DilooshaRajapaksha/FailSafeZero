using UnityEngine;
using UnityEngine.UI;  // <-- Required for Image type
using UnityEngine.AI;

/// <summary>
/// Player shooting - slows Bot_4 (Maze Ghost) on click for 5 seconds
/// Integrates with AgentMover.SetSlowed() for proper animation control
/// </summary>
public class PlayerShooter : MonoBehaviour
{
    [Header("Shooting Settings")]
    public float slowDuration = 5f;              // How long slow lasts
    public float slowFactor = 0.5f;              // How slow (0.5 = 50% slower, so 6→3)
    public LayerMask whatIsBot;                  // Bot layer mask
    
    [Header("Crosshair")]
    public GameObject crosshair; // Drag your Crosshair UI here
    
    private CharacterController _playerCC;
    private NavMeshAgent _playerNav;
    private BotController _targetBot; // The bot we slowed
    private float _slowEndTime;
    
    // Reference to AgentMover for proper slow effect
    private AgentMover _agentMover;

    void Start()
    {
        _playerCC = GetComponent<CharacterController>();
        _playerNav = GetComponent<NavMeshAgent>();
        
        // Find AgentMover on the bot or in scene using NEW API (obsolete fix)
        // Object.FindFirstObjectByType replaces deprecated FindObjectOfType
        _agentMover = Object.FindFirstObjectByType<AgentMover>();
        
        // Set crosshair to screen center
        if (crosshair)
        {
            RectTransform rt = crosshair.GetComponent<RectTransform>();
            rt.anchoredPosition = Vector2.zero;
        }
    }
    
    void Update()
    {
        // Check for slow effect expiration
        if (Time.time >= _slowEndTime)
        {
            // If we had a slowed bot, restore speed
            if (_targetBot != null)
            {
                _targetBot.isSlowed = false;
                
                // Call AgentMover's method to reset animator (ONLY way - no direct anim access)
                AgentMover agentMover = _targetBot.GetComponent<AgentMover>();
                if (agentMover != null)
                {
                    agentMover.SetSlowed(false);
                }
                // NO ELSE FALLBACK: BotController doesn't have 'anim' field
                // If no AgentMover component, just reset isSlowed flag simply
                
                _targetBot = null;
            }
        }
        
        // Shoot on Mouse Click
        if (Input.GetMouseButtonDown(0))
        {
            Shoot();
        }
    }
    
    void Shoot()
    {
        // Raycast from mouse position
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        
        if (Physics.Raycast(ray, out hit, 100f))
        {
            // Check if we hit a Bot
            BotController bot = hit.collider.GetComponent<BotController>();
            if (bot != null && whatIsBot == (whatIsBot | (1 << bot.gameObject.layer)))
            {
                // SLOW THE BOT using AgentMover!
                SlowBot(bot);
            }
            else
            {
                // Optional: Debug what we hit
                Debug.Log("Hit: " + hit.collider.name);
            }
        }
    }
    
    /// <summary>
    /// Apply slow effect to bot using AgentMover.SetSlowed()
    /// </summary>
    void SlowBot(BotController bot)
    {
        // Record original slow state
        _targetBot = bot;
        _slowEndTime = Time.time + slowDuration;
        
        // Call AgentMover's SetSlowed method for proper animation control
        AgentMover agentMover = bot.GetComponent<AgentMover>();
        if (agentMover != null)
        {
            agentMover.SetSlowed(true);
        }
        else
        {
            // Fallback: directly set the isSlowed field if no AgentMover component
            // Do NOT try to access bot.anim - BotController has no 'anim' field!
            bot.isSlowed = true;
            // If no AgentMover, we can't control animator properly
            // The isSlowed flag will be read by AgentMover if it exists later
        }
        
        // Visual feedback
        Debug.Log("Bot " + bot.name + " SLOWED for " + slowDuration + " seconds!");
        
        // Optional: Flash crosshair red
        if (crosshair.GetComponent<Image>())
            crosshair.GetComponent<Image>().color = Color.red;
    }
}