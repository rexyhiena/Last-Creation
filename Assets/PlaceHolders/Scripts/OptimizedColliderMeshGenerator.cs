using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Unity.Burst;

[BurstCompile]
public struct FilterFacesJob : IJob
{
    [ReadOnly] public NativeArray<float3> inVertices;
    [ReadOnly] public NativeArray<int> inTriangles;
    [ReadOnly] public NativeArray<float3> inNormals;

    public NativeList<float3> outVertices;
    public NativeList<int> outTriangles;

    public void Execute()
    {
        NativeHashMap<int, int> vertMap = new NativeHashMap<int, int>(inVertices.Length / 2, Allocator.Temp);

        for (int i = 0; i < inTriangles.Length; i += 3)
        {
            // Calculate average normal for the triangle
            float3 avgNormal = (inNormals[inTriangles[i]] + inNormals[inTriangles[i + 1]] + inNormals[inTriangles[i + 2]]) / 3f;

            // Check if it's a top face (normal pointing mostly upwards)
            if (math.abs(avgNormal.y) > 0.1f) // Threshold can be adjusted
            {
                for (int j = 0; j < 3; j++)
                {
                    int oldIdx = inTriangles[i + j];
                    if (!vertMap.ContainsKey(oldIdx))
                    {
                        vertMap.Add(oldIdx, outVertices.Length);
                        outVertices.Add(inVertices[oldIdx]);
                    }
                    outTriangles.Add(vertMap[oldIdx]);
                }
            }
        }
        vertMap.Dispose();
    }
}

public static class OptimizedColliderMeshGenerator
{
    public static JobHandle ScheduleFilterFacesJob(
        NativeArray<float3> inVertices,
        NativeArray<int> inTriangles,
        NativeArray<float3> inNormals,
        NativeList<float3> outVertices,
        NativeList<int> outTriangles)
    {
        var job = new FilterFacesJob
        {
            inVertices = inVertices,
            inTriangles = inTriangles,
            inNormals = inNormals,
            outVertices = outVertices,
            outTriangles = outTriangles
        };

        return job.Schedule();
    }
}
