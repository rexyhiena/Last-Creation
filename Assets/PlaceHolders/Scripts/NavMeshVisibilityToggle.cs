using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using System.Reflection;

/// <summary>
/// INSTRUCCIONES:
/// 1. Crea una carpeta llamada "Editor" en Assets
/// 2. Coloca este archivo en esa carpeta Editor
/// 3. Usa el menú: Tools > Hide NavMesh Visualization
/// </summary>
public class NavMeshVisibilityToggle : EditorWindow
{
    [MenuItem("Tools/Hide NavMesh Visualization %h")] // Ctrl+H / Cmd+H
    static void HideNavMesh()
    {
        SetNavMeshVisibility(false);
    }
    
    [MenuItem("Tools/Show NavMesh Visualization %j")] // Ctrl+J / Cmd+J
    static void ShowNavMesh()
    {
        SetNavMeshVisibility(true);
    }
    
    static void SetNavMeshVisibility(bool visible)
    {
        // Método 1: Usar la API pública de SceneView
        foreach (SceneView sceneView in SceneView.sceneViews)
        {
            sceneView.drawGizmos = visible;
            sceneView.Repaint();
        }
        
        // Método 2: Acceder a las configuraciones internas de NavMesh (solo si el método 1 no funciona)
        try
        {
            var navMeshEditorType = typeof(Editor).Assembly.GetType("UnityEditor.AI.NavMeshEditorHelpers");
            if (navMeshEditorType != null)
            {
                var drawField = navMeshEditorType.GetField("s_NavMeshDisplayOptions",
                    BindingFlags.Static | BindingFlags.NonPublic);
                
                if (drawField != null)
                {
                    drawField.SetValue(null, visible ? -1 : 0);
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"No se pudo cambiar visualización de NavMesh: {e.Message}");
        }
        
        Debug.Log(visible ? "🔊 NavMesh VISIBLE" : "🔇 NavMesh OCULTO");
        
        // Refrescar todas las vistas
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
    }
}
#endif
