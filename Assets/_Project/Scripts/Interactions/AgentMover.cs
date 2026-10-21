using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Makes capsule/character follow path points smoothly
/// Used by ALL 4 bots in team project (Bots 1, 2, 3, 4)
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Animator))]
public class AgentMover : MonoBehaviour
{
    [Header("Path Settings")]
    public List<Transform> pathPoints; // Drag waypoints here in Inspector

    [Header("Movement Settings")]
    public float normalSpeed = 6.0f;      // Ghost speed when normal (6 = running)
    public float slowedSpeed = 3.0f;      // Ghost speed when slowed (3 = walking)
    public float turnSpeed = 5.0f;
    public float stoppingDistance = 0.5f;

    [Header("Animation Settings")]
    public string speedParameter = "Speed";
    public string isMovingParameter = "IsMoving";

    [Header("Slow Effect")]
    public bool isSlowed = false;         // Is currently slowed by player?
    private int pathIndex = 0;
    private Rigidbody rb;
    private Animator anim;
    private bool reachedEnd = false;

    // Cached Animator parameter IDs for better performance and error prevention
    private int speedHash;
    private int isMovingHash;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        anim = GetComponent<Animator>();

        if (pathPoints == null || pathPoints.Count == 0)
        {
            Debug.LogError("AgentMover: No path points assigned! Assign pathPoints in Inspector.");
            enabled = false;
            return;
        }

        // Cache Animator parameter hashes - this prevents string lookup errors
        speedHash = Animator.StringToHash(speedParameter);
        isMovingHash = Animator.StringToHash(isMovingParameter);

        // Set initial speed based on slowed state
        if (anim != null)
        {
            // Use safe default values - ensure parameters exist in Animator
            anim.SetFloat(speedHash, isSlowed ? slowedSpeed / 10f : normalSpeed / 10f);
            anim.SetBool(isMovingHash, false);
        }

        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
    }

    void Update()
    {
        if (anim != null)
        {
            // Use appropriate speed based on slowed state
            float targetSpeed = isSlowed ? slowedSpeed : normalSpeed;

            // Safely get current Speed parameter (default to 0 if not set)
            float currentSpeed = anim.GetFloat(speedHash);

            // Clamp currentSpeed to 0-1 range to prevent Animator errors
            currentSpeed = Mathf.Clamp01(currentSpeed);
            float targetSpeedNormalized = Mathf.Clamp01(targetSpeed / 10f);

            // Protection against NaN/Infinity
            if (float.IsNaN(currentSpeed) || float.IsInfinity(currentSpeed))
                currentSpeed = 0f;

            // Smoothly animate toward target speed using Lerp
            anim.SetFloat(speedHash, Mathf.Lerp(currentSpeed, targetSpeedNormalized, Time.deltaTime * 5f));

            // Set IsMoving based on velocity magnitude
            float velMag = rb.linearVelocity.magnitude;
            anim.SetBool(isMovingHash, velMag > 0.1f);
        }
    }

    void FixedUpdate()
    {
        if (reachedEnd) return;

        // Safety checks kept from the dev integration (no waypoints = no crash)
        if (pathPoints == null || pathPoints.Count == 0 || pathIndex >= pathPoints.Count) return;

        Transform target = pathPoints[pathIndex];
        if (target == null) return;

        Vector3 direction = (target.position - transform.position).normalized;
        direction.y = 0f;

        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                turnSpeed * Time.fixedDeltaTime
            );
        }

        float currentSpeed = isSlowed ? slowedSpeed : normalSpeed;
        Vector3 movement = transform.forward * currentSpeed * Time.fixedDeltaTime;
        rb.MovePosition(transform.position + movement);

        float distance = Vector3.Distance(transform.position, pathPoints[pathIndex].position);

        if (distance < stoppingDistance)
        {
            pathIndex++;

            if (pathIndex >= pathPoints.Count)
            {
                reachedEnd = true;
                rb.linearVelocity = Vector3.zero;
                if (anim != null)
                {
                    anim.SetBool(isMovingHash, false);
                    anim.SetFloat(speedHash, 0f);
                }
                Debug.Log("Agent completed path");
            }
        }
    }

    /// <summary>
    /// Apply slow effect from player hit
    /// </summary>
    public void SetSlowed(bool slowed)
    {
        isSlowed = slowed;

        // Update animator immediately using cached hashes
        if (anim != null)
        {
            float targetSpeed = slowed ? slowedSpeed : normalSpeed;
            anim.SetFloat(speedHash, Mathf.Clamp01(targetSpeed / 10f));
            // Only set IsMoving to false if not already moving (preserve state otherwise)
            anim.SetBool(isMovingHash, !slowed || rb.linearVelocity.magnitude > 0.1f);
        }
    }

    public void ResetPath()
    {
        pathIndex = 0;
        reachedEnd = false;
        isSlowed = false; // Reset slow effect
        if (rb != null) rb.linearVelocity = Vector3.zero;
        if (anim != null)
        {
            anim.SetBool(isMovingHash, false);
            anim.SetFloat(speedHash, 0f);
        }
    }
}