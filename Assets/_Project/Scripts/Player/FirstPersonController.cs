using UnityEngine;

/// <summary>
/// First Person Controller - FPS viewpoint, WASD movement relative to camera look direction
/// Mouse look controls direction, Space jumps
/// </summary>
public class FirstPersonController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 3.0f;
    public float runSpeed = 6.0f;
    public float jumpForce = 5.0f;
    
    [Header("Mouse Look Settings")]
    public float mouseSensitivity = 3.0f;
    public float minimumX = -90.0f;
    public float maximumX = 90.0f;
    
    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundDistance = 0.4f;
    public LayerMask groundMask;
    
    private float _currentSpeed;
    private float _rotationX = 0f; // Camera X rotation (pitch)
    private float _rotationY = 0f; // Player Y rotation (yaw)
    private bool _isGrounded;
    private CharacterController _characterController;
    private Vector3 _moveDirection = Vector3.zero;
    
    // Reference to main camera (if not set, uses Camera.main)
    private Camera _playerCamera;
    
    void Start()
    {
        // Lock cursor to center
        Cursor.lockState = CursorLockMode.Locked;
        
        // Get or add CharacterController
        _characterController = GetComponent<CharacterController>();
        if (_characterController == null)
            _characterController = gameObject.AddComponent<CharacterController>();
        
        // Get main camera
        _playerCamera = Camera.main;
        if (_playerCamera == null)
        {
            // Try to find camera attached to this object or in scene
            _playerCamera = Camera.main;
        }
        
        // Set up ground check if not assigned
        if (groundCheck == null)
        {
            GameObject gc = new GameObject("GroundCheck");
            gc.transform.parent = transform;
            gc.transform.localPosition = new Vector3(0, 0.4f, 0);
            groundCheck = gc.transform;
        }
    }
    
    void Update()
    {
        // Ground check
        _isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);
        
        // --- MOUSE LOOK (First Person) ---
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
        
        // Rotate player (Y axis) and camera (X axis / pitch)
        _rotationY += mouseX;
        _rotationY = Mathf.Clamp(_rotationY, -360f, 360f);
        
        _rotationX -= mouseY;
        _rotationX = Mathf.Clamp(_rotationX, minimumX, maximumX);
        
        // Apply player rotation (Y axis only - yaw)
        transform.localRotation = Quaternion.Euler(0f, _rotationY, 0f);
        
        // Apply camera rotation (X axis / pitch) - IF we have a camera to modify
        if (_playerCamera != null)
        {
            _playerCamera.transform.localRotation = Quaternion.Euler(_rotationX, 0f, 0f);
        }
        
        // --- MOVEMENT INPUT ---
        // Get movement input
        bool isRunning = Input.GetKey(KeyCode.LeftShift);
        _currentSpeed = isRunning ? runSpeed : walkSpeed;
        
        // Get WASD input - move relative to camera direction
        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");
        
        // Calculate move direction relative to camera
        // If we have a camera, use its forward/right; otherwise use player's
        Vector3 moveDir;
        if (_playerCamera != null)
        {
            // Get forward and right directions from camera, but remove Y component so player stays grounded
            Vector3 camForward = Vector3.Scale(_playerCamera.transform.forward, new Vector3(1, 0, 1)).normalized;
            Vector3 camRight = Vector3.Scale(_playerCamera.transform.right, new Vector3(1, 0, 1)).normalized;
            moveDir = camForward * moveZ + camRight * moveX;
        }
        else
        {
            // Fallback: move relative to player's own rotation
            moveDir = transform.forward * moveZ + transform.right * moveX;
        }
        
        // Apply movement
        _moveDirection = moveDir * _currentSpeed;
        
        // Jumping
        if (_isGrounded && Input.GetKeyDown(KeyCode.Space))
        {
            _moveDirection.y = jumpForce;
        }
        
        // Apply gravity when not grounded
        if (!_isGrounded)
        {
            _moveDirection.y -= 9.81f * Time.deltaTime * 2f;
        }
        
        // Final movement
        _characterController.Move(_moveDirection * Time.deltaTime);
    }
    
    // Draw ground check radius in Scene view
    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;
        Gizmos.color = _isGrounded ? Color.green : Color.red;
        Gizmos.DrawSphere(groundCheck.position, groundDistance);
    }
}
