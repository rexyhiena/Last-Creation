using UnityEngine;
using System.Collections.Generic;  // ← ESTA LÍNEA FALTABA

[CreateAssetMenu(fileName = "NewBlock", menuName = "Minecraft Clone/Block Definition")]
public class BlockDefinition : ScriptableObject
{
    [Header("Información Básica")]
    [Tooltip("Nombre visible del bloque")]
    public string blockName = "Nuevo Bloque";

    [Tooltip("ID único del bloque")]
    public ushort id = 0;

    [Header("Visual")]
    [Tooltip("¿Este bloque debe ser instanciado como un prefab en lugar de ser parte de la malla del chunk?")]
    public bool isEntity = false;

    [Tooltip("Usa modelo 3D custom en lugar de cubo voxel")]
    public bool useCustomModel = false;

    [Tooltip("Prefab del modelo custom (usado si isEntity es true)")]
    public GameObject customModel;

    [Header("Material")]
    [Tooltip("Material para bloques vóxel (cuando isEntity es false)")]
    public Material blockMaterial;

    [Header("Física y Gameplay")]
    [Tooltip("Salud del bloque (-1 = indestructible)")]
    public int health = 2;

    [Tooltip("¿Puede caer por gravedad?")]
    public bool canDrop = false;

    [Tooltip("¿Puede fluir como líquido?")]
    public bool canFlow = false;

    [Tooltip("¿Es transparente? (Para la lógica de visibilidad de caras)")]
    public bool isTransparent = false;

    [Tooltip("¿Bloquea luz?")]
    public bool blocksLight = true;

    [Header("Generación de Mundo")]
    [Tooltip("Tipo de bloque para generación procedural")]
    public BlockGenerationType generationType = BlockGenerationType.Normal;

    [Tooltip("¿Este bloque puede aparecer como mineral en piedra?")]
    public bool isMineralOre = false;

    [Header("Configuración de Mineral (si isMineralOre = true)")]
    [Tooltip("Altura mínima donde aparece este mineral")]
    public int minSpawnHeight = 0;

    [Tooltip("Altura máxima donde aparece este mineral")]
    public int maxSpawnHeight = 64;

    [Tooltip("Probabilidad de generación (0-1, ej: 0.01 = 1%)")]
    [Range(0f, 1f)]
    public float spawnProbability = 0.01f;

    [Tooltip("Tamaño de las vetas (bloques por veta)")]
    [Range(1, 10)]
    public int veinSize = 3;

    [Tooltip("¿Reemplaza solo piedra o también otros bloques?")]
    public bool onlyReplacesStone = true;

    [Header("Optimización - LOD")]
    [Tooltip("Color para LOD lejano")]
    public Color lodColor = Color.white;

    // owo bloques que no son bloques que son 3D
    private List<GameObject> entityObjects = new List<GameObject>();

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(blockName))
            blockName = name;

        // Ajustar la transparencia si es una entidad para que no oculte caras
        if (isEntity)
        {
            isTransparent = true;
        }

        if (!useCustomModel && !isEntity && blockMaterial == null && Application.isPlaying)
            Debug.LogWarning($"[BlockDefinition] {blockName} (ID {id}) es un bloque vóxel y no tiene material.");
    }

    public Material GetMaterial()
    {
        if (blockMaterial != null)
            return blockMaterial;

        var fallback = new Material(Shader.Find("Standard") ?? Shader.Find("Unlit/Color"));
        fallback.color = new Color(1f, 0f, 1f, 0.5f); // Rosa brillante para errores
        fallback.name = $"MISSING_MAT_{blockName}";
        return fallback;
    }

    public Texture2D GetMainTexture()
    {
        if (blockMaterial != null && blockMaterial.mainTexture is Texture2D tex)
            return tex;
        return null;
    }

    public bool HasCustomModel() => useCustomModel && customModel != null;
}

/// <summary>
/// Tipo de bloque para generación procedural
/// </summary>
public enum BlockGenerationType
{
    Normal,
    Bedrock,
    Stone,
    Dirt,
    Surface,
    Water,
    Mineral
}