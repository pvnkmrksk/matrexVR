using UnityEngine;

public class BoundaryManager : MonoBehaviour
{
    public float boundarySize;
    public float boundaryBuffer = 0.1f;

    void Update()
    {
        LocustMover[] locusts = FindObjectsOfType<LocustMover>();
        
        foreach (LocustMover locust in locusts)
        {
            Transform locustTransform = locust.transform;

            // Handle x-axis boundaries
            if (locustTransform.position.x > transform.position.x + boundarySize / 2)
                locustTransform.position = new Vector3(locustTransform.position.x - boundarySize / 2 + boundaryBuffer, locustTransform.position.y, locustTransform.position.z);
            
            if (locustTransform.position.x < transform.position.x - boundarySize / 2)
                locustTransform.position = new Vector3(locustTransform.position.x + boundarySize / 2 - boundaryBuffer, locustTransform.position.y, locustTransform.position.z);
            
            // Handle z-axis boundaries
            if (locustTransform.position.z > transform.position.z + boundarySize / 2)
                locustTransform.position = new Vector3(locustTransform.position.x, locustTransform.position.y, locustTransform.position.z - boundarySize / 2 + boundaryBuffer);
            
            if (locustTransform.position.z < transform.position.z - boundarySize / 2)
                locustTransform.position = new Vector3(locustTransform.position.x, locustTransform.position.y, locustTransform.position.z + boundarySize / 2 - boundaryBuffer);
        }
    }
}
