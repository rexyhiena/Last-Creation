using UnityEngine;
using System.Collections.Generic;
using Unity.Mathematics;   // ← AGREGADO: necesario para int3

/// <summary>
/// Greedy Mesher SIMPLE + separación por blockId (sin Burst/Collections)
/// </summary>
[BurstCompile]
public struct GreedyMesher : IJob
{
    [ReadOnly] public NativeArray<ushort> blockIds;
    public int3 chunkSize;
    public NativeArray<MeshData> outputMeshData; // Usamos NativeArray para pasar la MeshData

    public void Execute()
    {
        // Inicializar las listas nativas para la malla dentro del Job
        // Allocator.TempJob es adecuado para datos que se usan y se liberan dentro del Job
        MeshData meshData = new MeshData(Allocator.TempJob);

        // Arrays para almacenar las caras ya procesadas en cada dirección
        // Esto evita generar caras duplicadas
        NativeArray<bool> visited = new NativeArray<bool>(chunkSize.x * chunkSize.y * chunkSize.z, Allocator.Temp);

        // Iterar sobre cada dirección (6 caras por bloque)
        // Para cada dirección, creamos una máscara 2D y aplicamos el algoritmo Greedy Meshing

        // Direcciones de las caras (normales)
        float3[] normals = new float3[]
        {
            new float3(0, 0, 1),  // +Z
            new float3(0, 0, -1), // -Z
            new float3(0, 1, 0),  // +Y
            new float3(0, -1, 0), // -Y
            new float3(1, 0, 0),  // +X
            new float3(-1, 0, 0)  // -X
        };

        // Vectores para construir las caras (right, up)
        float3[] faceVectorsRight = new float3[]
        {
            new float3(1, 0, 0), new float3(-1, 0, 0), new float3(1, 0, 0), new float3(1, 0, 0), new float3(0, 0, 1), new float3(0, 0, -1)
        };
        float3[] faceVectorsUp = new float3[]
        {
            new float3(0, 1, 0), new float3(0, 1, 0), new float3(0, 0, 1), new float3(0, 0, 1), new float3(0, 1, 0), new float3(0, 1, 0)
        };

        // Orígenes de las caras (para el primer vértice)
        float3[] faceOrigins = new float3[]
        {
            new float3(0, 0, 1), new float3(0, 0, 0), new float3(0, 1, 0), new float3(0, 0, 0), new float3(1, 0, 0), new float3(0, 0, 0)
        };

        for (int faceIndex = 0; faceIndex < 6; faceIndex++)
        {
            float3 normal = normals[faceIndex];
            float3 right = faceVectorsRight[faceIndex];
            float3 up = faceVectorsUp[faceIndex];
            float3 originOffset = faceOrigins[faceIndex];

            // Crear una máscara 2D para la rebanada actual
            // El tamaño de la máscara dependerá de la dirección de la cara
            int maskDim1 = 0, maskDim2 = 0, sliceDim = 0;

            if (math.abs(normal.x) > 0.5f) // Caras en X
            {
                maskDim1 = chunkSize.y; maskDim2 = chunkSize.z; sliceDim = chunkSize.x;
            }
            else if (math.abs(normal.y) > 0.5f) // Caras en Y
            {
                maskDim1 = chunkSize.x; maskDim2 = chunkSize.z; sliceDim = chunkSize.y;
            }
            else // Caras en Z
            {
                maskDim1 = chunkSize.x; maskDim2 = chunkSize.y; sliceDim = chunkSize.z;
            }

            NativeArray<ushort> mask = new NativeArray<ushort>(maskDim1 * maskDim2, Allocator.Temp);

            for (int k = 0; k < sliceDim; k++) // Iterar a través de las rebanadas del chunk
            {
                // Reiniciar la máscara para cada rebanada
                for (int i = 0; i < mask.Length; i++) mask[i] = 0;

                for (int j = 0; j < maskDim2; j++)
                {
                    for (int i = 0; i < maskDim1; i++)
                    {
                        int3 currentBlockPos = GetBlockPosFromMaskCoords(i, j, k, normal, chunkSize);
                        int3 neighborBlockPos = currentBlockPos + (int3)normal; // Bloque adyacente en la dirección de la normal

                        ushort currentBlockId = GetBlockId(currentBlockPos);
                        ushort neighborBlockId = GetBlockId(neighborBlockPos);

                        // Si el bloque actual es sólido y el vecino es aire (o fuera del chunk), entonces la cara es visible
                        if (currentBlockId != 0 && neighborBlockId == 0)
                        {
                            mask[GetMaskIndex(i, j, maskDim1)] = currentBlockId; // Usar el ID del bloque como valor en la máscara
                        }
                    }
                }

                // Aplicar Greedy Meshing a la máscara 2D
                for (int j = 0; j < maskDim2; j++)
                {
                    for (int i = 0; i < maskDim1; i++)
                    {
                        ushort blockType = mask[GetMaskIndex(i, j, maskDim1)];

                        if (blockType != 0) // Si hay una cara visible en esta posición
                        {
                            // Encontrar el ancho del rectángulo
                            int width = 1;
                            while (i + width < maskDim1 && mask[GetMaskIndex(i + width, j, maskDim1)] == blockType)
                            {
                                width++;
                            }

                            // Encontrar la altura del rectángulo
                            int height = 1;
                            bool done = false;
                            while (j + height < maskDim2 && !done)
                            {
                                for (int x = 0; x < width; x++)
                                {
                                    if (mask[GetMaskIndex(i + x, j + height, maskDim1)] != blockType)
                                    {
                                        done = true;
                                        break;
                                    }
                                }
                                if (!done) height++;
                            }

                            // Añadir el quad a la malla
                            AddQuad(i, j, k, width, height, normal, right, up, originOffset, blockType, ref meshData, maskDim1, maskDim2, chunkSize);

                            // Marcar las celdas de la máscara como procesadas
                            for (int x = 0; x < width; x++)
                            {
                                for (int y = 0; y < height; y++)
                                {
                                    mask[GetMaskIndex(i + x, j + y, maskDim1)] = 0;
                                }
                            }

                            i += width - 1; // Avanzar 'i' para no reprocesar este rectángulo
                        }
                    }
                }
            }
            mask.Dispose();
        }
        visited.Dispose();

        // Asignar los datos de la malla al NativeArray de salida
        outputMeshData[0] = meshData;
    }

