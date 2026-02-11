using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;

/// <summary>
/// Chunk ULTRA-OPTIMIZADO para Unity 6 - Versión Completa
/// </summary>
public class Chunk : MonoBehaviour
{
    #region --- Campos ---

    public Vector3 location;
    public bool isModified = false;

    public int width = 16;
    public int height = 128;
    public int depth = 16;

    public ushort[] blockIds;
    public byte[] healthLevels;

    private GameObject solidMeshObject;
    private MeshRenderer meshRendererSolid;
    private MeshFilter solidMeshFilter;
    private MeshCollider meshCollider;

    private List<GameObject> entityObjects = new List<GameObject>();
    private World world;

    private bool isGenerating = false;

    #endregion

    #region --- Pool de Meshes ---

    private static Queue<Mesh> meshPool;
    private const int MESH_POOL_SIZE = 50;

    private static void EnsureMeshPoolInitialized()
    {
        if (meshPool != null) return;

        meshPool = new Queue<Mesh>(MESH_POOL_SIZE);

        for (int i = 0; i < MESH_POOL_SIZE; i++)
        {
            var mesh = new Mesh
            {
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
            };
            meshPool.Enqueue(mesh);
        }
    }

    private static Mesh GetPooledMesh()
    {
        if (meshPool == null)
        {
            EnsureMeshPoolInitialized();
        }

        if (meshPool.Count > 0)
        {
            var mesh = meshPool.Dequeue();
            mesh.Clear();
            return mesh;
        }

        return new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
    }

    private static void ReturnMeshToPool(Mesh mesh)
    {
        if (mesh != null && meshPool != null && meshPool.Count < MESH_POOL_SIZE)
        {
            mesh.Clear();
            meshPool.Enqueue(mesh);
        }
    }

    #endregion

    #region --- Lifecycle ---

    private void Awake()
    {
        EnsureMeshPoolInitialized();
        world = FindObjectOfType<World>();
        InitializeMeshObjects();
    }

    private void OnDisable()
    {
        ClearEntities();
        
        if (solidMeshFilter != null && solidMeshFilter.sharedMesh != null)
        {
            ReturnMeshToPool(solidMeshFilter.sharedMesh);
            solidMeshFilter.sharedMesh = null;
        }

        if (meshCollider != null && meshCollider.sharedMesh != null)
        {
            ReturnMeshToPool(meshCollider.sharedMesh);
            meshCollider.sharedMesh = null;
        }
    }

    #endregion

    #region --- Public API ---

    public void CreateChunk(Vector3Int dimensions, Vector3 position)
    {
        this.width = dimensions.x;
        this.height = dimensions.y;
        this.depth = dimensions.z;
        this.location = position;
        this.isModified = false;
        
        BuildChunkData();
    }

    public void CreateChunkFromSave(Vector3 position, ChunkSaveData savedData)
    {
        this.width = World.chunkDimensions.x;
        this.height = World.chunkDimensions.y;
        this.depth = World.chunkDimensions.z;
        this.location = position;
        this.isModified = false;
        this.blockIds = savedData.blockIds;
        this.healthLevels = savedData.healthLevels;
        
        RegenerateVisuals();
    }

    public void RegenerateVisuals()
    {
        if (blockIds == null || isGenerating) return;
        GenerateMeshAndEntities();
    }

    #endregion

    #region --- Generación de Datos ---

    private void BuildChunkData()
    {
        int totalBlocks = width * height * depth;
        blockIds = new ushort[totalBlocks];
        healthLevels = new byte[totalBlocks];

        // Usamos Allocator.TempJob para que la memoria se gestione automáticamente
        NativeArray<ushort> nativeBlockIds = new NativeArray<ushort>(totalBlocks, Allocator.TempJob);

        // Obtenemos parámetros del World
        NoiseParameters surfaceNoiseParams = World.SurfaceNoise;
        NoiseParameters stoneNoiseParams = World.StoneNoise;
        NoiseParameters caveNoiseParams = World.CaveNoise;

        var genJob = new TerrainGenerationJob
        {
            BlockIds = nativeBlockIds,
            ChunkSize = new int3(width, height, depth),
            WorldPosition = location,
            Seed = World.currentSeed,
            SurfaceNoise = surfaceNoiseParams,
            StoneNoise = stoneNoiseParams,
            CaveNoise = caveNoiseParams
        };

        // Ejecutamos el Job
        JobHandle handle = genJob.Schedule(totalBlocks, 64);
        handle.Complete(); 

        // Copiamos resultados y liberamos memoria nativa
        nativeBlockIds.CopyTo(blockIds);
        nativeBlockIds.Dispose();

        // Una vez tenemos los datos, generamos la visualización
        RegenerateVisuals();
    }

    #endregion

    #region --- Generación de Mesh ---

    private void GenerateMeshAndEntities()
    {
        if (isGenerating) return;
        isGenerating = true;

        ClearEntities();

        ushort[] voxelBlockIds = (ushort[])blockIds.Clone();

        ExtractEntities(voxelBlockIds);

        var solidGroups = new Dictionary<ushort, VoxelMeshData>();
        
        GreedyMesher.GenerateGreedyMeshGroups(
            voxelBlockIds,
            healthLevels,
            new Vector3Int(width, height, depth),
            solidGroups
        );

        CombineAndApplyMeshes(solidGroups);

        isGenerating = false;
    }

