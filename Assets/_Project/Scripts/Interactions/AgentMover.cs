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
    public float speed = 3.0f;
    public float turnSpeed = 5.0f;
    public float stoppingDistance = 0.5f;

    [Header("Animation Settings")]
    public string speedParameter = "Speed";
    public string isMovingParameter = "IsMoving";

    private int pathIndex = 0;
    private Rigidbody rb;
    private Animator anim;
    private bool reachedEnd = false;

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

        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
    }

    void Update()
    {
        if (anim != null)
        {
            float velMag = rb.linearVelocity.magnitude;
            anim.SetFloat(speedParameter, velMag);
            anim.SetBool(isMovingParameter, velMag > 0.1f);
        }
    }

    void FixedUpdate()
    {
        if (reachedEnd) return;

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

        Vector3 movement = transform.forward * speed * Time.fixedDeltaTime;
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
                    anim.SetBool(isMovingParameter, false);
                    anim.SetFloat(speedParameter, 0f);
                }
                Debug.Log("Agent completed path");
            }
        }
    }
}