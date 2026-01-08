using Unity.Entities;
using Unity.Mathematics;

namespace War.Game
{
    public struct FieldCamera : IComponentData
    {
        public float Padding;
        public float SmoothTime;
        public float MinDistance;
        public float MaxDistance;

        public float MinOrthographicSize;
        public float MaxOrthographicSize;

        public float3 OriginalCameraPosition;
        public quaternion OriginalCameraRotation;
    }

    public struct FieldCameraRuntimeData : IComponentData
    {
        public bool IsOrthographic;

        public float3 VelocityPos;
        public float VelocitySize;
    }

    public struct UseFieldCamera : IComponentData, IEnableableComponent
    {
    }

    public struct ResetFieldCamera : IComponentData, IEnableableComponent
    {
    }
}