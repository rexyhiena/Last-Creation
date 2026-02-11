using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AI;
using Unity.AI.Navigation;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;

/// <summary>
/// World Manager - OPTIMIZADO con Burst y Jobs
/// </summary>
public class World : MonoBehaviour
{
    #region --- Configuración Estática ---
    
    public static Vector3Int chunkDimensions = new Vector3Int(16, 128, 16);
    public static int currentSeed;
    
    #endregion
    
    #region --- Inspector Settings ---
    
    [Header("Core Settings")]
    [SerializeField] private GameObject chunkPrefab;
    public GameObject fpc;
    [SerializeField] private CharacterController characterController;
    
    [Header("World Generation")]
    [SerializeField] private bool useRandomSeed = true;
    [SerializeField] private int manualSeed = 12345;
    [SerializeField] [Tooltip("Radio de chunks cargados")]
    private int drawRadius = 8;
    [SerializeField] private bool loadOnStart = true;
    [SerializeField] private int worldHeight = 2;
    
    [Header("Minecraft-Style Generation")]
    [SerializeField] private int bedrockHeight = 0;
    [SerializeField] private int seaLevel = 62;
    [SerializeField] private int maxTerrainHeight = 100;
    
    [Header("Surface Details")]
    [SerializeField] [Range(0f, 1f)] private float treeDensity = 0.02f;
    [SerializeField] private ushort[] treeBlockIds;
    
    [Space(10)]
    [SerializeField] [Range(0f, 1f)] private float detailDensity = 0.15f;
    [SerializeField] private ushort[] detailBlockIds;
    
    [Header("UI & Debug")]
    [SerializeField] private GameObject mCamera;
    [SerializeField] private Slider loadingBar;
    [SerializeField] private Text seedText;
    [SerializeField] private bool showDebugGUI = true;
    
    [Header("Noise Settings")]
    [SerializeField] private PerlinGrapher surface;
    [SerializeField] private PerlinGrapher stone;
    [SerializeField] private Perlin3DGrapher caves;
    [SerializeField] private Perlin3DGrapher vegetation;
    
    [Header("NavMesh Settings")]
    [SerializeField] private bool enableNavMesh = true;
    [SerializeField] private float navMeshUpdateInterval = 5f;
    [SerializeField] private int navMeshAgentTypeID = 0;
     
    #endregion
    
    #region --- Static Noise Parameters ---
    
    public static NoiseParameters SurfaceNoise;
    public static NoiseParameters StoneNoise;
    public static NoiseParameters CaveNoise;
    public static NoiseParameters VegetationNoise;
    
    #endregion
    
    #region --- Private Fields ---
    
    private Dictionary<Vector3Int, Chunk> chunks = new Dictionary<Vector3Int, Chunk>();
    private HashSet<Vector3Int> chunkChecker = new HashSet<Vector3Int>();
    private Queue<IEnumerator> buildQueue = new Queue<IEnumerator>();
    private HashSet<Chunk> modifiedChunks = new HashSet<Chunk>();
    
    private ChunkPool chunkPool;
    
    // NavMesh
    private NavMeshSurface navMeshSurface;
    private float lastNavMeshBakeTime = 0f;
    private bool needsNavMeshUpdate = false;
    private int chunksGeneratedSinceLastBake = 0;
    
    private Vector3Int lastPlayerChunkPos;
    
    private int chunksBuiltThisFrame = 0;
    private int totalChunksGenerated = 0;
    private float worldGenerationTime = 0f;
    
    private bool isWorldReady = false;
    private bool isInitialLoadComplete = false;
    
    #endregion
    
    #region --- Unity Lifecycle ---
    
    void Start()
    {
        StartCoroutine(InitializeWorld());
    }
    
    void Update()
    {
        if (enableNavMesh && needsNavMeshUpdate && 
            Time.time - lastNavMeshBakeTime > navMeshUpdateInterval &&
            chunksGeneratedSinceLastBake >= 5)
        {
            UpdateNavMesh();
        }
    }
    
    private void OnApplicationQuit()
    {
        if (isWorldReady)
            SaveGame();
    }
    
    #endregion
    
    #region --- Inicialización ---
    
