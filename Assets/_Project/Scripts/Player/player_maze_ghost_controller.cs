using UnityEngine;
using UnityEngine.UI;  // Required for crosshair UI
using UnityEngine.AI;  // Required for NavMeshAgent

/// <summary>
/// Maze-Ghost Player Controller - FPS movement with crosshair
/// WASD movement, mouse look, jump, and crosshair UI reference
/// </summary>
public class player_maze_ghost_controller : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 3.0f;
    public float runSpeed = 6.0f;
    public float jumpForce = 8.0f;
    public float mouseSensitivity = 5.0f;
    
    [Header("Crosshair")]
    public GameObject crosshair;  // Drag UI Image here
    
    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundDistance = 0.4f;
    public LayerMask groundMask;
    
    [Header("Animator")]
    public Animator anim;
    
    // Cached hashes for animator parameters
    private int speedHash;
    
    private void Awake()
    {
        // Cache animator hash for performance
        speedHash = Animator.StringToHash("Speed");
        
        // Auto-assign components if not dragged in Inspector
        if (anim == null) anim = GetComponent<Animator>();
        if (crosshair == null)
        {
            // Try to find crosshair by name
            crosshair = GameObject.Find("Crosshair");
        }
        
        // Auto-setup ground check if not assigned
        if (groundCheck == null)
        {
            // Create empty child object for ground check
            GameObject go = new GameObject("GroundCheck");
            go.transform.parent = transform;
            go.transform.localPosition = new Vector3(0, -1f, 0);
            groundCheck = go.transform;
        }
    }
    
    private void Start()
    {
        // Lock cursor to center of screen
        Cursor.lockState = CursorLockMode.Locked;
        
        // Auto-setup ground check position if needed
        if (groundCheck != null)
        {
            groundCheck.transform.localPosition = new Vector3(0, -1f, 0);
        }
    }
    
    private void Update()
    {
        // 1. Movement (WASD) - relative to camera
        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");
        
        // Calculate movement relative to camera's rotation
        Vector3 cameraForward = Vector3.Scale(
            Camera.main.transform.forward, 
            new Vector3(1, 0, 1)
        ).normalized;
        Vector3 move = cameraForward * moveZ + Camera.main.transform.right * moveX;
        
        // Apply run shift (LeftShift)
        bool isRunning = Input.GetKey(KeyCode.LeftShift);
        float currentSpeed = isRunning ? runSpeed : walkSpeed;
        
        // Move the player
        transform.position += move * currentSpeed * Time.deltaTime;
        
        // 2. Jump (Space key)
        if (Input.GetKeyDown(KeyCode.Space) && IsGrounded())
        {
            GetComponent<Rigidbody>().linearVelocity = new Vector3(
                GetComponent<Rigidbody>().linearVelocity.x,
                jumpForce,
                GetComponent<Rigidbody>().linearVelocity.z
            );
        }
        
        // 3. Mouse Look (Horizontal + Vertical)
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
        
        // Horizontal rotation (yaw) - rotate the player object
        transform.Rotate(Vector3.up * mouseX);
        
        // Vertical rotation (pitch) - clamp between -90 and +90 degrees
        float currentX = transform.eulerAngles.x - mouseY;
        currentX = Mathf.Clamp(currentX, -90f, 90f);
        transform.eulerAngles = new Vector3(currentX, transform.eulerAngles.y, 0);
        
        // 4. Update Animator Speed parameter
        if (anim != null)
        {
            float velocityMagnitude = GetComponent<Rigidbody>().linearVelocity.magnitude;
            anim.SetFloat(speedHash, velocityMagnitude / runSpeed);
        }
        
        // 5. Crosshair visibility/logic
        if (crosshair != null)
        {
            // Crosshair can be shown/hidden based on gameplay needs
            // e.g., show when aiming, hide when moving, etc.
        }
    }
    
    private bool IsGrounded()
    {
        if (groundCheck == null) return true;
        return Physics.CheckSphere(
            groundCheck.position, 
            groundDistance, 
            groundMask
        );
    }
}