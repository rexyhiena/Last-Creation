using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sistema de Object Pooling para Chunks
/// Reutiliza chunks en lugar de crear y destruir constantemente
/// Reduce drásticamente garbage collection y mejora performance
/// </summary>
public class ChunkPool : MonoBehaviour
{
    [Header("Pool Settings")]
    [SerializeField] private GameObject chunkPrefab;
    [SerializeField] private int initialPoolSize = 50;
    [SerializeField] private int maxPoolSize = 200;
    [SerializeField] private Transform poolParent;

    private Queue<GameObject> availableChunks = new Queue<GameObject>();
    private HashSet<GameObject> activeChunks = new HashSet<GameObject>();
    private Dictionary<Vector3Int, GameObject> positionToChunk = new Dictionary<Vector3Int, GameObject>();

    private static ChunkPool instance;
    public static ChunkPool Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<ChunkPool>();
                if (instance == null)
                {
                    GameObject obj = new GameObject("ChunkPool");
                    instance = obj.AddComponent<ChunkPool>();
                }
            }
            return instance;
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;

        if (poolParent == null)
        {
            poolParent = transform;
        }
    }

    /// <summary>
    /// Inicializa el pool con chunks pre-creados
    /// </summary>
    public void Initialize(GameObject prefab, int poolSize = -1)
    {
        if (prefab != null)
            chunkPrefab = prefab;

        if (poolSize > 0)
            initialPoolSize = poolSize;

        // Pre-crear chunks
        for (int i = 0; i < initialPoolSize; i++)
        {
            CreateNewChunk();
        }

        Debug.Log($"ChunkPool initialized with {availableChunks.Count} chunks");
    }

    /// <summary>
    /// Obtiene un chunk del pool o crea uno nuevo si es necesario
    /// </summary>
    public GameObject GetChunk(Vector3Int position)
    {
        // Si ya existe un chunk en esa posición, devolverlo
        if (positionToChunk.TryGetValue(position, out GameObject existingChunk))
        {
            return existingChunk;
        }

        GameObject chunk;

        // Obtener del pool o crear nuevo
        if (availableChunks.Count > 0)
        {
            chunk = availableChunks.Dequeue();
        }
        else
        {
            if (activeChunks.Count >= maxPoolSize)
            {
                Debug.LogWarning($"ChunkPool: Max pool size reached ({maxPoolSize})");
                return null;
            }
            chunk = CreateNewChunk();
        }

        // Activar y configurar
        chunk.SetActive(true);
        chunk.name = $"Chunk_{position.x}_{position.y}_{position.z}";
        chunk.transform.position = position;

        activeChunks.Add(chunk);
        positionToChunk[position] = chunk;

        return chunk;
    }

    /// <summary>
    /// Devuelve un chunk al pool
    /// </summary>
    public void ReturnChunk(GameObject chunk)
    {
        if (chunk == null) return;

        // Buscar posición del chunk
        Vector3Int? chunkPosition = null;
        foreach (var kvp in positionToChunk)
        {
            if (kvp.Value == chunk)
            {
                chunkPosition = kvp.Key;
                break;
            }
        }

        if (chunkPosition.HasValue)
        {
            positionToChunk.Remove(chunkPosition.Value);
        }

        activeChunks.Remove(chunk);


        // Desactivar y agregar al pool
        chunk.SetActive(false);
        chunk.transform.position = poolParent.position;
        availableChunks.Enqueue(chunk);
    }

    /// <summary>
    /// Devuelve un chunk al pool por su posición
    /// </summary>
    public void ReturnChunkAtPosition(Vector3Int position)
    {
        if (positionToChunk.TryGetValue(position, out GameObject chunk))
        {
            ReturnChunk(chunk);
        }
    }

    /// <summary>
    /// Obtiene un chunk por su posición
    /// </summary>
    public GameObject GetChunkAtPosition(Vector3Int position)
    {
        positionToChunk.TryGetValue(position, out GameObject chunk);
        return chunk;
    }

    /// <summary>
    /// Verifica si existe un chunk en una posición
    /// </summary>
    public bool HasChunkAtPosition(Vector3Int position)
    {
        return positionToChunk.ContainsKey(position);
    }

    /// <summary>
    /// Crea un nuevo chunk y lo agrega al pool
    /// </summary>
    private GameObject CreateNewChunk()
    {
        GameObject chunk = Instantiate(chunkPrefab, poolParent);
        chunk.SetActive(false);
        availableChunks.Enqueue(chunk);
        return chunk;
    }

    /// <summary>
    /// Devuelve todos los chunks activos al pool
    /// </summary>
    public void ReturnAllChunks()
    {
        List<GameObject> toReturn = new List<GameObject>(activeChunks);
        foreach (GameObject chunk in toReturn)
        {
            ReturnChunk(chunk);
        }
    }

    /// <summary>
    /// Pre-calienta el pool creando más chunks
    /// </summary>
    public void WarmUp(int additionalChunks)
    {
        for (int i = 0; i < additionalChunks; i++)
        {
            if (availableChunks.Count + activeChunks.Count >= maxPoolSize)
                break;
            CreateNewChunk();
        }
    }

    /// <summary>
    /// Obtiene estadísticas del pool
    /// </summary>
    public PoolStats GetStats()
    {
        return new PoolStats
        {
            available = availableChunks.Count,
            active = activeChunks.Count,
            total = availableChunks.Count + activeChunks.Count,
            maxSize = maxPoolSize
        };
    }

    public struct PoolStats
    {
        public int available;
        public int active;
        public int total;
        public int maxSize;

        public override string ToString()
        {
            return $"Pool Stats - Active: {active}, Available: {available}, Total: {total}/{maxSize}";
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    #if UNITY_EDITOR
    private void OnGUI()
    {
        if (!Application.isPlaying) return;

        var stats = GetStats();
        GUI.Label(new Rect(10, 10, 300, 20), $"Chunks - Active: {stats.active} | Available: {stats.available}");
    }
    #endif
}