    IEnumerator InitializeWorld()
    {
        float startTime = Time.realtimeSinceStartup;
        
        Debug.Log("🌍 Iniciando generación del mundo...");
        
        DisablePlayer();
        
        Vector3 spawnPosition = Vector3.zero;
        
        if (loadOnStart && WorldSaver.LoadWorldData(out WorldSaveData worldData))
        {
            currentSeed = worldData.seed;
            spawnPosition = worldData.playerPosition;
            
            if (characterController != null)
            {
                fpc.transform.eulerAngles = worldData.playerRotation;
            }
            
            Debug.Log($"📦 Mundo cargado | Seed: {currentSeed}");
        }
        else
        {
            if (useRandomSeed)
            {
                currentSeed = GenerateRandomSeed();
                Debug.Log($"🎲 Semilla aleatoria: {currentSeed}");
            }
            else
            {
                currentSeed = manualSeed;
                Debug.Log($"🔧 Semilla manual: {currentSeed}");
            }
            
            spawnPosition = CalculateSpawnAtOrigin();
            Debug.Log($"📍 Spawn en origen: {spawnPosition}");
        }
        
        if (seedText != null)
        {
            seedText.text = $"Seed: {currentSeed}";
        }
        
        InitializeNoiseParameters();
        yield return null;
        
        UnityEngine.Random.InitState(currentSeed);
        InitializeSystems();
        
        if (enableNavMesh)
            InitializeNavMesh();
        
        yield return null;
        
        fpc.transform.position = spawnPosition;
        lastPlayerChunkPos = WorldToChunkCoord(spawnPosition);
        
        if (loadingBar != null)
        {
            loadingBar.gameObject.SetActive(true);
            loadingBar.maxValue = CalculateInitialChunksCount();
            loadingBar.value = 0;
        }
        
        yield return StartCoroutine(LoadInitialChunks());
        
        if (enableNavMesh)
        {
            yield return new WaitForSeconds(1f);
            UpdateNavMesh();
        }
        
        EnablePlayer();
        
        if (mCamera != null) mCamera.SetActive(false);
        if (loadingBar != null) loadingBar.gameObject.SetActive(false);
        
        StartCoroutine(UpdateWorld());
        StartCoroutine(BuildCoordinator());
        
        isWorldReady = true;
        worldGenerationTime = Time.realtimeSinceStartup - startTime;
        
        Debug.Log($"✅ Mundo listo en {worldGenerationTime:F2}s | Seed: {currentSeed}");
    }
    
    private void InitializeNavMesh()
    {
        navMeshSurface = gameObject.AddComponent<NavMeshSurface>();
        navMeshSurface.agentTypeID = navMeshAgentTypeID;
        navMeshSurface.collectObjects = CollectObjects.All;
        navMeshSurface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
        navMeshSurface.layerMask = LayerMask.GetMask("Default");

        navMeshSurface.overrideVoxelSize = true;
        navMeshSurface.useGeometry = NavMeshCollectGeometry.RenderMeshes;


        navMeshSurface.overrideTileSize = true;
        navMeshSurface.voxelSize = 0.008f;
        navMeshSurface.tileSize = 512;

        navMeshSurface.buildHeightMesh = false;

        Debug.Log("🗺️ NavMeshSurface inicializado");
    }
    
    private void UpdateNavMesh()
    {
        if (navMeshSurface == null || !enableNavMesh) return;
        
        navMeshSurface.RemoveData();
        navMeshSurface.BuildNavMesh();
        
        lastNavMeshBakeTime = Time.time;
        needsNavMeshUpdate = false;
        chunksGeneratedSinceLastBake = 0;
        
        Debug.Log($"🗺️ NavMesh actualizado | Chunks: {chunks.Count}");
    }
    
    public void ForceNavMeshUpdate()
    {
        if (enableNavMesh)
            UpdateNavMesh();
    }
    
    private int GenerateRandomSeed()
    {
        long ticks = System.DateTime.Now.Ticks;
        int timeComponent = (int)(ticks & 0xFFFFFFFF);
        int randomComponent = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
        int seed = timeComponent ^ randomComponent;
        return seed == 0 ? 1 : seed;
    }
    
