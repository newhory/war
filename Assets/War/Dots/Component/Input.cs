using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;


namespace War.Dots.Component
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
        public float2 Point;
        public Ray Ray;
    }

    public struct OnPointerDragStart : IComponentData
    {
        public float2 Point;
        public Ray Ray;
    }

    public struct OnPointerDragging : IComponentData
    {
        public float2 Point;
        public Ray Ray;
    }

    public struct OnPointerDragEnd : IComponentData
    {
        public float2 Point;
        public Ray Ray;
    }

    public struct DragStartPosition : IComponentData, IEnableableComponent
    {
        public float3 Position;
    }
}