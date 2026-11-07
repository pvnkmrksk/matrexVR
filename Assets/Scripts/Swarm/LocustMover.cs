using UnityEngine;

public class LocustMover : MonoBehaviour
{
    public BoundaryManager boundaryManager;
    public float speed = 4.0f;

    void Update() => Advance(Time.deltaTime);

    public void Advance(float seconds) => Advance(seconds,
        boundaryManager != null ? new BoundaryManager.WrappingFrame(boundaryManager) : default);

    internal void Advance(float seconds, in BoundaryManager.WrappingFrame frame)
    {
        Transform agent = transform;
        Vector3 current = agent.position;
        float distance = speed * seconds;
        Vector3 next = NextPosition(current, distance != 0 ? agent.rotation : default, distance, frame);
        if (!next.Equals(current)) agent.position = next;
    }

    internal static Vector3 NextPosition(Vector3 current, Quaternion rotation, float distance,
        in BoundaryManager.WrappingFrame frame) =>
        frame.Wrap(distance != 0 ? current + (rotation * Vector3.forward) * distance : current);
}
