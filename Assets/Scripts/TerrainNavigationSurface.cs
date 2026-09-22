using UnityEngine;

// Navigation samples data directly, independently of renderers and physics colliders.
public class TerrainNavigationSurface : MonoBehaviour
{
    public TerrainData ruggedData;
    public bool flat;
    public float flatHeightCm;

    public bool Sample(Vector3 world, out float height, out Vector3 normal)
    {
        height = 0; normal = Vector3.up;
        if (ruggedData == null) return false;
        Vector3 p = transform.InverseTransformPoint(world);
        Vector3 size = ruggedData.size;
        float x = p.x / size.x, z = p.z / size.z;
        if (x < 0 || x > 1 || z < 0 || z > 1) return false;
        if (!flat && ruggedData.IsHole(Mathf.Min((int)(x * ruggedData.holesResolution), ruggedData.holesResolution - 1),
            Mathf.Min((int)(z * ruggedData.holesResolution), ruggedData.holesResolution - 1))) return false;
        float y = flat ? flatHeightCm : ruggedData.GetInterpolatedHeight(x, z);
        height = transform.TransformPoint(new Vector3(p.x, y, p.z)).y;
        normal = flat ? transform.up : transform.localToWorldMatrix.inverse.transpose.MultiplyVector(ruggedData.GetInterpolatedNormal(x, z)).normalized;
        return true;
    }
}
