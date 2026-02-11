using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/// <summary>
/// BlockRegistry mejorado con configuración de capas y minerales
/// </summary>
public class BlockRegistry : MonoBehaviour
{
    public static BlockRegistry Instance { get; private set; }

    [Header("Block Definitions")]
    [Tooltip("Lista de todas las definiciones de bloques")]
    public List<BlockDefinition> blockDefinitions = new List<BlockDefinition>();

    [Header("Layer Configuration - Bedrock")]
    [Tooltip("Bloques que pueden aparecer en la capa de bedrock (Y 0-4)")]
    public List<BlockDefinition> bedrockLayerBlocks = new List<BlockDefinition>();

    [Header("Layer Configuration - Stone")]
    [Tooltip("Bloques que pueden aparecer en la capa de piedra")]
    public List<BlockDefinition> stoneLayerBlocks = new List<BlockDefinition>();

    [Header("Layer Configuration - Dirt")]
    [Tooltip("Bloques que pueden aparecer en la capa de tierra")]
    public List<BlockDefinition> dirtLayerBlocks = new List<BlockDefinition>();

    [Header("Layer Configuration - Surface")]
    [Tooltip("Bloques que pueden aparecer en la superficie según altura")]
    public List<SurfaceBlockRule> surfaceBlockRules = new List<SurfaceBlockRule>();

    [Header("Mineral Configuration")]
    [Tooltip("Minerales que pueden aparecer en piedra (se auto-genera desde blockDefinitions)")]
    public List<BlockDefinition> mineralOres = new List<BlockDefinition>();

