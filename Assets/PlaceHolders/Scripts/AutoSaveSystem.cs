using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Sistema de auto-guardado que guarda el mundo automáticamente cada X segundos
/// </summary>
public class AutoSaveSystem : MonoBehaviour
{
    [Header("Auto-Save Configuration")]
    [Tooltip("Intervalo de guardado en segundos")]
    [SerializeField] private float saveInterval = 2f;
    
    [Tooltip("Activar auto-guardado")]
    [SerializeField] private bool enableAutoSave = true;
    
    [Tooltip("Solo guardar si hay chunks modificados")]
    [SerializeField] private bool saveOnlyIfModified = true;
    
    [Tooltip("Mostrar notificación al guardar")]
    [SerializeField] private bool showSaveNotification = true;
    
    [Header("UI (Opcional)")]
    [Tooltip("Texto para mostrar última vez guardado")]
    [SerializeField] private Text saveStatusText;
    
    [Tooltip("Icono de guardado (opcional)")]
    [SerializeField] private GameObject saveIcon;
    
    [Tooltip("Duración del icono de guardado (segundos)")]
    [SerializeField] private float iconDuration = 1f;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;
    
    // Referencias privadas
    private World world;
    private float timeSinceLastSave = 0f;
    private int totalSaves = 0;
    private float lastSaveTime = 0f;
    private bool isSaving = false;
    
    private void Start()
    {
        // Buscar World
        world = FindObjectOfType<World>();
        
        if (world == null)
        {
            Debug.LogError("❌ AutoSaveSystem: No se encontró World en la escena!");
            enabled = false;
            return;
        }
        
        // Ocultar icono de guardado
        if (saveIcon != null)
            saveIcon.SetActive(false);
        
        // Iniciar auto-guardado
        if (enableAutoSave)
        {
            StartCoroutine(AutoSaveCoroutine());
            Debug.Log($"💾 AutoSaveSystem iniciado | Intervalo: {saveInterval}s");
        }
    }
    
    private void Update()
    {
        timeSinceLastSave += Time.deltaTime;
        
        // Actualizar UI si existe
        if (saveStatusText != null)
        {
            float timeSince = Time.time - lastSaveTime;
            
            if (lastSaveTime > 0)
            {
                if (timeSince < 60)
                    saveStatusText.text = $"Guardado hace {timeSince:F0}s";
                else
                    saveStatusText.text = $"Guardado hace {(timeSince / 60f):F1}m";
            }
            else
            {
                saveStatusText.text = "Sin guardar";
            }
        }
    }
    
    /// <summary>
    /// Corutina de auto-guardado
    /// </summary>
    private IEnumerator AutoSaveCoroutine()
    {
        // Esperar a que el mundo esté listo
        yield return new WaitUntil(() => world != null);
        yield return new WaitForSeconds(2f); // Esperar 2 segundos extra tras iniciar
        
        WaitForSeconds waitInterval = new WaitForSeconds(saveInterval);
        
        while (enableAutoSave)
        {
            yield return waitInterval;
            
            // Guardar automáticamente
            PerformAutoSave();
        }
    }
    
    /// <summary>
    /// Ejecuta el auto-guardado
    /// </summary>
    private void PerformAutoSave()
    {
        if (isSaving)
        {
            if (showDebugLogs)
                Debug.LogWarning("⏳ Guardado ya en progreso, saltando...");
            return;
        }
        
        // Verificar si hay chunks modificados
        if (saveOnlyIfModified)
        {
            int modifiedCount = 0;
            foreach (var _ in world.GetModifiedChunks())
            {
                modifiedCount++;
                break; // Solo necesitamos saber si hay al menos 1
            }
            
            if (modifiedCount == 0)
            {
                if (showDebugLogs)
                    Debug.Log("💾 Auto-save: Sin cambios, saltando guardado");
                return;
            }
        }
        
        // Ejecutar guardado
        StartCoroutine(SaveAsync());
    }
    