    private Vector3 CalculateSpawnAtOrigin()
    {
        float surfaceHeight = NoiseGenerator.GenerateHeight(0, 0, SurfaceNoise);
        int surfaceY = Mathf.FloorToInt(surfaceHeight);
        surfaceY = Mathf.Clamp(surfaceY, bedrockHeight + 5, maxTerrainHeight);
        return new Vector3(0.5f, surfaceY + 3f, 0.5f);
    }
    
    private void DisablePlayer()
    {
        fpc.SetActive(false);
        if (characterController == null)
            characterController = fpc.GetComponent<CharacterController>();
        if (characterController != null)
            characterController.enabled = false;
        Rigidbody rb = fpc.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }
    
    private void EnablePlayer()
    {
        fpc.SetActive(true);
        if (characterController != null)
            characterController.enabled = true;
        Rigidbody rb = fpc.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }
    }
    
    private int CalculateInitialChunksCount()
    {
        int horizontalChunks = (drawRadius * 2 + 1) * (drawRadius * 2 + 1);
        return horizontalChunks * worldHeight;
    }
    
    void InitializeSystems()
    {
        int poolSize = CalculateInitialChunksCount() + 200;
        chunkPool = gameObject.AddComponent<ChunkPool>();
        chunkPool.Initialize(chunkPrefab, poolSize);
    }
    
    void InitializeNoiseParameters()
    {
        SurfaceNoise = surface != null ? surface.GetNoiseParameters() : CreateDefaultSurfaceNoise();
        SurfaceNoise.seed = currentSeed;
        
        StoneNoise = stone != null ? stone.GetNoiseParameters() : CreateDefaultStoneNoise();
        StoneNoise.seed = currentSeed + 1000;
        
        CaveNoise = caves != null ? caves.GetNoiseParameters() : CreateDefaultCaveNoise();
        CaveNoise.seed = currentSeed + 2000;
        
        VegetationNoise = vegetation != null ? vegetation.GetNoiseParameters() : CreateDefaultVegetationNoise();
        VegetationNoise.seed = currentSeed + 3000;
    }
    
    private NoiseParameters CreateDefaultSurfaceNoise()
    {
        return new NoiseParameters { seed = currentSeed, scale = 0.008f, heightScale = 25f, octaves = 5, heightOffset = seaLevel, probability = 1f };
    }
    
    private NoiseParameters CreateDefaultStoneNoise()
    {
        return new NoiseParameters { seed = currentSeed + 1000, scale = 0.015f, heightScale = 15f, octaves = 4, heightOffset = seaLevel - 10, probability = 1f };
    }
    
    private NoiseParameters CreateDefaultCaveNoise()
    {
        return new NoiseParameters { seed = currentSeed + 2000, scale = 0.04f, heightScale = 1f, octaves = 3, heightOffset = 0f, probability = 0.5f };
    }
    
    private NoiseParameters CreateDefaultVegetationNoise()
    {
        return new NoiseParameters { seed = currentSeed + 3000, scale = 0.12f, heightScale = 1f, octaves = 2, heightOffset = 0f, probability = 0.5f };
    }
    
    #endregion
    
    #region --- Carga ---
    
    IEnumerator LoadInitialChunks()
    {
        Vector3Int playerChunkPos = lastPlayerChunkPos;
        int chunksLoaded = 0;
        
        for (int radius = 0; radius <= drawRadius; radius++)
        {
            for (int x = playerChunkPos.x - radius; x <= playerChunkPos.x + radius; x++)
            {
                for (int z = playerChunkPos.z - radius; z <= playerChunkPos.z + radius; z++)
                {
                    if (Mathf.Abs(x - playerChunkPos.x) != radius && Mathf.Abs(z - playerChunkPos.z) != radius)
                        continue;
                    
                    for (int y = 0; y < worldHeight; y++)
                    {
                        Vector3Int chunkWorldPos = new Vector3Int(x * chunkDimensions.x, y * chunkDimensions.y, z * chunkDimensions.z);
                        
                        if (!chunkChecker.Contains(chunkWorldPos))
                        {
                            yield return StartCoroutine(BuildChunkImmediate(chunkWorldPos));
                            chunkChecker.Add(chunkWorldPos);
                            chunksLoaded++;
                            
                            if (loadingBar != null)
                                loadingBar.value = chunksLoaded;
                            
                            if (chunksLoaded % 3 == 0)
                                yield return null;
                        }
                    }
                }
            }
        }
        
        isInitialLoadComplete = true;
    }
    
    IEnumerator BuildChunkImmediate(Vector3Int position)
    {
        GameObject chunkObj = chunkPool.GetChunk(position);
        
        if (chunkObj == null)
        {
            Debug.LogError($"❌ ChunkPool se quedó sin objetos en {position}");
            yield break;
        }
        
        Chunk chunk = chunkObj.GetComponent<Chunk>();
        
        if (chunk == null)
        {
            Debug.LogError($"❌ El prefab del Chunk no tiene componente 'Chunk' en {position}");
            yield break;
        }
        
        if (loadOnStart && WorldSaver.LoadChunkData(position, out ChunkSaveData savedData))
        {
            chunk.CreateChunkFromSave(position, savedData);
        }
        else
        {
            chunk.CreateChunk(chunkDimensions, position);
            
            if (position.y == (worldHeight - 1) * chunkDimensions.y)
            {
                GenerateSurfaceDetails(chunk);
            }
            
            chunk.RegenerateVisuals();
        }
        
        chunks.Add(position, chunk);
        totalChunksGenerated++;
        
        if (enableNavMesh)
        {
            chunksGeneratedSinceLastBake++;
            needsNavMeshUpdate = true;
        }
        
        yield return null;
    }
    
    #endregion
    
    #region --- Gestión ---
    
    IEnumerator BuildCoordinator()
    {
        while (true)
        {
            if (!isInitialLoadComplete)
            {
                yield return null;
                continue;
            }
            
            chunksBuiltThisFrame = 0;
            while (buildQueue.Count > 0 && chunksBuiltThisFrame < 2)
            {
                yield return StartCoroutine(buildQueue.Dequeue());
                chunksBuiltThisFrame++;
            }
            yield return null;
        }
    }
    
    IEnumerator UpdateWorld()
    {
        WaitForSeconds updateInterval = new WaitForSeconds(0.5f);
        while (true)
        {
            if (!isInitialLoadComplete)
            {
                yield return updateInterval;
                continue;
            }
            
            Vector3Int currentPlayerChunkPos = WorldToChunkCoord(fpc.transform.position);
            if (currentPlayerChunkPos != lastPlayerChunkPos)
            {
                lastPlayerChunkPos = currentPlayerChunkPos;
                LoadAndUnloadChunks(currentPlayerChunkPos);
            }
            yield return updateInterval;
        }
    }
    
    void LoadAndUnloadChunks(Vector3Int playerChunkPos)
    {
        for (int x = playerChunkPos.x - drawRadius; x <= playerChunkPos.x + drawRadius; x++)
        {
            for (int z = playerChunkPos.z - drawRadius; z <= playerChunkPos.z + drawRadius; z++)
            {
                for (int y = 0; y < worldHeight; y++)
                {
                    Vector3Int chunkWorldPos = new Vector3Int(x * chunkDimensions.x, y * chunkDimensions.y, z * chunkDimensions.z);
                    if (!chunkChecker.Contains(chunkWorldPos))
                    {
                        buildQueue.Enqueue(BuildChunkImmediate(chunkWorldPos));
                        chunkChecker.Add(chunkWorldPos);
                    }
                }
            }
        }
        UnloadDistantChunks(playerChunkPos);
    }
    
    private void UnloadDistantChunks(Vector3Int playerChunkPos)
    {
        List<Vector3Int> toUnload = new List<Vector3Int>();
        foreach (var chunkPos in chunks.Keys)
        {
            int chunkX = chunkPos.x / chunkDimensions.x;
            int chunkZ = chunkPos.z / chunkDimensions.z;
            float distance = Vector2.Distance(new Vector2(chunkX, chunkZ), new Vector2(playerChunkPos.x, playerChunkPos.z));
            if (distance > drawRadius + 3)
                toUnload.Add(chunkPos);
        }
        foreach (var pos in toUnload)
            UnloadChunk(pos);
    }
    
    private void UnloadChunk(Vector3Int pos)
    {
        if (chunks.TryGetValue(pos, out Chunk chunk))
        {
            if (chunk.isModified)
                WorldSaver.SaveChunkData(pos, chunk.GetSaveData());
            
            chunkPool.ReturnChunk(chunk.gameObject);
            chunks.Remove(pos);
            chunkChecker.Remove(pos);
            
            if (enableNavMesh)
            {
                chunksGeneratedSinceLastBake++;
                needsNavMeshUpdate = true;
            }
        }
    }
    
    #endregion
    
    #region --- Vegetación ---
    
    private void GenerateSurfaceDetails(Chunk chunk)
    {
        int seedForChunk = currentSeed + Mathf.FloorToInt(chunk.location.x) + Mathf.FloorToInt(chunk.location.z);
        System.Random random = new System.Random(seedForChunk);
        
        for (int x = 0; x < chunkDimensions.x; x++)
        {
            for (int z = 0; z < chunkDimensions.z; z++)
            {
                int surfaceY = FindSurfaceHeight(chunk, x, z);
                if (surfaceY == -1 || surfaceY >= chunkDimensions.y - 1)
                    continue;

                ushort surfaceBlockId = chunk.GetBlock(x, surfaceY, z);
                if (!CanSupportSurfaceDetails(surfaceBlockId))
                    continue;

                if (chunk.GetBlock(x, surfaceY + 1, z) != 0)
                    continue;

                float noise = (float)random.NextDouble();

                if (noise < treeDensity && treeBlockIds != null && treeBlockIds.Length > 0)
                    PlaceTree(chunk, x, surfaceY + 1, z, random);
                else if (noise < treeDensity + detailDensity && detailBlockIds != null && detailBlockIds.Length > 0)
                    PlaceDetailBlock(chunk, x, surfaceY + 1, z, random);
            }
        }
    }

    private int FindSurfaceHeight(Chunk chunk, int x, int z)
    {
        for (int y = chunkDimensions.y - 1; y >= 0; y--)
        {
            ushort blockId = chunk.GetBlock(x, y, z);
            if (blockId != 0)
            {
                BlockDefinition def = BlockRegistry.Instance.GetBlockDefinition(blockId);
                if (def != null && !def.isTransparent)
                    return y;
            }
        }
        return -1;
    }

    private bool CanSupportSurfaceDetails(ushort blockId)
    {
        return blockId == 1 || blockId == 2 || blockId == 28;
    }

    private void PlaceDetailBlock(Chunk chunk, int x, int y, int z, System.Random random)
    {
        ushort randomDetailId = detailBlockIds[random.Next(0, detailBlockIds.Length)];
        chunk.SetBlock(x, y, z, randomDetailId);
    }

    private void PlaceTree(Chunk chunk, int x, int y, int z, System.Random random)
    {
        ushort randomTreeId = treeBlockIds[random.Next(0, treeBlockIds.Length)];
        chunk.SetBlock(x, y, z, randomTreeId);
    }
    
    #endregion
    
    #region --- Guardado ---
    
    public void SaveGame()
    {
        if (!isWorldReady) return;
        WorldSaver.SaveWorld(this);
    }

    public int GetTotalChunksLoaded()
    {
        return totalChunksGenerated;
    }
    
    public void MarkChunkAsModified(Chunk chunk)
    {
        if (!modifiedChunks.Contains(chunk))
        {
            modifiedChunks.Add(chunk);
            chunk.isModified = true;
        }
    }
    
    public IEnumerable<Chunk> GetModifiedChunks()
    {
        return modifiedChunks;
    }
    
    public void ClearModifiedChunks()
    {
        foreach (var chunk in modifiedChunks)
            chunk.isModified = false;
        modifiedChunks.Clear();
    }
    
    #endregion
    
    #region --- Utilidades ---
    
    public Vector3Int WorldToChunkCoord(Vector3 worldPos)
    {
        return new Vector3Int(
            Mathf.FloorToInt(worldPos.x / chunkDimensions.x),
            Mathf.FloorToInt(worldPos.y / chunkDimensions.y),
            Mathf.FloorToInt(worldPos.z / chunkDimensions.z)
        );
    }
    
    public Chunk GetChunkAtPosition(Vector3Int chunkPos)
    {
        chunks.TryGetValue(chunkPos, out Chunk chunk);
        return chunk;
    }
    
    #endregion
    
    #region --- Debug GUI ---
    
    #if UNITY_EDITOR
    private void OnGUI()
    {
        if (!Application.isPlaying || !showDebugGUI)
            return;
        
        GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.normal.background = MakeTex(2, 2, new Color(0f, 0f, 0f, 0.7f));
        
        GUILayout.BeginArea(new Rect(10, 10, 340, 300), boxStyle);
        GUILayout.Label($"<b>🌍 World Manager</b>", GetHeaderStyle());
        GUILayout.Space(5);
        GUILayout.Label($"Status: {(isWorldReady ? "✅ Ready" : "⏳ Loading...")}");
        GUILayout.Label($"Seed: {currentSeed} {(useRandomSeed ? "(Random)" : "(Manual)")}");
        GUILayout.Label($"Player Chunk: {lastPlayerChunkPos}");
        GUILayout.Label($"Chunks Loaded: {chunks.Count}");
        GUILayout.Label($"Modified: {modifiedChunks.Count}");
        
        if (chunkPool != null)
            GUILayout.Label($"Pool: {chunkPool.GetStats()}");
        
        if (enableNavMesh)
        {
            GUILayout.Label($"NavMesh: {(navMeshSurface != null ? "✅" : "❌")}");
            if (needsNavMeshUpdate)
                GUILayout.Label($"Pending: {chunksGeneratedSinceLastBake} chunks");
        }
        
        GUILayout.Space(5);
        if (GUILayout.Button("💾 Save World") && isWorldReady)
            SaveGame();
        
        if (enableNavMesh && GUILayout.Button("🗺️ Update NavMesh") && isWorldReady)
            ForceNavMeshUpdate();
        
        if (GUILayout.Button("🎲 Copy Seed"))
            GUIUtility.systemCopyBuffer = currentSeed.ToString();
        
        GUILayout.EndArea();
    }
    
    private GUIStyle GetHeaderStyle()
    {
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.richText = true;
        style.fontSize = 14;
        style.fontStyle = FontStyle.Bold;
        return style;
    }
    
    private Texture2D MakeTex(int width, int height, Color col)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; i++)
            pix[i] = col;
        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }
    #endif
    
    #endregion
}
// --- ESTRUCTURAS DE DATOS PARA BURST ---

