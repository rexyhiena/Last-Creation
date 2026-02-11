using Unity.Entities;
using Unity.Mathematics;

[System.Serializable]

public struct NoiseParameters : Unity.Entities.IComponentData
{
    public float heightScale;
    public float scale;
    public int   octaves;
    public float heightOffset;
    public float probability;
    public int seed;
}