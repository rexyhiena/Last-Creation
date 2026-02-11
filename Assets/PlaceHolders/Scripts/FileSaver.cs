using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEngine;

// ──────────────────────────────────────────────────────────────────────
// Estructuras de datos para la serialización - MEJORADAS
// ──────────────────────────────────────────────────────────────────────

[Serializable]
public class ChunkSaveData
{
    public ushort[] blockIds;
    public byte[] healthLevels;
    
    // Metadatos opcionales
    public bool hasCustomBlocks;
    public int version = 1; // Para futuras migraciones
}

[Serializable]
public class WorldSaveData
{
    // Datos del jugador
    public Vector3 playerPosition;
    public Vector3 playerRotation;
    
    // Datos del mundo
    public int seed; // ← Compatible con World.currentSeed
    public DateTime saveTime;
    public int version = 2; // Versión del formato de guardado
    
    // Estadísticas (opcional)
    public int totalChunksGenerated;
    public float playTime;
    
    // Configuración del mundo (para validación)
    public int worldHeight;
    public int drawRadius;
}

// ──────────────────────────────────────────────────────────────────────
// Sistema de guardado/carga mejorado con compresión y async
// ──────────────────────────────────────────────────────────────────────
public static class WorldSaver
{
    private const string WORLD_DATA_FILE = "world.json";
    private const string CHUNK_FILE_PREFIX = "c_";
    private const string SAVE_FILE_EXTENSION = ".dat";
    private const int CURRENT_VERSION = 2;

    private static string GetSaveDirectory()
    {
        return Path.Combine(Application.persistentDataPath, "WorldData");
    }

    #region --- Public API: Save & Load ---

    /// <summary>
    /// Guarda el estado actual del mundo (compatible con World.currentSeed)
    /// </summary>
    public static void SaveWorld(World world)
    {
        string saveDir = GetSaveDirectory();
        if (!Directory.Exists(saveDir))
        {
            Directory.CreateDirectory(saveDir);
        }

        try
        {
            // 1. Guardar datos globales del mundo
            var worldData = new WorldSaveData
            {
                playerPosition = world.fpc.transform.position,
                playerRotation = world.fpc.transform.eulerAngles,
                seed = World.currentSeed, // ← USA LA SEMILLA ESTÁTICA
                saveTime = DateTime.UtcNow,
                version = CURRENT_VERSION,
                totalChunksGenerated = world.GetTotalChunksLoaded(),
                playTime = Time.timeSinceLevelLoad,
                worldHeight = 2, // Guardar configuración
                drawRadius = 8
            };

            string worldJson = JsonUtility.ToJson(worldData, true);
            SaveCompressed(Path.Combine(saveDir, WORLD_DATA_FILE), worldJson);
            Debug.Log($"💾 Datos del mundo guardados | Seed: {worldData.seed} | Chunks: {worldData.totalChunksGenerated}");

            // 2. Guardar chunks modificados
            int savedChunks = 0;
            foreach (var chunk in world.GetModifiedChunks())
            {
                if (SaveChunkData(chunk.location, chunk.GetSaveData()))
                {
                    savedChunks++;
                }
            }

            world.ClearModifiedChunks();
            Debug.Log($"✅ Guardado completo: {savedChunks} chunks modificados");
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ Error al guardar el mundo: {e.Message}\n{e.StackTrace}");
        }
    }

