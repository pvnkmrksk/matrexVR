using UnityEngine;

public class BoundaryManager : MonoBehaviour
{
    public float boundarySize = 200;
    // Zero means use the historical boundarySize field, preserving old scenes.
    public float boundaryLengthX;
    public float boundaryLengthZ;
    public float boundaryHeight = 0;
    public bool wrapY;
    public float boundaryBuffer = 0.1f;
    void OnDrawGizmos()
{
    Gizmos.color = Color.red;
    float x = boundaryLengthX > 0 ? boundaryLengthX : boundarySize;
    float z = boundaryLengthZ > 0 ? boundaryLengthZ : boundarySize;
    float y = boundaryHeight > 0 ? boundaryHeight : 1;
    Gizmos.DrawWireCube(transform.position, new Vector3(x, y, z));
}

}