[BurstCompile]
public struct TerrainGenerationJob : IJobParallelFor
{
    public NativeArray<ushort> BlockIds;
    public int3 ChunkSize;
    public float3 WorldPosition;
    public int Seed;
    public NoiseParameters SurfaceNoise;
    public NoiseParameters StoneNoise;
    public NoiseParameters CaveNoise;

    public void Execute(int index)
    {
        int x = index % ChunkSize.x;
        int y = (index / ChunkSize.x) % ChunkSize.y;
        int z = index / (ChunkSize.x * ChunkSize.y);

        float3 worldPos = WorldPosition + new float3(x, y, z);
        
        float surfaceHeight = SampleNoise(worldPos.xz, SurfaceNoise);
        float stoneHeight = SampleNoise(worldPos.xz, StoneNoise);
        
        ushort blockId = 0;
        if (y + WorldPosition.y <= 4) blockId = 4; // Bedrock
        else if (y + WorldPosition.y < stoneHeight) blockId = 3; // Stone
        else if (y + WorldPosition.y < surfaceHeight) blockId = 1; // Dirt/Grass
        
        if (blockId != 0)
        {
            float caveValue = SampleNoise3D(worldPos, CaveNoise);
            if (caveValue > CaveNoise.probability) blockId = 0;
        }

        BlockIds[index] = blockId;
    }
  private float SampleNoise(float2 pos, NoiseParameters par)
    {
        // Usamos noise.snoise de Unity.Mathematics
        float2 noisePos = pos * par.scale + new float2(par.seed, par.seed);
        return par.heightOffset + noise.snoise(noisePos) * par.heightScale;
    }

    private float SampleNoise3D(float3 pos, NoiseParameters par)
    {
        float3 noisePos = pos * par.scale + new float3(par.seed, par.seed, par.seed);
        return noise.snoise(noisePos);
    }
}