    /// <summary>
    /// Guarda de forma asíncrona para no bloquear el juego
    /// </summary>
    private IEnumerator SaveAsync()
    {
        isSaving = true;
        float saveStartTime = Time.realtimeSinceStartup;
        
        // Guardar
        world.SaveGame();
        
        // Esperar un frame
        yield return null;
        
        float saveTime = Time.realtimeSinceStartup - saveStartTime;
        timeSinceLastSave = 0f;
        lastSaveTime = Time.time;
        totalSaves++;
        
        // Log
        if (showDebugLogs)
        {
            int modifiedChunks = 0;
            foreach (var _ in world.GetModifiedChunks())
                modifiedChunks++;
            
            Debug.Log($"💾 Auto-save #{totalSaves} | Tiempo: {saveTime:F3}s | Chunks: {modifiedChunks}");
        }
        
        // Mostrar notificación
        if (showSaveNotification)
        {
            ShowSaveNotification();
        }
        
        isSaving = false;
    }
    
    /// <summary>
    /// Muestra notificación visual de guardado
    /// </summary>
    private void ShowSaveNotification()
    {
        if (saveIcon != null)
        {
            StartCoroutine(ShowSaveIconCoroutine());
        }
    }
    
    /// <summary>
    /// Muestra el icono de guardado temporalmente
    /// </summary>
    private IEnumerator ShowSaveIconCoroutine()
    {
        saveIcon.SetActive(true);
        yield return new WaitForSeconds(iconDuration);
        saveIcon.SetActive(false);
    }
    
    /// <summary>
    /// Fuerza un guardado manual inmediato
    /// </summary>
    public void ForceSave()
    {
        if (isSaving)
        {
            Debug.LogWarning("⏳ Ya hay un guardado en progreso");
            return;
        }
        
        Debug.Log("💾 Guardado manual forzado");
        StartCoroutine(SaveAsync());
    }
    
    /// <summary>
    /// Activa/desactiva el auto-guardado
    /// </summary>
    public void SetAutoSaveEnabled(bool enabled)
    {
        enableAutoSave = enabled;
        
        if (enabled)
        {
            StartCoroutine(AutoSaveCoroutine());
            Debug.Log("✅ Auto-save activado");
        }
        else
        {
            StopAllCoroutines();
            Debug.Log("❌ Auto-save desactivado");
        }
    }
    
    /// <summary>
    /// Cambia el intervalo de guardado
    /// </summary>
    public void SetSaveInterval(float newInterval)
    {
        saveInterval = Mathf.Max(1f, newInterval); // Mínimo 1 segundo
        Debug.Log($"💾 Intervalo de guardado cambiado a {saveInterval}s");
        
        // Reiniciar corutina con nuevo intervalo
        if (enableAutoSave)
        {
            StopAllCoroutines();
            StartCoroutine(AutoSaveCoroutine());
        }
    }
    
    /// <summary>
    /// Obtiene estadísticas de guardado
    /// </summary>
    public string GetStats()
    {
        return $"Guardados: {totalSaves} | Último: {lastSaveTime}s | Próximo en: {(saveInterval - timeSinceLastSave):F0}s";
    }
    
    #region --- Debug GUI ---
    
    #if UNITY_EDITOR
    private void OnGUI()
    {
        if (!Application.isPlaying || !showDebugLogs)
            return;
        
        // Panel pequeño en esquina superior derecha
        GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.normal.background = MakeTexture(2, 2, new Color(0f, 0f, 0f, 0.6f));
        
        GUILayout.BeginArea(new Rect(Screen.width - 310, 10, 300, 120), boxStyle);
        
        GUILayout.Label($"<b>💾 Auto-Save</b>", GetHeaderStyle());
        GUILayout.Space(3);
        
        GUILayout.Label($"Status: {(enableAutoSave ? "✅ Activo" : "❌ Desactivado")}");
        GUILayout.Label($"Intervalo: {saveInterval}s");
        GUILayout.Label($"Siguiente: {Mathf.Max(0, saveInterval - timeSinceLastSave):F1}s");
        GUILayout.Label($"Total guardados: {totalSaves}");
        
        if (GUILayout.Button(isSaving ? "⏳ Guardando..." : "💾 Forzar Guardado"))
        {
            if (!isSaving)
                ForceSave();
        }
        
        GUILayout.EndArea();
    }
    
    private GUIStyle GetHeaderStyle()
    {
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.richText = true;
        style.fontSize = 12;
        style.fontStyle = FontStyle.Bold;
        return style;
    }
    
    private Texture2D MakeTexture(int width, int height, Color col)
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