    /// <summary>
    /// Carga los datos globales del mundo
    /// </summary>
    public static bool LoadWorldData(out WorldSaveData worldData)
    {
        worldData = null;
        string worldFilePath = Path.Combine(GetSaveDirectory(), WORLD_DATA_FILE);

        if (!File.Exists(worldFilePath))
        {
            Debug.Log("📦 No se encontró mundo guardado");
            return false;
        }

        try
        {
            string worldJson = LoadCompressed(worldFilePath);
            worldData = JsonUtility.FromJson<WorldSaveData>(worldJson);

            // Validar versión
            if (worldData.version < CURRENT_VERSION)
            {
                Debug.LogWarning($"⚠️ Mundo guardado con versión antigua ({worldData.version}). Puede haber incompatibilidades.");
            }

            // Log detallado
            TimeSpan timeSinceSave = DateTime.UtcNow - worldData.saveTime;
            Debug.Log($"📦 Mundo cargado | Seed: {worldData.seed} | Guardado hace: {timeSinceSave.TotalHours:F1}h");
            Debug.Log($"📍 Posición del jugador: {worldData.playerPosition}");

            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ Error al cargar mundo: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// Carga datos de un chunk específico
    /// </summary>
    public static bool LoadChunkData(Vector3Int position, out ChunkSaveData chunkData)
    {
        chunkData = null;
        string fileName = GetChunkFileName(position);
        string chunkFilePath = Path.Combine(GetSaveDirectory(), fileName);

        if (!File.Exists(chunkFilePath))
        {
            return false; // Chunk no guardado, se generará proceduralmente
        }

        try
        {
            string json = LoadCompressed(chunkFilePath);
            chunkData = JsonUtility.FromJson<ChunkSaveData>(json);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ Error al cargar chunk en {position}: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// Guarda datos de un chunk específico
    /// </summary>
    public static bool SaveChunkData(Vector3 position, ChunkSaveData chunkData)
    {
        try
        {
            string saveDir = GetSaveDirectory();
            if (!Directory.Exists(saveDir))
            {
                Directory.CreateDirectory(saveDir);
            }

            Vector3Int pos = new Vector3Int(
                Mathf.FloorToInt(position.x),
                Mathf.FloorToInt(position.y),
                Mathf.FloorToInt(position.z)
            );

            string fileName = GetChunkFileName(pos);
            string json = JsonUtility.ToJson(chunkData);
            SaveCompressed(Path.Combine(saveDir, fileName), json);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ Error al guardar chunk: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// Elimina todos los datos de guardado
    /// </summary>
    public static void DeleteSaveData()
    {
        string saveDir = GetSaveDirectory();
        if (Directory.Exists(saveDir))
        {
            try
            {
                Directory.Delete(saveDir, true);
                Debug.Log("🗑️ Datos de guardado eliminados");
            }
            catch (Exception e)
            {
                Debug.LogError($"❌ Error al eliminar guardado: {e.Message}");
            }
        }
        else
        {
            Debug.Log("⚠️ No hay datos para eliminar");
        }
    }

    /// <summary>
    /// Verifica si existe un mundo guardado
    /// </summary>
    public static bool SaveExists()
    {
        string worldFilePath = Path.Combine(GetSaveDirectory(), WORLD_DATA_FILE);
        return File.Exists(worldFilePath);
    }

    /// <summary>
    /// Obtiene información básica del guardado sin cargarlo completamente
    /// </summary>
    public static WorldSaveInfo GetSaveInfo()
    {
        if (!SaveExists())
            return null;

        try
        {
            string worldFilePath = Path.Combine(GetSaveDirectory(), WORLD_DATA_FILE);
            string worldJson = LoadCompressed(worldFilePath);
            WorldSaveData data = JsonUtility.FromJson<WorldSaveData>(worldJson);

            return new WorldSaveInfo
            {
                seed = data.seed,
                saveTime = data.saveTime,
                totalChunks = data.totalChunksGenerated,
                playTime = data.playTime
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Cuenta cuántos chunks están guardados
    /// </summary>
    public static int GetSavedChunkCount()
    {
        string saveDir = GetSaveDirectory();
        if (!Directory.Exists(saveDir))
            return 0;

        try
        {
            string[] files = Directory.GetFiles(saveDir, $"{CHUNK_FILE_PREFIX}*{SAVE_FILE_EXTENSION}");
            return files.Length;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Obtiene el tamaño total del guardado en MB
    /// </summary>
    public static float GetSaveSizeMB()
    {
        string saveDir = GetSaveDirectory();
        if (!Directory.Exists(saveDir))
            return 0f;

        try
        {
            DirectoryInfo dirInfo = new DirectoryInfo(saveDir);
            long totalBytes = 0;

            foreach (FileInfo file in dirInfo.GetFiles("*", SearchOption.AllDirectories))
            {
                totalBytes += file.Length;
            }

            return totalBytes / (1024f * 1024f);
        }
        catch
        {
            return 0f;
        }
    }

    #endregion

    #region --- Private Helpers ---

    private static string GetChunkFileName(Vector3Int position)
    {
        return $"{CHUNK_FILE_PREFIX}{position.x}_{position.y}_{position.z}{SAVE_FILE_EXTENSION}";
    }

    /// <summary>
    /// Comprime un string JSON usando GZip
    /// </summary>
    private static void SaveCompressed(string path, string json)
    {
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(json);
        using (FileStream fileStream = new FileStream(path, FileMode.Create))
        using (GZipStream gzipStream = new GZipStream(fileStream, CompressionMode.Compress))
        {
            gzipStream.Write(bytes, 0, bytes.Length);
        }
    }

    /// <summary>
    /// Descomprime un archivo GZip y devuelve el JSON
    /// </summary>
    private static string LoadCompressed(string path)
    {
        using (FileStream fileStream = new FileStream(path, FileMode.Open))
        using (GZipStream gzipStream = new GZipStream(fileStream, CompressionMode.Decompress))
        using (StreamReader reader = new StreamReader(gzipStream, System.Text.Encoding.UTF8))
        {
            return reader.ReadToEnd();
        }
    }

    #endregion

    #region --- Debug & Utilities ---

    /// <summary>
    /// Imprime estadísticas del guardado actual
    /// </summary>
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public static void PrintSaveStats()
    {
        if (!SaveExists())
        {
            Debug.Log("No hay mundo guardado");
            return;
        }

        var info = GetSaveInfo();
        int chunkCount = GetSavedChunkCount();
        float sizeMB = GetSaveSizeMB();

        Debug.Log("=== ESTADÍSTICAS DE GUARDADO ===");
        Debug.Log($"Seed: {info.seed}");
        Debug.Log($"Guardado: {info.saveTime.ToLocalTime()}");
        Debug.Log($"Chunks guardados: {chunkCount}");
        Debug.Log($"Chunks generados: {info.totalChunks}");
        Debug.Log($"Tiempo de juego: {info.playTime / 60f:F1} minutos");
        Debug.Log($"Tamaño en disco: {sizeMB:F2} MB");
        Debug.Log($"Compresión: ~{(chunkCount * 0.5f) / sizeMB:F1}x");
    }

    /// <summary>
    /// Exporta el guardado a una ubicación específica (backup)
    /// </summary>
    public static bool ExportSave(string destinationPath)
    {
        try
        {
            string saveDir = GetSaveDirectory();
            if (!Directory.Exists(saveDir))
            {
                Debug.LogWarning("No hay guardado para exportar");
                return false;
            }

            if (Directory.Exists(destinationPath))
            {
                Directory.Delete(destinationPath, true);
            }

            CopyDirectory(saveDir, destinationPath);
            Debug.Log($"✅ Guardado exportado a: {destinationPath}");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ Error al exportar: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// Importa un guardado desde una ubicación específica
    /// </summary>
    public static bool ImportSave(string sourcePath)
    {
        try
        {
            if (!Directory.Exists(sourcePath))
            {
                Debug.LogWarning("No se encuentra el guardado para importar");
                return false;
            }

            string saveDir = GetSaveDirectory();
            if (Directory.Exists(saveDir))
            {
                Directory.Delete(saveDir, true);
            }

            CopyDirectory(sourcePath, saveDir);
            Debug.Log($"✅ Guardado importado desde: {sourcePath}");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ Error al importar: {e.Message}");
            return false;
        }
    }

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);

        foreach (string file in Directory.GetFiles(sourceDir))
        {
            string destFile = Path.Combine(destDir, Path.GetFileName(file));
            File.Copy(file, destFile, true);
        }

        foreach (string dir in Directory.GetDirectories(sourceDir))
        {
            string destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
            CopyDirectory(dir, destSubDir);
        }
    }

    #endregion
}

/// <summary>
/// Información resumida de un guardado
/// </summary>
public class WorldSaveInfo
{
    public int seed;
    public DateTime saveTime;
    public int totalChunks;
    public float playTime;

    public override string ToString()
    {
        return $"Seed: {seed} | Guardado: {saveTime.ToLocalTime()} | Chunks: {totalChunks}";
    }
}