    private void ExtractEntities(ushort[] voxelBlockIds)
    {
        for (int i = 0; i < voxelBlockIds.Length; i++)
        {
            ushort id = voxelBlockIds[i];
            if (id == 0) continue;

            BlockDefinition def = BlockRegistry.Instance.GetBlockDefinition(id);
            if (def != null && def.isEntity)
            {
                Vector3 localPos = GetPositionFromIndex(i);
                Vector3 worldPos = location + localPos;

                GameObject entityPrefab = def.customModel;
                if (entityPrefab != null)
                {
                    GameObject newEntity = Instantiate(entityPrefab, worldPos, Quaternion.identity, transform);
                    entityObjects.Add(newEntity);

                    var obstacle = newEntity.AddComponent<NavMeshObstacle>();
                    obstacle.carving = true;
                    obstacle.carveOnlyStationary = true;
                    obstacle.shape = NavMeshObstacleShape.Box;
                    obstacle.size = new Vector3(1.8f, 4f, 1.8f);
                    obstacle.center = new Vector3(0f, 2f, 0f);
                }

                voxelBlockIds[i] = 0;
            }
        }
    }

    private void CombineAndApplyMeshes(Dictionary<ushort, VoxelMeshData> solidGroups)
    {
        var combineInstances = new List<CombineInstance>();
        var materials = new List<Material>();

        foreach (var kvp in solidGroups)
        {
            var meshData = kvp.Value;
            if (meshData.vertices.Count < 3) continue;

            Mesh submesh = meshData.CreateMesh();
            if (submesh == null || submesh.vertexCount == 0) continue;

            combineInstances.Add(new CombineInstance
            {
                mesh = submesh,
                transform = Matrix4x4.identity
            });

            Material mat = BlockRegistry.Instance.GetMaterial(kvp.Key);
            if (mat != null) materials.Add(mat);
        }

        if (combineInstances.Count > 0)
        {
            Mesh finalMesh = GetPooledMesh();
            finalMesh.name = $"Chunk_{location.x}_{location.y}_{location.z}";
            finalMesh.CombineMeshes(combineInstances.ToArray(), true, false);
            
            finalMesh.RecalculateBounds();

            Mesh colliderMesh = FilterTopFaces(finalMesh);

            solidMeshFilter.sharedMesh = finalMesh;
            meshRendererSolid.sharedMaterials = materials.ToArray();

            if (meshCollider != null)
            {
                if (meshCollider.sharedMesh != null)
                    ReturnMeshToPool(meshCollider.sharedMesh);
                
                meshCollider.sharedMesh = colliderMesh;
            }

            solidMeshObject.SetActive(true);
        }
        else
        {
            solidMeshFilter.sharedMesh = null;
            solidMeshObject.SetActive(false);
        }
    }

    private Mesh FilterTopFaces(Mesh original)
    {
        Vector3[] verts = original.vertices;
        int[] tris = original.triangles;
        Vector3[] norms = original.normals;

        List<Vector3> newVerts = new List<Vector3>(verts.Length / 2);
        List<int> newTris = new List<int>(tris.Length / 2);
        Dictionary<int, int> vertMap = new Dictionary<int, int>(verts.Length / 2);

        for (int i = 0; i < tris.Length; i += 3)
        {
            Vector3 avgNormal = (norms[tris[i]] + norms[tris[i + 1]] + norms[tris[i + 2]]) / 3f;

            if (Mathf.Abs(avgNormal.y) > 0.1f)
            {
                for (int j = 0; j < 3; j++)
                {
                    int oldIdx = tris[i + j];
                    if (!vertMap.ContainsKey(oldIdx))
                    {
                        vertMap[oldIdx] = newVerts.Count;
                        newVerts.Add(verts[oldIdx]);
                    }
                    newTris.Add(vertMap[oldIdx]);
                }
            }
        }

        if (newTris.Count == 0) return original;

        Mesh filtered = GetPooledMesh();
        filtered.vertices = newVerts.ToArray();
        filtered.triangles = newTris.ToArray();
        filtered.RecalculateBounds();
        filtered.RecalculateNormals();

        return filtered;
    }

    #endregion

    #region --- Utilidades ---

    private void ClearEntities()
    {
        foreach (var entity in entityObjects)
        {
            if (entity != null) Destroy(entity);
        }
        entityObjects.Clear();
    }

    private void InitializeMeshObjects()
    {
        if (solidMeshObject == null)
        {
            solidMeshObject = new GameObject("Solid");
            solidMeshObject.transform.SetParent(transform, false);
            solidMeshFilter = solidMeshObject.AddComponent<MeshFilter>();
            meshRendererSolid = solidMeshObject.AddComponent<MeshRenderer>();
            meshCollider = solidMeshObject.AddComponent<MeshCollider>();
        }
    }

    public void SetBlock(int x, int y, int z, ushort blockId)
    {
        if (x < 0 || x >= width || y < 0 || y >= height || z < 0 || z >= depth) return;
        
        int index = x + width * (y + depth * z);
        if (blockIds[index] != blockId)
        {
            blockIds[index] = blockId;
            isModified = true;
            if (world != null) world.MarkChunkAsModified(this);
        }
    }

    public ushort GetBlock(int x, int y, int z)
    {
        if (x < 0 || x >= width || y < 0 || y >= height || z < 0 || z >= depth) return 0;
        return blockIds[x + width * (y + depth * z)];
    }

    private Vector3 GetPositionFromIndex(int index)
    {
        int x = index % width;
        int y = (index / width) % height;
        int z = index / (width * height);
        return new Vector3(x, y, z);
    }

    public ChunkSaveData GetSaveData() => new ChunkSaveData 
    { 
        blockIds = this.blockIds, 
        healthLevels = this.healthLevels, 
        version = 1 
    };

    #endregion
}

