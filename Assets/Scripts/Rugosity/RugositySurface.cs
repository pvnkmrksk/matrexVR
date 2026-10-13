using System;
using System.Collections.Generic;
using UnityEngine;

// Sampling is independent of renderer/collider enabled states. Explicit geometry only;
// no global raycasts that can hit animals, trees, or the wrong visual terrain.
public sealed class RugositySurface : MonoBehaviour
{
    public string surfaceId = "RS";
    public Terrain[] terrains = Array.Empty<Terrain>();
    public MeshFilter[] meshes = Array.Empty<MeshFilter>();
    public Renderer[] renderers = Array.Empty<Renderer>();
    public Collider[] colliders = Array.Empty<Collider>();
    private readonly Dictionary<MeshFilter, MeshIndex> indices = new Dictionary<MeshFilter, MeshIndex>();

    public void SetRoles(bool visible, bool physical)
    {
        foreach (var t in terrains) if (t != null) t.enabled = visible;
        foreach (var r in renderers) if (r != null) r.enabled = visible;
        foreach (var c in colliders) if (c != null) c.enabled = physical;
    }

    public void InvalidateGeometry() => indices.Clear();
    private void OnValidate() => InvalidateGeometry();

    public bool Sample(Vector3 world, out float height, out Vector3 normal)
    {
        height = float.NegativeInfinity; normal = Vector3.up;
        foreach (var terrain in terrains)
        {
            if (terrain == null || terrain.terrainData == null) continue;
            if (Vector3.Angle(terrain.transform.up, Vector3.up) > .01f)
                throw new InvalidOperationException("Unity Terrain sampling requires a world-up terrain: " + terrain.name);
            var data = terrain.terrainData;
            var p = terrain.transform.InverseTransformPoint(world);
            float x = p.x / data.size.x, z = p.z / data.size.z;
            if (x < 0 || x > 1 || z < 0 || z > 1) continue;
            if (data.IsHole(Mathf.Min((int)(x * data.holesResolution), data.holesResolution - 1),
                Mathf.Min((int)(z * data.holesResolution), data.holesResolution - 1))) continue;
            float y = terrain.transform.TransformPoint(new Vector3(p.x, data.GetInterpolatedHeight(x, z), p.z)).y;
            if (y > height)
            {
                height = y;
                normal = terrain.transform.localToWorldMatrix.inverse.transpose.MultiplyVector(data.GetInterpolatedNormal(x, z)).normalized;
            }
        }
        foreach (var filter in meshes)
        {
            if (filter == null || filter.sharedMesh == null) continue;
            if (!indices.TryGetValue(filter, out var index) || index.Mesh != filter.sharedMesh || index.Matrix != filter.transform.localToWorldMatrix)
                indices[filter] = index = new MeshIndex(filter);
            if (index.Sample(world, out float y, out Vector3 n) && y > height) { height = y; normal = n; }
        }
        return !float.IsNegativeInfinity(height);
    }

    // A world-XZ grid keeps a vertical height query local rather than scanning the entire mesh.
    private sealed class MeshIndex
    {
        public readonly Mesh Mesh;
        public readonly Matrix4x4 Matrix;
        private readonly Vector3[] vertices, normals;
        private readonly int[] triangles;
        private readonly List<int>[] cells;
        private readonly int resolution;
        private readonly Bounds bounds;
        public MeshIndex(MeshFilter filter)
        {
            Mesh = filter.sharedMesh; Matrix = filter.transform.localToWorldMatrix;
            if (!Mesh.isReadable) throw new InvalidOperationException("Enable Read/Write on rugosity mesh: " + Mesh.name);
            vertices = Mesh.vertices; normals = Mesh.normals; triangles = Mesh.triangles;
            var b = new Bounds(filter.transform.position, Vector3.zero);
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = Matrix.MultiplyPoint3x4(vertices[i]);
                if (i == 0) b = new Bounds(vertices[i], Vector3.zero); else b.Encapsulate(vertices[i]);
                if (normals.Length == vertices.Length) normals[i] = Matrix.inverse.transpose.MultiplyVector(normals[i]).normalized;
            }
            bounds = b;
            resolution = Mathf.Clamp(Mathf.CeilToInt(Mathf.Sqrt(triangles.Length / 6f)), 1, 128);
            cells = new List<int>[resolution * resolution];
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a = vertices[triangles[i]], c = vertices[triangles[i + 1]], d = vertices[triangles[i + 2]];
                int x0 = X(Mathf.Min(a.x, c.x, d.x)), x1 = X(Mathf.Max(a.x, c.x, d.x));
                int z0 = Z(Mathf.Min(a.z, c.z, d.z)), z1 = Z(Mathf.Max(a.z, c.z, d.z));
                for (int z = z0; z <= z1; z++) for (int x = x0; x <= x1; x++)
                { int cell = z * resolution + x; (cells[cell] ?? (cells[cell] = new List<int>())).Add(i); }
            }
        }
        private int X(float x) => Mathf.Clamp((int)((x - bounds.min.x) / Mathf.Max(bounds.size.x, .00001f) * resolution), 0, resolution - 1);
        private int Z(float z) => Mathf.Clamp((int)((z - bounds.min.z) / Mathf.Max(bounds.size.z, .00001f) * resolution), 0, resolution - 1);
        public bool Sample(Vector3 p, out float height, out Vector3 normal)
        {
            height = float.NegativeInfinity; normal = Vector3.up;
            if (p.x < bounds.min.x || p.x > bounds.max.x || p.z < bounds.min.z || p.z > bounds.max.z) return false;
            var candidates = cells[Z(p.z) * resolution + X(p.x)];
            if (candidates == null) return false;
            foreach (int i in candidates)
            {
                int ia = triangles[i], ib = triangles[i + 1], ic = triangles[i + 2];
                Vector3 a = vertices[ia], b = vertices[ib], c = vertices[ic];
                float den = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
                if (Mathf.Abs(den) < 1e-10f) continue;
                float u = ((b.z - c.z) * (p.x - c.x) + (c.x - b.x) * (p.z - c.z)) / den;
                float v = ((c.z - a.z) * (p.x - c.x) + (a.x - c.x) * (p.z - c.z)) / den;
                float w = 1 - u - v;
                if (u < -.00001f || v < -.00001f || w < -.00001f) continue;
                float y = u * a.y + v * b.y + w * c.y;
                if (y <= height) continue;
                height = y;
                normal = normals.Length == vertices.Length ? (u * normals[ia] + v * normals[ib] + w * normals[ic]).normalized : Vector3.Cross(b - a, c - a).normalized;
                if (normal.y < 0) normal = -normal;
            }
            return !float.IsNegativeInfinity(height);
        }
    }
}
