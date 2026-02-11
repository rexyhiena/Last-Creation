using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Clase compartida para almacenar datos de mesh de voxels
/// </summary>
public class VoxelMeshData
{
    public List<Vector3> vertices = new List<Vector3>();
    public List<int> triangles = new List<int>();
    public List<Vector2> uvs = new List<Vector2>();
    public List<Vector3> normals = new List<Vector3>();

    public void Clear()
    {
        vertices.Clear();
        triangles.Clear();
        uvs.Clear();
        normals.Clear();
    }

    public Mesh CreateMesh()
    {
        if (vertices.Count == 0) return null;

        Mesh mesh = new Mesh
        {
            indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
        };

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, uvs);
        
        if (normals.Count == vertices.Count)
        {
            mesh.SetNormals(normals);
        }
        else
        {
            mesh.RecalculateNormals();
        }

        mesh.RecalculateBounds();
        mesh.RecalculateTangents();

        return mesh;
    }
}
