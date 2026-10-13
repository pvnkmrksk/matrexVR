using UnityEngine;

public class LocustMover : MonoBehaviour
{
    public BoundaryManager boundaryManager;
    public float speed = 4.0f;

    void Update() => Advance(Time.deltaTime);

    public void Advance(float seconds)
    {
        transform.Translate(Vector3.forward * speed * seconds, Space.Self); // Move based on local space

        // Check boundaries and reposition if out of bounds
        if (boundaryManager)
        {
            Vector3 center = boundaryManager.Center;
            Vector3 pos = Quaternion.Inverse(boundaryManager.referenceRotation) * (transform.position - center);

            float lengthX = boundaryManager.boundaryLengthX > 0 ? boundaryManager.boundaryLengthX : boundaryManager.boundarySize;
            float lengthZ = boundaryManager.boundaryLengthZ > 0 ? boundaryManager.boundaryLengthZ : boundaryManager.boundarySize;
            if (boundaryManager.preserveOvershoot)
            {
                // Preserve distance travelled, including large frame steps and observer teleports.
                pos.x = Mathf.Repeat(pos.x + lengthX / 2, lengthX) - lengthX / 2;
                pos.z = Mathf.Repeat(pos.z + lengthZ / 2, lengthZ) - lengthZ / 2;
                if (boundaryManager.wrapY && boundaryManager.boundaryHeight > 0)
                    pos.y = Mathf.Repeat(pos.y + boundaryManager.boundaryHeight / 2, boundaryManager.boundaryHeight) - boundaryManager.boundaryHeight / 2;
                transform.position = center + boundaryManager.referenceRotation * pos;
                return;
            }
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
