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
            Vector3 pos = transform.position;

            float lengthX = boundaryManager.boundaryLengthX > 0 ? boundaryManager.boundaryLengthX : boundaryManager.boundarySize;
            float lengthZ = boundaryManager.boundaryLengthZ > 0 ? boundaryManager.boundaryLengthZ : boundaryManager.boundarySize;
            if (pos.x > boundaryManager.transform.position.x + lengthX / 2)
                pos.x = boundaryManager.transform.position.x - lengthX / 2 + boundaryManager.boundaryBuffer;

            else if (pos.x < boundaryManager.transform.position.x - lengthX / 2)
                pos.x = boundaryManager.transform.position.x + lengthX / 2 - boundaryManager.boundaryBuffer;

            if (pos.z > boundaryManager.transform.position.z + lengthZ / 2)
                pos.z = boundaryManager.transform.position.z - lengthZ / 2 + boundaryManager.boundaryBuffer;

            else if (pos.z < boundaryManager.transform.position.z - lengthZ / 2)
                pos.z = boundaryManager.transform.position.z + lengthZ / 2 - boundaryManager.boundaryBuffer;

            if (boundaryManager.wrapY && boundaryManager.boundaryHeight > 0)
            {
                float halfHeight = boundaryManager.boundaryHeight / 2;
                if (pos.y > boundaryManager.transform.position.y + halfHeight)
                    pos.y = boundaryManager.transform.position.y - halfHeight + boundaryManager.boundaryBuffer;
                else if (pos.y < boundaryManager.transform.position.y - halfHeight)
                    pos.y = boundaryManager.transform.position.y + halfHeight - boundaryManager.boundaryBuffer;
            }

            transform.position = pos;
        }
    }
}
