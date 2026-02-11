using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Unity.Burst;

[BurstCompile]
public struct ChunkDataGenerationJob : IJobParallelFor
{
    public int3 chunkSize;
    public int3 chunkWorldPosition;
    public int currentSeed;
    public int seaLevel;

    // Noise parameters as NativeArrays for Burst compatibility
    [ReadOnly] public NoiseParametersNative surfaceNoise;
    [ReadOnly] public NoiseParametersNative stoneNoise;
    [ReadOnly] public NoiseParametersNative caveNoise;

    // Output NativeArray for block IDs and health levels
    public NativeArray<ushort> blockIds;
    public NativeArray<byte> healthLevels;

    // Placeholder for BlockRegistry data - ideally this would be a NativeHashMap or similar
    // For now, we'll use simplified logic within the job.
    // In a real scenario, you'd pass NativeArrays of block properties (isEntity, isTransparent, etc.)

    public void Execute(int index)
    {
        int x = index % chunkSize.x;
        int y = (index / chunkSize.x) % chunkSize.y;
        int z = index / (chunkSize.x * chunkSize.y);

        int worldX = chunkWorldPosition.x + x;
        int worldY = chunkWorldPosition.y + y;
        int worldZ = chunkWorldPosition.z + z;

        // Generate noise for surface and stone heights
        float surfaceHeight = NoiseGeneratorNative.GenerateHeight(worldX, worldZ, surfaceNoise);
        int surfaceY = (int)math.floor(surfaceHeight);

        float stoneHeight = NoiseGeneratorNative.GenerateHeight(worldX, worldZ, stoneNoise);
        int stoneY = (int)math.floor(stoneHeight);

        ushort chosenBlock = DetermineBlockFastNative(worldY, surfaceY, stoneY, seaLevel);

        // Apply cave noise
        if (chosenBlock != 29 && chosenBlock != 0) // Assuming 29 is a special block like water, 0 is air
        {
            float caveNoiseValue = NoiseGeneratorNative.Generate3D(worldX, worldY, worldZ, caveNoise);
            if (caveNoiseValue > caveNoise.probability)
                chosenBlock = 0; // Make it air if cave noise is high enough
        }

        blockIds[index] = chosenBlock;
        healthLevels[index] = 0; // Health levels can be determined here too if needed
    }

    private ushort DetermineBlockFastNative(int worldY, int surfaceY, int stoneY, int seaLevel)
    {
        // Simplified block determination for Burst compatibility
        // In a real scenario, block properties would be looked up from NativeArrays
        if (worldY <= 4) return 1; // Bedrock
        if (worldY < stoneY) return 2; // Stone
        if (worldY < surfaceY - 3) return 2; // Stone
        if (worldY < surfaceY) return 3; // Dirt
        if (worldY == surfaceY) return 4; // Surface block (e.g., Grass)
        return 0; // Air
    }
}

// Native version of NoiseParameters for Burst compatibility
public struct NoiseParametersNative
{
    public int seed;
    public float scale;
    public float heightScale;
    public int octaves;
    public float heightOffset;
    public float probability;
}

// Burst-compatible NoiseGenerator functions
[BurstCompile]
public static class NoiseGeneratorNative
{
    public static float GenerateHeight(int x, int z, NoiseParametersNative noiseParams)
    {
        // Placeholder for actual noise generation logic
        // This would typically involve Perlin noise or similar, implemented in a Burst-compatible way.
        // For demonstration, returning a simple value.
        return noiseParams.heightOffset + noiseParams.heightScale * math.sin(x * noiseParams.scale + z * noiseParams.scale);
    }

    public static float Generate3D(int x, int y, int z, NoiseParametersNative noiseParams)
    {
        // Placeholder for 3D noise generation
        return (float)math.abs(math.sin(x * noiseParams.scale + y * noiseParams.scale + z * noiseParams.scale));
    }
}