    // Cachés para performance
    private Dictionary<ushort, BlockDefinition> idToDefCache = new Dictionary<ushort, BlockDefinition>();
    private Dictionary<ushort, Material> idToMaterialCache = new Dictionary<ushort, Material>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeRegistry();
    }

    private void InitializeRegistry()
    {
        // Limpiar cachés
        idToDefCache.Clear();
        idToMaterialCache.Clear();

        // Poblar caché
        foreach (var def in blockDefinitions)
        {
            if (def == null)
            {
                Debug.LogWarning("BlockDefinition nula en lista!");
                continue;
            }

            if (idToDefCache.ContainsKey(def.id))
            {
                Debug.LogError($"ID duplicado: {def.id} ({def.blockName})");
                continue;
            }

            idToDefCache[def.id] = def;
            idToMaterialCache[def.id] = def.GetMaterial();

            Debug.Log($"✅ Registrado: {def.blockName} (ID: {def.id})");
        }

        // Auto-poblar lista de minerales
        RefreshMineralList();

        // Validar configuración de capas
        ValidateLayerConfiguration();

        Debug.Log($"🎨 BlockRegistry inicializado: {blockDefinitions.Count} bloques, {mineralOres.Count} minerales");
    }

    /// <summary>
    /// Refresca la lista de minerales desde blockDefinitions
    /// </summary>
    [ContextMenu("Refresh Mineral List")]
    public void RefreshMineralList()
    {
        mineralOres.Clear();
        
        foreach (var def in blockDefinitions)
        {
            if (def != null && def.isMineralOre)
            {
                mineralOres.Add(def);
                Debug.Log($"⛏️ Mineral registrado: {def.blockName} (Y {def.minSpawnHeight}-{def.maxSpawnHeight}, {def.spawnProbability * 100}%)");
            }
        }
    }

    private void ValidateLayerConfiguration()
    {
        if (bedrockLayerBlocks.Count == 0)
            Debug.LogWarning("⚠️ No hay bloques configurados para capa de Bedrock");

        if (stoneLayerBlocks.Count == 0)
            Debug.LogWarning("⚠️ No hay bloques configurados para capa de Piedra");

        if (dirtLayerBlocks.Count == 0)
            Debug.LogWarning("⚠️ No hay bloques configurados para capa de Tierra");

        if (surfaceBlockRules.Count == 0)
            Debug.LogWarning("⚠️ No hay reglas configuradas para bloques de Superficie");
    }

    #region --- Obtener Bloques por Capa ---

    /// <summary>
    /// Obtiene un bloque aleatorio para la capa de bedrock
    /// </summary>
    public ushort GetBedrockBlock(System.Random random)
    {
        if (bedrockLayerBlocks.Count == 0)
            return 29; // Fallback

        var block = bedrockLayerBlocks[random.Next(bedrockLayerBlocks.Count)];
        return block.id;
    }

    /// <summary>
    /// Obtiene un bloque para la capa de piedra (con posibilidad de mineral)
    /// </summary>
    public ushort GetStoneBlock(int worldX, int worldY, int worldZ, System.Random random)
    {
        // Intentar generar mineral primero
        foreach (var mineral in mineralOres)
        {
            // Verificar rango de altura
            if (worldY < mineral.minSpawnHeight || worldY > mineral.maxSpawnHeight)
                continue;

            // Verificar probabilidad
            float roll = (float)random.NextDouble();
            if (roll < mineral.spawnProbability)
            {
                // Generar veta de mineral
                return mineral.id;
            }
        }

        // Si no hay mineral, usar piedra normal
        if (stoneLayerBlocks.Count == 0)
            return 28; // Fallback

        var block = stoneLayerBlocks[random.Next(stoneLayerBlocks.Count)];
        return block.id;
    }

    /// <summary>
    /// Obtiene un bloque aleatorio para la capa de tierra
    /// </summary>
    public ushort GetDirtBlock(System.Random random)
    {
        if (dirtLayerBlocks.Count == 0)
            return 1; // Fallback

        var block = dirtLayerBlocks[random.Next(dirtLayerBlocks.Count)];
        return block.id;
    }

    /// <summary>
    /// Obtiene un bloque de superficie según la altura
    /// </summary>
    public ushort GetSurfaceBlock(int surfaceY, int seaLevel)
    {
        // Ordenar reglas por prioridad (menor = más prioritario)
        var sortedRules = surfaceBlockRules.OrderBy(r => r.priority).ToList();

        foreach (var rule in sortedRules)
        {
            if (rule.block == null)
                continue;

            // Verificar condiciones de altura
            bool meetsConditions = false;

            switch (rule.heightCondition)
            {
                case HeightCondition.BelowSeaLevel:
                    meetsConditions = surfaceY < seaLevel;
                    break;

                case HeightCondition.AtSeaLevel:
                    meetsConditions = Mathf.Abs(surfaceY - seaLevel) <= 2;
                    break;

                case HeightCondition.AboveSeaLevel:
                    meetsConditions = surfaceY > seaLevel + 2;
                    break;

                case HeightCondition.CustomRange:
                    meetsConditions = surfaceY >= rule.minHeight && surfaceY <= rule.maxHeight;
                    break;

                case HeightCondition.Always:
                    meetsConditions = true;
                    break;
            }

            if (meetsConditions)
                return rule.block.id;
        }

        // Fallback: césped
        return 2;
    }

    #endregion

    #region --- Getters Estándar ---

    public BlockDefinition GetBlockDefinition(ushort id)
    {
        if (idToDefCache.TryGetValue(id, out BlockDefinition def))
            return def;
        return null;
    }

    public Material GetMaterial(ushort id)
    {
        if (idToMaterialCache.TryGetValue(id, out Material mat))
            return mat;

        var def = GetBlockDefinition(id);
        if (def != null)
        {
            mat = def.GetMaterial();
            idToMaterialCache[id] = mat;
            return mat;
        }

        return null;
    }

    public string GetBlockName(ushort id)
    {
        var def = GetBlockDefinition(id);
        return def != null ? def.blockName : "Unknown";
    }

    public bool IsTransparent(ushort id)
    {
        var def = GetBlockDefinition(id);
        return def != null && def.isTransparent;
    }

    #endregion

    #region --- Editor Utilities ---

    [ContextMenu("Log All Blocks")]
    public void LogAllBlocks()
    {
        Debug.Log("=== BLOCK REGISTRY ===");
        foreach (var def in blockDefinitions)
        {
            if (def != null)
                Debug.Log($"ID {def.id}: {def.blockName} | Type: {def.generationType}");
        }
    }

    [ContextMenu("Validate Configuration")]
    public void ValidateConfiguration()
    {
        ValidateLayerConfiguration();
        RefreshMineralList();
    }

    #endregion
}

/// <summary>
/// Regla para determinar qué bloque usar en la superficie
/// </summary>
[System.Serializable]
public class SurfaceBlockRule
{
    [Tooltip("Bloque a usar")]
    public BlockDefinition block;

    [Tooltip("Condición de altura")]
    public HeightCondition heightCondition = HeightCondition.AboveSeaLevel;

    [Tooltip("Altura mínima (para CustomRange)")]
    public int minHeight = 0;

    [Tooltip("Altura máxima (para CustomRange)")]
    public int maxHeight = 100;

    [Tooltip("Prioridad (menor = más prioritario)")]
    public int priority = 0;
}

/// <summary>
/// Condiciones de altura para bloques de superficie
/// </summary>
public enum HeightCondition
{
    BelowSeaLevel,   // Y < seaLevel
    AtSeaLevel,      // Y ≈ seaLevel (±2)
    AboveSeaLevel,   // Y > seaLevel + 2
    CustomRange,     // minHeight ≤ Y ≤ maxHeight
    Always           // Siempre (usado como fallback)
}
