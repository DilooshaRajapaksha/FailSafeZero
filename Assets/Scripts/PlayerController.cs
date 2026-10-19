using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// FPS Player Controller - using Unity New Input System
/// WASD movement, jumping, mouse look
/// </summary>
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 3.0f;
    public float runSpeed = 6.0f;
    public float jumpForce = 5.0f;
    
    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundDistance = 0.4f;
    public LayerMask groundMask;
    
    private float _currentSpeed;
    private bool _isGrounded;
    private Vector2 _moveInput;
    private CharacterController _characterController;
    private float _rotationX = 0f;
    private float _rotationY = 0f;
    
    // New Input System actions
    private PlayerInput _playerInput;
    
    // Reference to the input action asset
    // If you don't have an Input Action Asset, we'll use the old GetAxis fallback
    void OnEnable()
    {
        // Try to find player input component
        _playerInput = GetComponent<PlayerInput>();
        
        // Ground check setup
        if (groundCheck == null)
        {
            // Auto-create ground check transform
            GameObject gc = new GameObject("GroundCheck");
            gc.transform.parent = transform;
            gc.transform.localPosition = new Vector3(0, 0.4f, 0);
            groundCheck = gc.transform;
        }
    }
    
    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        _characterController = GetComponent<CharacterController>();
        if (_characterController == null)
            _characterController = gameObject.AddComponent<CharacterController>();
    }
    
    void Update()
    {
        // Ground check
        _isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);
        
        // --- NEW INPUT SYSTEM ---
        // Get WASD movement from Input System
        if (_playerInput != null)
        {
            _moveInput = _playerInput.actions["Move"].ReadValue<Vector2>();
        }
        else
        {
            // Fallback to old Input.GetAxis if no Input System
            _moveInput = new Vector2(
                Input.GetAxis("Horizontal"),
                Input.GetAxis("Vertical")
            );
        }
        
        // Mouse look - new system uses different API
        // Using old Input for mouse as fallback
        float mouseX = Input.GetAxis("Mouse X") * 3.0f;
        float mouseY = Input.GetAxis("Mouse Y") * 3.0f;
        
        _rotationX -= mouseY;
        _rotationX = Mathf.Clamp(_rotationX, -90f, 90f);
        
        _rotationY += mouseX;
        _rotationY = Mathf.Clamp(_rotationY, -90f, 90f);
        
        // Apply rotations
        transform.localRotation = Quaternion.Euler(_rotationX, 0f, 0f);
        if (Camera.main != null)
            Camera.main.transform.localRotation = Quaternion.Euler(0f, _rotationY, 0f);
        
        // Movement calculation
        _currentSpeed = Input.GetKey(KeyCode.LeftShift) ? runSpeed : walkSpeed;
        
        // Calculate movement direction
        Vector3 move = transform.forward * _moveInput.y + transform.right * _moveInput.x;
        
        // Apply gravity only when not grounded
        if (!_isGrounded)
        {
            _characterController.Move(Vector3.down * 9.81f * Time.deltaTime * 2f);
        }
        
        // Jumping
        if (_isGrounded && Input.GetKeyDown(KeyCode.Space))
        {
            _characterController.Move(Vector3.up * jumpForce);
        }
        
        // Apply movement
        _characterController.Move(move * _currentSpeed * Time.deltaTime);
    }
    
    // Draw ground check radius in Scene view
    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;
        Gizmos.color = _isGrounded ? Color.green : Color.red;
        Gizmos.DrawSphere(groundCheck.position, groundDistance);
    }
}
