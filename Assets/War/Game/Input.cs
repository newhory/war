using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace War.Game
{
    public struct PointInput : IComponentData
    {
        public float DragOffset;
    }

    public struct OnPointerPressStart : IComponentData, IEnableableComponent
    {
    }

    public struct OnPointerPressEnd : IComponentData, IEnableableComponent
    {
    }

    public struct OnPointerMove : IComponentData
    {
        public float3 PositionOnGround;
    }

    public struct OnPointerDragStart : IComponentData
    {
        public float2 Point;
        public float3 PositionOnGround;
        public Ray Ray;
    }

    public struct OnPointerDragging : IComponentData
    {
        public float2 Point;
        public float3 PositionOnGround;
        public Ray Ray;
    }

    public struct OnPointerDragEnd : IComponentData
    {
        public float3 PositionOnGround;
    }

    public struct DragStartWorldPosition : IComponentData, IEnableableComponent
    {
        public float3 Position;
    }
    
    public struct DraggingWorldPosition : IComponentData, IEnableableComponent
    {
        public float3 Position;
    }
    
    public struct DragEndWorldPosition : IComponentData, IEnableableComponent
    {
        public float3 Position;
    }
}