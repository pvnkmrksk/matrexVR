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

            if (pos.x > boundaryManager.transform.position.x + boundaryManager.boundarySize / 2)
                pos.x = boundaryManager.transform.position.x - boundaryManager.boundarySize / 2 + boundaryManager.boundaryBuffer;
            
            else if (pos.x < boundaryManager.transform.position.x - boundaryManager.boundarySize / 2)
                pos.x = boundaryManager.transform.position.x + boundaryManager.boundarySize / 2 - boundaryManager.boundaryBuffer;
            
            if (pos.z > boundaryManager.transform.position.z + boundaryManager.boundarySize / 2)
                pos.z = boundaryManager.transform.position.z - boundaryManager.boundarySize / 2 + boundaryManager.boundaryBuffer;
            
            else if (pos.z < boundaryManager.transform.position.z - boundaryManager.boundarySize / 2)
                pos.z = boundaryManager.transform.position.z + boundaryManager.boundarySize / 2 - boundaryManager.boundaryBuffer;

            transform.position = pos;
        }
    }
}
