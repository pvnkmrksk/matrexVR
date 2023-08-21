using UnityEngine;

public class BoundaryManager : MonoBehaviour
{
    public float boundarySize;

    void Update()
    {
        // Handle x-axis boundaries
        if (transform.position.x > boundarySize / 2)
            transform.position = new Vector3(-boundarySize / 2, transform.position.y, transform.position.z);
        
        if (transform.position.x < -boundarySize / 2)
            transform.position = new Vector3(boundarySize / 2, transform.position.y, transform.position.z);
        
        // Handle z-axis boundaries
        if (transform.position.z > boundarySize / 2)
            transform.position = new Vector3(transform.position.x, transform.position.y, -boundarySize / 2);
        
        if (transform.position.z < -boundarySize / 2)
            transform.position = new Vector3(transform.position.x, transform.position.y, boundarySize / 2);
    }
}
