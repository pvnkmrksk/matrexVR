using UnityEngine;

/// <summary>
/// Global boundary manager that wraps all objects crossing the boundary as a final fallback.
/// This is a separate system from PeriodicBoundary, acting as a safety net.
/// </summary>
public class GlobalBoundaryManager : MonoBehaviour
{
    [Tooltip("Boundary center in world space")] 
    public Vector3 boundaryCenter = Vector3.zero;
    
    [Tooltip("Boundary width (X-axis) in centimeters")] 
    public float boundaryLengthX = 200f;
    
    [Tooltip("Boundary length (Z-axis) in centimeters")] 
    public float boundaryLengthZ = 200f;
    
    [Tooltip("Tags of objects to wrap (leave empty to wrap all objects)")] 
    public string[] targetTags = new string[0];
    
    [Tooltip("Layers to wrap (leave empty to wrap all layers)")] 
    public LayerMask targetLayers = -1; // All layers by default
    
    private void LateUpdate()
    {
        // Find all objects that need wrapping
        GameObject[] allObjects = FindObjectsOfType<GameObject>();
        
        foreach (GameObject obj in allObjects)
        {
            // Skip if object doesn't match criteria
            if (!ShouldWrapObject(obj))
                continue;
            
            // Skip if object has PeriodicBoundary (let it handle its own wrapping)
            if (obj.GetComponent<PeriodicBoundary>() != null)
                continue;
            
            // Wrap the object if it's outside the boundary
            WrapObject(obj.transform);
        }
    }
    
    private bool ShouldWrapObject(GameObject obj)
    {
        // Check tags
        if (targetTags != null && targetTags.Length > 0)
        {
            bool tagMatches = false;
            foreach (string tag in targetTags)
            {
                if (obj.CompareTag(tag))
                {
                    tagMatches = true;
                    break;
                }
            }
            if (!tagMatches)
                return false;
        }
        
        // Check layers
        if (targetLayers != -1 && (targetLayers & (1 << obj.layer)) == 0)
            return false;
        
        return true;
    }
    
    private void WrapObject(Transform objTransform)
    {
        Vector3 position = objTransform.position;
        Vector3 localPos = position - boundaryCenter;
        
        float halfWidth = boundaryLengthX / 2f;
        float halfLength = boundaryLengthZ / 2f;
        
        bool wasWrapped = false;
        
        // Wrap X-axis
        if (localPos.x >= halfWidth)
        {
            localPos.x -= boundaryLengthX;
            wasWrapped = true;
        }
        else if (localPos.x <= -halfWidth)
        {
            localPos.x += boundaryLengthX;
            wasWrapped = true;
        }
        
        // Wrap Z-axis
        if (localPos.z >= halfLength)
        {
            localPos.z -= boundaryLengthZ;
            wasWrapped = true;
        }
        else if (localPos.z <= -halfLength)
        {
            localPos.z += boundaryLengthZ;
            wasWrapped = true;
        }
        
        if (wasWrapped)
        {
            objTransform.position = boundaryCenter + localPos;
        }
    }
    
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Vector3 size = new Vector3(boundaryLengthX, 1f, boundaryLengthZ);
        Gizmos.DrawWireCube(boundaryCenter, size);
    }
}

