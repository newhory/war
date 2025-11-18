using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;


namespace War.Dots.Component
{
    public struct PointInput : IComponentData
    {
        public float DragOffset;
    }

    public struct OnPressStart : IComponentData, IEnableableComponent
    {
    }

    public struct OnPressEnd : IComponentData, IEnableableComponent
    {
    }

    public struct OnDragStart : IComponentData
    {
        public float2 Point;
        public Ray Ray;
    }

    public struct OnDragging : IComponentData
    {
        public float2 Point;
        public Ray Ray;
    }

    public struct OnDragEnd : IComponentData
    {
        public float2 Point;
        public Ray Ray;
    }

    public struct DragStartPosition : IComponentData, IEnableableComponent
    {
        public float3 Position;
    }

    public struct SelectedTroop : IComponentData
    {
        public Entity TroopEntity;
    }

    public struct TargetCandidateTroop : IComponentData
    {
        public Entity TroopEntity;
    }

    public struct SelectedTargetTroop : IComponentData
    {
        public Entity TroopEntity;
    }
}