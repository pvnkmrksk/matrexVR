using UnityEngine;

public class BoundaryManager : MonoBehaviour
{
    public float boundarySize = 200;
    public float boundaryBuffer = 0.1f;
    void OnDrawGizmos()
{
    Gizmos.color = Color.red;
    Gizmos.DrawWireCube(transform.position, new Vector3(boundarySize, 1, boundarySize));
}

}
