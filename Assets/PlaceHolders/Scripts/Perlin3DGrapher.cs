using UnityEngine;

/// <summary>
/// Perlin3DGrapher actualizado - USA NoiseGenerator en lugar de MeshUtils
/// Visualiza el ruido de Perlin en 3D para ajustar parámetros (cuevas, árboles, biomas)
/// </summary>
[ExecuteInEditMode]
public class Perlin3DGrapher : MonoBehaviour
{
    [Header("Visualization Settings")]
    public Vector3Int dimensions = new Vector3Int(10, 10, 10);
    
    [Header("Noise Parameters")]
    public int seed = 12345;
    public float heightScale = 2;
    [Range(0.0f, 1.0f)]
    public float scale = 0.5f;
    public int octaves = 1;
    public float heightOffset = 1;
    
    [Range(0.0f, 10.0f)]
    [Tooltip("Threshold para mostrar/ocultar cubos (usado para probability en caves/trees)")]
    public float DrawCutOff = 1;

    private GameObject[] cubes;

    void CreateCubes()
    {
        // Limpiar cubos existentes
        if (cubes != null)
        {
            foreach (GameObject cube in cubes)
            {
                if (cube != null)
                    DestroyImmediate(cube);
            }
        }

        int totalCubes = dimensions.x * dimensions.y * dimensions.z;
        cubes = new GameObject[totalCubes];
        int index = 0;

        for (int z = 0; z < dimensions.z; z++)
        {
            for (int y = 0; y < dimensions.y; y++)
            {
                for (int x = 0; x < dimensions.x; x++)
                {
                    GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cube.name = $"perlin_cube_{x}_{y}_{z}";
                    cube.transform.parent = this.transform;
                    cube.transform.localPosition = new Vector3(x, y, z);
                    cube.transform.localScale = Vector3.one * 0.9f;
                    
                    var renderer = cube.GetComponent<MeshRenderer>();
                    if (renderer != null)
                    {
                        Material mat = new Material(Shader.Find("Standard"));
                        mat.color = new Color(
                            (float)x / dimensions.x,
                            (float)y / dimensions.y,
                            (float)z / dimensions.z
                        );
                        renderer.material = mat;
                    }
                    
                    cubes[index] = cube;
                    index++;
                }
            }
        }
    }

    void Graph()
    {
        if (cubes == null || cubes.Length != dimensions.x * dimensions.y * dimensions.z)
        {
            CreateCubes();
        }

        if (cubes == null || cubes.Length == 0) return;

        NoiseParameters param = GetNoiseParameters();
        int index = 0;

        for (int z = 0; z < dimensions.z; z++)
        {
            for (int y = 0; y < dimensions.y; y++)
            {
                for (int x = 0; x < dimensions.x; x++)
                {
                    if (index >= cubes.Length) break;

                    // Usar NoiseGenerator en lugar de MeshUtils
                    float p3d = NoiseGenerator.Generate3D(x, y, z, param);

                    GameObject cube = cubes[index];
                    if (cube != null)
                    {
                        var renderer = cube.GetComponent<MeshRenderer>();
                        if (renderer != null)
                        {
                            if (p3d < DrawCutOff)
                            {
                                renderer.enabled = false;
                            }
                            else
                            {
                                renderer.enabled = true;
                                
                                float intensity = Mathf.InverseLerp(DrawCutOff, heightScale + heightOffset, p3d);
                                renderer.material.color = Color.Lerp(Color.blue, Color.red, intensity);
                            }
                        }
                    }

                    index++;
                }
            }
        }
    }

    void OnValidate()
    {
        Graph();
    }

    void OnEnable()
    {
        Graph();
    }

    void OnDisable()
    {
        if (cubes != null)
        {
            foreach (GameObject cube in cubes)
            {
                if (cube != null)
                    DestroyImmediate(cube);
            }
            cubes = null;
        }
    }

    public NoiseParameters GetNoiseParameters()
    {
        return new NoiseParameters
        {
            seed = seed,
            heightScale = heightScale,
            scale = scale,
            octaves = octaves,
            heightOffset = heightOffset,
            probability = DrawCutOff
        };
    }

    [ContextMenu("Rebuild Cubes")]
    public void RebuildCubes()
    {
        OnDisable();
        CreateCubes();
        Graph();
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(
            transform.position + new Vector3(dimensions.x, dimensions.y, dimensions.z) * 0.5f,
            new Vector3(dimensions.x, dimensions.y, dimensions.z)
        );

        UnityEditor.Handles.Label(
            transform.position + Vector3.up * (dimensions.y + 1),
            $"Perlin 3D\nSeed: {seed}\nCutoff: {DrawCutOff:F2}"
        );
    }
#endif
}
