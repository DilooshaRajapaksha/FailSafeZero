using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AI;

public class MazeGhostShooter : MonoBehaviour
{
    [Header("Shooting Settings")]
    public float slowDuration = 5f;
    public float slowFactor = 0.5f;
    public LayerMask whatIsBot;

    [Header("Crosshair")]
    public GameObject crosshair;

    private CharacterController _playerCC;
    private NavMeshAgent _playerNav;
    private MazeGhostController _targetBot;
    private float _slowEndTime;

    private AgentMover _agentMover;

    void Start()
    {
        _playerCC = GetComponent<CharacterController>();
        _playerNav = GetComponent<NavMeshAgent>();
        _agentMover = Object.FindFirstObjectByType<AgentMover>();

        if (crosshair)
        {
            RectTransform rt = crosshair.GetComponent<RectTransform>();
            rt.anchoredPosition = Vector2.zero;
        }
    }

    void Update()
    {
        if (Time.time >= _slowEndTime)
        {
            if (_targetBot != null)
            {
                _targetBot.isSlowed = false;

                AgentMover agentMover = _targetBot.GetComponent<AgentMover>();
                if (agentMover != null)
                {
                    agentMover.SetSlowed(false);
                }

                _targetBot = null;
            }
        }

        if (Input.GetMouseButtonDown(0))
        {
            Shoot();
        }
    }

    void Shoot()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, 100f))
        {
            MazeGhostController bot = hit.collider.GetComponent<MazeGhostController>();
            if (bot != null && whatIsBot == (whatIsBot | (1 << bot.gameObject.layer)))
            {
                SlowBot(bot);
            }
            else
            {
                Debug.Log("Hit: " + hit.collider.name);
            }
        }
    }

    void SlowBot(MazeGhostController bot)
    {
        _targetBot = bot;
        _slowEndTime = Time.time + slowDuration;

        AgentMover agentMover = bot.GetComponent<AgentMover>();
        if (agentMover != null)
        {
            agentMover.SetSlowed(true);
        }
        else
        {
            bot.isSlowed = true;
        }

        Debug.Log("Bot " + bot.name + " SLOWED for " + slowDuration + " seconds!");

        if (crosshair.GetComponent<Image>())
            crosshair.GetComponent<Image>().color = Color.red;
    }
}