    // Helper para obtener el ID del bloque de forma segura
    private ushort GetBlockId(int3 pos)
    {
        if (pos.x < 0 || pos.x >= chunkSize.x ||
            pos.y < 0 || pos.y >= chunkSize.y ||
            pos.z < 0 || pos.z >= chunkSize.z)
        {
            return 0; // Fuera de los límites del chunk, considerar como aire
        }
        return blockIds[pos.x + chunkSize.x * (pos.y + chunkSize.y * pos.z)];
    }

    // Helper para obtener la posición del bloque a partir de las coordenadas de la máscara
    private int3 GetBlockPosFromMaskCoords(int i, int j, int k, float3 normal, int3 chunkSize)
    {
        if (math.abs(normal.x) > 0.5f) return new int3((int)(normal.x > 0 ? k : k), i, j);
        if (math.abs(normal.y) > 0.5f) return new int3(i, (int)(normal.y > 0 ? k : k), j);
        return new int3(i, j, (int)(normal.z > 0 ? k : k));
    }

    // Helper para obtener el índice en la máscara 2D
    private int GetMaskIndex(int i, int j, int maskDim1)
    {
        return i + j * maskDim1;
    }

    // Añade un quad a la malla
    private void AddQuad(int i, int j, int k, int width, int height, float3 normal, float3 right, float3 up, float3 originOffset, ushort blockType, ref MeshData meshData, int maskDim1, int maskDim2, int3 chunkSize)
    {
        int3 baseBlockPos = GetBlockPosFromMaskCoords(i, j, k, normal, chunkSize);

        // Calcular los 4 vértices del quad
        float3 v0 = baseBlockPos + originOffset;
        float3 v1 = v0 + right * width;
        float3 v2 = v0 + up * height;
        float3 v3 = v0 + right * width + up * height;

        int vertexCount = meshData.vertices.Length;

        meshData.vertices.Add(v0);
        meshData.vertices.Add(v1);
        meshData.vertices.Add(v2);
        meshData.vertices.Add(v3);

        // Triángulos
        meshData.triangles.Add(vertexCount + 0);
        meshData.triangles.Add(vertexCount + 2);
        meshData.triangles.Add(vertexCount + 1);
        meshData.triangles.Add(vertexCount + 2);
        meshData.triangles.Add(vertexCount + 3);
        meshData.triangles.Add(vertexCount + 1);

        // Normales
        meshData.normals.Add(normal);
        meshData.normals.Add(normal);
        meshData.normals.Add(normal);
        meshData.normals.Add(normal);

        // UVs (simplificado, puedes adaptarlo para texturas de atlas)
        meshData.uvs.Add(new float2(0, 0));
        meshData.uvs.Add(new float2(width, 0));
        meshData.uvs.Add(new float2(0, height));
        meshData.uvs.Add(new float2(width, height));
    }
}
