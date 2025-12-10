using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;


namespace War.Navigation
{
    public struct FlowFieldBlobReference : IComponentData
    {
        public BlobAssetReference<FlowFieldBlobRoot> BlobAssetReference;
    }
    
    public struct FlowFieldTarget
    {
        public int FlowId;
        public SimpleBounds AreaBounds;
        public BlobArray<float2> DirectionField;
        
        public float3 Position => AreaBounds.Center;
    }
    
    public struct FlowFieldBlobRoot
    {
        public BlobArray<FlowFieldTarget> FlowFieldTargets;
    }

    [BurstCompile]
    public struct SimpleBounds
    {
        public float3 Min;
        public float3 Max;

        public float3 Center => (Min + Max) * 0.5f;
        public float3 Size => Max - Min;


        public SimpleBounds(float3 min, float3 max)
        {
            Min = min;
            Max = max;
        }


        public bool Contains(float3 point) =>
            point.x >= Min.x && point.x <= Max.x &&
            point.y >= Min.y && point.y <= Max.y &&
            point.z >= Min.z && point.z <= Max.z;
        
        public bool Contains(float2 point) =>
            point.x >= Min.x && point.x <= Max.x &&
            point.y >= Min.z && point.y <= Max.z;

        public bool Contains(SimpleBounds other) =>
            Min.x <= other.Min.x && Max.x >= other.Max.x &&
            Min.y <= other.Min.y && Max.y >= other.Max.y &&
            Min.z <= other.Min.z && Max.z >= other.Max.z;

        public void Expand(float3 amount)
        {
            Min -= amount * 0.5f;
            Max += amount * 0.5f;
        }
    }
}