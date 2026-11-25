using UnityEngine;
using FailSafeZero.Interactions;


[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour, IKnockbackable
{
    [Header("Movement Settings")]
    public float walkSpeed = 3.0f;
    public float runSpeed = 6.0f;
    [Tooltip("Upward speed at the moment of jumping (m/s).")]
    public float jumpForce = 5.0f;
    [Tooltip("Downward acceleration. 2x real gravity feels snappier in games.")]
    public float gravity = 19.62f;
    [Tooltip("How quickly the player reaches full speed (higher = snappier).")]
    public float acceleration = 12f;

    [Header("Mouse Look Settings")]
    public float mouseSensitivity = 5.0f;
    public float minimumX = -90.0f;
    public float maximumX = 90.0f;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundDistance = 0.4f;
    public LayerMask groundMask = 1;

    [Header("Crosshair")]
    public GameObject crosshair; 

    private float _rotationX;          
    private float _rotationY;          
    private bool _isGrounded;
    private CharacterController _characterController;
    private Camera _playerCamera;
    private Vector3 _horizontalVelocity;
    private float _verticalVelocity;
    private Vector3 _externalVelocity;  

    public float GravityScale { get; set; } = 1f;
    public bool IsGrounded => _isGrounded;
    public Vector3 Velocity => _horizontalVelocity + Vector3.up * _verticalVelocity + _externalVelocity;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        _characterController = GetComponent<CharacterController>();
        _playerCamera = GetComponentInChildren<Camera>();
        if (_playerCamera == null) _playerCamera = Camera.main;
        _rotationY = transform.eulerAngles.y;

        if (groundCheck == null)
        {
            GameObject gc = new GameObject("GroundCheck");
            gc.transform.parent = transform;
            gc.transform.localPosition = new Vector3(0, -0.9f, 0);
            groundCheck = gc.transform;
        }
    }

    void Update()
    {
        _isGrounded = _characterController.isGrounded ||
                      Physics.CheckSphere(groundCheck.position, groundDistance, groundMask, QueryTriggerInteraction.Ignore);

        Look();
        Move();

        if (crosshair != null)
        {
            RectTransform rt = crosshair.GetComponent<RectTransform>();
            if (rt != null) rt.anchoredPosition = Vector2.zero;
        }
    }

    void Look()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        _rotationY += mouseX;
        _rotationX = Mathf.Clamp(_rotationX - mouseY, minimumX, maximumX);

        transform.localRotation = Quaternion.Euler(0f, _rotationY, 0f);
        if (_playerCamera != null)
            _playerCamera.transform.localRotation = Quaternion.Euler(_rotationX, 0f, 0f);
    }

    void Move()
    {
        // 1. Horizontal: input relative to where the body faces, smoothed for a natural start/stop.
        float speed = Input.GetKey(KeyCode.LeftShift) ? runSpeed : walkSpeed;
        Vector3 input = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
        input = Vector3.ClampMagnitude(input, 1f);                       // no faster diagonals
        Vector3 target = transform.TransformDirection(input) * speed;
        _horizontalVelocity = Vector3.Lerp(_horizontalVelocity, target, acceleration * Time.deltaTime);

        // 2. Vertical: our own gravity, scaled by the Gravity bot when it messes with us.
        if (_isGrounded && _verticalVelocity < 0f && GravityScale > 0f)
            _verticalVelocity = -2f;                                       // small push keeps us glued to slopes

        if (_isGrounded && GravityScale > 0f && Input.GetKeyDown(KeyCode.Space))
            _verticalVelocity = jumpForce;

        _verticalVelocity -= gravity * GravityScale * Time.deltaTime;
        _verticalVelocity = Mathf.Clamp(_verticalVelocity, -40f, 15f);    // terminal velocity

        _externalVelocity = Vector3.Lerp(_externalVelocity, Vector3.zero, 3f * Time.deltaTime);

        CollisionFlags flags = _characterController.Move(
            (_horizontalVelocity + Vector3.up * _verticalVelocity + _externalVelocity) * Time.deltaTime);

        if ((flags & CollisionFlags.Above) != 0 && _verticalVelocity > 0f) _verticalVelocity = 0f;
    }

    public void ApplyKnockback(Vector3 velocity)
    {
        _externalVelocity += new Vector3(velocity.x, 0f, velocity.z);
        _verticalVelocity = Mathf.Max(_verticalVelocity, velocity.y);
    }

    public void AddPull(Vector3 accelerationPerSecond)
    {
        _externalVelocity += accelerationPerSecond * Time.deltaTime;
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;
        Gizmos.color = _isGrounded ? Color.green : Color.red;
        Gizmos.DrawSphere(groundCheck.position, groundDistance);
    }
}
