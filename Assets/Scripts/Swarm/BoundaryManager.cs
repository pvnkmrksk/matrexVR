using UnityEngine;

public class BoundaryManager : MonoBehaviour
{
    [System.NonSerialized] public bool useCenterOverride;
    [System.NonSerialized] public Vector3 centerOverride, followOffset;
    [System.NonSerialized] public Transform followTarget;
    [System.NonSerialized] public Quaternion referenceRotation = Quaternion.identity;
    public Vector3 Center => followTarget != null ? followTarget.position + followOffset : useCenterOverride ? centerOverride : transform.position;
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
