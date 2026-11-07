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
    [System.NonSerialized] public bool preserveOvershoot;
    public float boundaryBuffer = 0.1f;
    public float LengthX => boundaryLengthX > 0 ? boundaryLengthX : boundarySize;
    public float LengthZ => boundaryLengthZ > 0 ? boundaryLengthZ : boundarySize;

    internal readonly struct WrappingFrame
    {
        private readonly Vector3 center;
        private readonly Quaternion rotation, inverse;
        private readonly float x, y, z, buffer;
        private readonly bool rotated, preserve;

        public WrappingFrame(BoundaryManager boundary)
        {
            center = boundary.Center;
            rotation = boundary.referenceRotation;
            rotated = !rotation.Equals(Quaternion.identity);
            inverse = rotated ? Quaternion.Inverse(rotation) : Quaternion.identity;
            x = boundary.LengthX;
            z = boundary.LengthZ;
            y = boundary.wrapY ? boundary.boundaryHeight : 0;
            buffer = boundary.boundaryBuffer;
            preserve = boundary.preserveOvershoot;
        }

        public Vector3 Wrap(Vector3 position)
        {
            if (x <= 0 || z <= 0) return position;
            Vector3 local = position - center;
            if (rotated) local = inverse * local;
            float wrappedX = WrapAxis(local.x, x);
            float wrappedZ = WrapAxis(local.z, z);
            float wrappedY = y > 0 ? WrapAxis(local.y, y) : local.y;
            if (wrappedX == local.x && wrappedY == local.y && wrappedZ == local.z) return position;
            var wrapped = new Vector3(wrappedX, wrappedY, wrappedZ);
            return center + (rotated ? rotation * wrapped : wrapped);
        }

        private float WrapAxis(float value, float length)
        {
            float half = length * .5f;
            if (preserve)
                return value < -half || value >= half ? Mathf.Repeat(value + half, length) - half : value;
            if (value < -half) return half - buffer;
            return value > half ? -half + buffer : value;
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(Center, new Vector3(LengthX, boundaryHeight > 0 ? boundaryHeight : 1, LengthZ));
    }
}
