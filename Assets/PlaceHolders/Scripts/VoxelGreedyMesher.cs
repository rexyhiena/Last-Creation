using System.Collections.Generic;
using UnityEngine;

public static class VoxelGreedyMesher
{

    // Usa la clase compartida VoxelMeshData

    private static readonly int[,] FaceDirections = new int[6, 3]
    {
        { 1,  0,  0 }, {-1,  0,  0 },
        { 0,  1,  0 }, { 0, -1,  0 },
        { 0,  0,  1 }, { 0,  0, -1 }
    };

    private static readonly Vector3[] FaceNormalVectors = new Vector3[6]
    {
        Vector3.right, Vector3.left,
        Vector3.up,    Vector3.down,
        Vector3.forward, Vector3.back
    };

    public static void Generate(
        ushort[] blockIds,
        byte[] healthLevels,
        Vector3Int chunkSize,
        Dictionary<ushort, VoxelMeshData> outputGroups)
    {
        foreach (var md in outputGroups.Values) md.Clear();
        outputGroups.Clear();

        int[] size = { chunkSize.x, chunkSize.y, chunkSize.z };

        for (int face = 0; face < 6; face++)
        {
            int axis  = (FaceDirections[face, 0] != 0) ? 0 : (FaceDirections[face, 1] != 0 ? 1 : 2);
            int sign  = FaceDirections[face, axis];
            int uAxis = (axis + 1) % 3;
            int vAxis = (axis + 2) % 3;

            int width  = size[uAxis];
            int height = size[vAxis];
            int depth  = size[axis];

            var mask = new ushort[width * height];

            int start = (sign > 0) ? 0 : depth - 1;
            int end   = (sign > 0) ? depth     : -1;
            int step  = sign;

            for (int slice = start; slice != end; slice += step)
            {
                BuildFaceMask(blockIds, size, axis, sign, uAxis, vAxis, slice, mask);

                GreedyQuadExtraction(mask, width, height, outputGroups,
                    axis, sign, slice, uAxis, vAxis, face);
            }
        }
    }

    private static void BuildFaceMask(
        ushort[] blockIds, int[] size,
        int axis, int sign, int uAxis, int vAxis, int slice,
        ushort[] mask)
    {
        int idx = 0;
        for (int py = 0; py < mask.Length / size[uAxis]; py++)
        {
            for (int px = 0; px < size[uAxis]; px++)
            {
                int x = (axis == 0) ? slice : (uAxis == 0 ? px : (vAxis == 0 ? py : 0));
                int y = (axis == 1) ? slice : (uAxis == 1 ? px : (vAxis == 1 ? py : 0));
                int z = (axis == 2) ? slice : (uAxis == 2 ? px : (vAxis == 2 ? py : 0));

                ushort id = SafeGetBlock(blockIds, x, y, z, size);

                int nx = x + sign * (axis == 0 ? 1 : 0);
                int ny = y + sign * (axis == 1 ? 1 : 0);
                int nz = z + sign * (axis == 2 ? 1 : 0);
                ushort neighborId = SafeGetBlock(blockIds, nx, ny, nz, size);

                bool shouldRender = (id != 0) && (neighborId == 0);
                mask[idx++] = shouldRender ? id : (ushort)0;
            }
        }
    }

    private static void GreedyQuadExtraction(
        ushort[] mask, int width, int height,
        Dictionary<ushort, VoxelMeshData> groups,
        int axis, int sign, int slice,
        int uAxis, int vAxis, int faceIndex)
    {
        int j = 0;
        while (j < height)
        {
            int i = 0;
            while (i < width)
            {
                int idx = i + j * width;
                ushort id = mask[idx];
                if (id == 0) { i++; continue; }

                int quadWidth = 1;
                while (i + quadWidth < width && mask[idx + quadWidth] == id)
                    quadWidth++;

                int quadHeight = 1;
                while (j + quadHeight < height)
                {
                    bool rowOk = true;
                    for (int k = 0; k < quadWidth; k++)
                    {
                        if (mask[idx + k + quadHeight * width] != id)
                        {
                            rowOk = false;
                            break;
                        }
                    }
                    if (!rowOk) break;
                    quadHeight++;
                }

                Vector3Int origin = new Vector3Int(
                    axis == 0 ? slice : (uAxis == 0 ? i : (vAxis == 0 ? j : 0)),
                    axis == 1 ? slice : (uAxis == 1 ? i : (vAxis == 1 ? j : 0)),
                    axis == 2 ? slice : (uAxis == 2 ? i : (vAxis == 2 ? j : 0))
                );

                if (sign < 0) origin[axis] += sign;

                Vector3 v0 = origin;
                Vector3 right = Vector3.zero; right[uAxis] = quadWidth;
                Vector3 up    = Vector3.zero; up[vAxis]   = quadHeight;

                AddQuad(groups, id, v0, right, up, FaceNormalVectors[faceIndex], sign > 0);

                for (int ly = 0; ly < quadHeight; ly++)
                    for (int lx = 0; lx < quadWidth; lx++)
                        mask[i + lx + (j + ly) * width] = 0;

                i += quadWidth;
            }
            j++;
        }
    }

    private static void AddQuad(
        Dictionary<ushort, VoxelMeshData> groups,
        ushort blockId,
        Vector3 origin, Vector3 right, Vector3 up,
        Vector3 normal, bool isPositiveFace)
    {
        if (!groups.TryGetValue(blockId, out var data))
        {
            data = new VoxelMeshData();
            groups[blockId] = data;
        }

        int baseIdx = data.vertices.Count;

        data.vertices.Add(origin);
        data.vertices.Add(origin + right);
        data.vertices.Add(origin + up);
        data.vertices.Add(origin + right + up);

        data.uvs.Add(new Vector2(0, 0));
        data.uvs.Add(new Vector2(right.magnitude, 0));
        data.uvs.Add(new Vector2(0, up.magnitude));
        data.uvs.Add(new Vector2(right.magnitude, up.magnitude));

        data.normals.Add(normal);
        data.normals.Add(normal);
        data.normals.Add(normal);
        data.normals.Add(normal);

        if (isPositiveFace)
        {
            data.triangles.Add(baseIdx + 0); data.triangles.Add(baseIdx + 2); data.triangles.Add(baseIdx + 1);
            data.triangles.Add(baseIdx + 1); data.triangles.Add(baseIdx + 2); data.triangles.Add(baseIdx + 3);
        }
        else
        {
            data.triangles.Add(baseIdx + 0); data.triangles.Add(baseIdx + 1); data.triangles.Add(baseIdx + 2);
            data.triangles.Add(baseIdx + 1); data.triangles.Add(baseIdx + 3); data.triangles.Add(baseIdx + 2);
        }
    }

    private static ushort SafeGetBlock(ushort[] blocks, int x, int y, int z, int[] size)
    {
        if (x < 0 || x >= size[0] || y < 0 || y >= size[1] || z < 0 || z >= size[2])
            return 0;
        return blocks[x + size[0] * (y + size[1] * z)];
    }
}