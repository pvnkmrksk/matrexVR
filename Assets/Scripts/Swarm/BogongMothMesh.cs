using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>48 body triangles, one coupled left/right wing pair (two quads) and a subpixel proxy. No Animator.</summary>
public static class BogongMothMesh
{
    public const int VerticesPerAgent = 44; // 4 rings * 8 body vertices + 8 wings + 4 proxy.
    public const int IndicesPerAgent = 162; // 48 body + 4 wing + 2 proxy triangles.
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct PoseVertex
    {
        public Vector3 center;
        public Vector4 rotation;
        public Vector2 phase;
    }
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct ShapeVertex
    {
        public Vector2 part;
        public Vector3 local;
    }
    // Static shape and dynamic pose live in separate buffers. Only live poses are uploaded.
    public static Mesh CreateBatch(int count)
    {
        Mesh mesh = Create(count);
        int[] indices = mesh.triangles;
        var parts = new List<Vector2>(); var local = new List<Vector3>();
        mesh.GetUVs(0,parts); mesh.GetUVs(2,local);
        var shapes = new ShapeVertex[mesh.vertexCount];
        for (int i=0;i<shapes.Length;i++) shapes[i] = new ShapeVertex { part=parts[i], local=local[i] };
        mesh.SetVertexBufferParams(shapes.Length,
            new VertexAttributeDescriptor(VertexAttribute.Position,VertexAttributeFormat.Float32,3,0),
            new VertexAttributeDescriptor(VertexAttribute.Tangent,VertexAttributeFormat.Float32,4,0),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0,VertexAttributeFormat.Float32,2,1),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord1,VertexAttributeFormat.Float32,2,0),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord2,VertexAttributeFormat.Float32,3,1));
        mesh.SetVertexBufferData(shapes,0,0,shapes.Length,1,MeshUpdateFlags.DontRecalculateBounds);
        mesh.SetTriangles(indices,0,false);
        return mesh;
    }
    public static Mesh Create(int count)
    {
        var vertices = new Vector3[count * VerticesPerAgent];
        var local = new List<Vector3>(vertices.Length);
        var parts = new List<Vector2>(vertices.Length);
        var indices = new List<int>(count * IndicesPerAgent);
        for (int i = 0; i < count; i++)
        {
            int start = i * VerticesPerAgent;
            for (int ring = 0; ring < 4; ring++)
            {
                float latitude = -Mathf.PI / 2 + ring * Mathf.PI / 3;
                for (int side = 0; side < 8; side++)
                {
                    float angle = side * Mathf.PI / 4;
                    // Four rings otherwise reach only cos(pi/6)=.866 of the configured
                    // diameter. Normalize the interior rings to the stated thickness.
                    float radius = ring == 0 || ring == 3 ? 0 : 1;
                    local.Add(new Vector3(radius * Mathf.Cos(angle),
                        radius * Mathf.Sin(angle), Mathf.Sin(latitude)));
                    parts.Add(Vector2.zero);
                }
            }
            for (int ring = 0; ring < 3; ring++)
                for (int side = 0; side < 8; side++)
                {
                    int a = start + ring * 8 + side, b = start + ring * 8 + (side + 1) % 8;
                    int c = a + 8, d = b + 8;
                    indices.AddRange(new[] { a, b, c, b, d, c });
                }
            for (int wing = 0; wing < 2; wing++)
            {
                float sign = wing == 0 ? -1 : 1;
                local.Add(new Vector3(0, 0, -.35f)); local.Add(new Vector3(sign, 0, -.25f));
                local.Add(new Vector3(sign, 0, .25f)); local.Add(new Vector3(0, 0, .35f));
                for (int j = 0; j < 4; j++) parts.Add(new Vector2(1, 0));
                int v = start + 32 + wing * 4;
                indices.AddRange(new[] { v, v + 1, v + 2, v, v + 2, v + 3 });
            }
            local.Add(new Vector3(-1,-1,0)); local.Add(new Vector3(1,-1,0));
            local.Add(new Vector3(1,1,0)); local.Add(new Vector3(-1,1,0));
            for (int j = 0; j < 4; j++) parts.Add(new Vector2(2, 0));
            int proxy = start + 40;
            indices.AddRange(new[] { proxy, proxy + 1, proxy + 2, proxy, proxy + 2, proxy + 3 });
        }
        var mesh = new Mesh { name = "Batched Bogong moth bodies and wings", indexFormat = IndexFormat.UInt32 };
        mesh.MarkDynamic(); mesh.vertices = vertices;
        mesh.SetUVs(0, parts); mesh.SetUVs(2, local); mesh.SetTriangles(indices, 0);
        return mesh;
    }
}
