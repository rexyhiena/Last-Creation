using UnityEngine;

/// <summary>
/// PerlinGrapher actualizado - USA NoiseGenerator en lugar de MeshUtils
/// Visualiza el ruido de Perlin en 2D para ajustar parámetros
/// </summary>
[ExecuteInEditMode]
public class PerlinGrapher : MonoBehaviour
{
    public LineRenderer lr;
    
    [Header("Noise Parameters")]
    public int seed = 12345;
    public float heightScale = 2;
    [Range(0.0f, 1.0f)]
    public float scale = 0.5f;
    public int octaves = 1;
    public float heightOffset = 1;
    [Range(0.0f, 1.0f)]
    public float probability = 1;
    
    [Header("Visualization")]
    [Tooltip("Línea Z donde se visualiza el perlin")]
    public int visualizationZ = 11;
    
    void Start()
    {
        lr = this.GetComponent<LineRenderer>();
        if (lr == null)
        {
            lr = gameObject.AddComponent<LineRenderer>();
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = Color.green;
            lr.endColor = Color.green;
            lr.startWidth = 0.1f;
            lr.endWidth = 0.1f;
        }
        lr.positionCount = 100;
        Graph();
    }

    void Graph()
    {
        if (lr == null)
        {
            lr = this.GetComponent<LineRenderer>();
            if (lr == null) return;
        }
        
        lr.positionCount = 100;
        Vector3[] positions = new Vector3[lr.positionCount];
        
        NoiseParameters param = GetNoiseParameters();
        
        for (int x = 0; x < lr.positionCount; x++)
        {
            // Usar NoiseGenerator en lugar de MeshUtils
            float y = NoiseGenerator.GenerateHeight(x, visualizationZ, param);
            positions[x] = new Vector3(x, y, visualizationZ);
        }
        lr.SetPositions(positions);
    }

    void OnValidate()
    {
        Graph();
    }

    void Update()
    {
        if (Application.isPlaying)
        {
            Graph();
        }
    }

    /// <summary>
    /// Obtiene los parámetros como NoiseParameters para usar en World
    /// </summary>
    public NoiseParameters GetNoiseParameters()
    {
        return new NoiseParameters
        {
            seed = seed,
            heightScale = heightScale,
            scale = scale,
            octaves = octaves,
            heightOffset = heightOffset,
            probability = probability
        };
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(
            new Vector3(0, heightOffset, visualizationZ),
            new Vector3(100, heightOffset, visualizationZ)
        );
        
        Gizmos.color = Color.red;
        Gizmos.DrawLine(
            new Vector3(0, heightOffset + heightScale, visualizationZ),
            new Vector3(100, heightOffset + heightScale, visualizationZ)
        );
    }
#endif
}
