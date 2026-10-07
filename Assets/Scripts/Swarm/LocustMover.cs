using UnityEngine;

public class LocustMover : MonoBehaviour
{
    public BoundaryManager boundaryManager;
    public float speed = 4.0f;

    void Update()
    {
        transform.Translate(Vector3.forward * speed * Time.deltaTime, Space.Self); // Move based on local space

        // Check boundaries and reposition if out of bounds
        if (boundaryManager)
        {
            Vector3 center = boundaryManager.Center;
            Vector3 pos = Quaternion.Inverse(boundaryManager.referenceRotation) * (transform.position - center);

            float lengthX = boundaryManager.boundaryLengthX > 0 ? boundaryManager.boundaryLengthX : boundaryManager.boundarySize;
            float lengthZ = boundaryManager.boundaryLengthZ > 0 ? boundaryManager.boundaryLengthZ : boundaryManager.boundarySize;
            if (pos.x > lengthX / 2)
                pos.x = -lengthX / 2 + boundaryManager.boundaryBuffer;

            else if (pos.x < -lengthX / 2)
                pos.x = lengthX / 2 - boundaryManager.boundaryBuffer;

            if (pos.z > lengthZ / 2)
                pos.z = -lengthZ / 2 + boundaryManager.boundaryBuffer;

            else if (pos.z < -lengthZ / 2)
                pos.z = lengthZ / 2 - boundaryManager.boundaryBuffer;

            if (boundaryManager.wrapY && boundaryManager.boundaryHeight > 0)
            {
                float halfHeight = boundaryManager.boundaryHeight / 2;
                if (pos.y > halfHeight)
                    pos.y = -halfHeight + boundaryManager.boundaryBuffer;
                else if (pos.y < -halfHeight)
                    pos.y = halfHeight - boundaryManager.boundaryBuffer;
            }

            transform.position = center + boundaryManager.referenceRotation * pos;
        }
    }
}
