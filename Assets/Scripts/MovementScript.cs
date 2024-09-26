using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MovementScript : MonoBehaviour
{
    public float speed;

    // Periodic Boundary Parameters
    public Vector3 boundaryCenter = Vector3.zero;
    public float boundaryWidth = 20f;
    public float boundaryDepth = 20f;

    // Band Movement Parameters
    public bool moveWithTransform = false;
    public Transform targetTransform;

    void Update()
    {
        // Move forward
        transform.position += transform.forward * speed * Time.deltaTime;

        // Update boundary center if moving with transform
        Vector3 center = moveWithTransform && targetTransform != null ? targetTransform.position : boundaryCenter;

        // Handle periodic boundaries
        HandlePeriodicBoundaries(center);
    }

    void HandlePeriodicBoundaries(Vector3 center)
    {
        Vector3 position = transform.position;

        float halfWidth = boundaryWidth / 2f;
        float halfDepth = boundaryDepth / 2f;

        // Check X-axis boundaries
        if (position.x > center.x + halfWidth)
        {
            position.x -= boundaryWidth;
        }
        else if (position.x < center.x - halfWidth)
        {
            position.x += boundaryWidth;
        }

        // Check Z-axis boundaries
        if (position.z > center.z + halfDepth)
        {
            position.z -= boundaryDepth;
        }
        else if (position.z < center.z - halfDepth)
        {
            position.z += boundaryDepth;
        }

        transform.position = position;
    }
}

