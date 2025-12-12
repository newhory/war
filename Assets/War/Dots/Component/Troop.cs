using Unity.Entities;
using Unity.Mathematics;


namespace War.Dots.Component
{
    public struct Troop : ISharedComponentData
    {
        public int Id;
    }

    public struct TroopEntity : IComponentData
    {
        public int Id;
        public Entity Entity;
        public int HorizontalUnitCount;
    }

    public struct TroopTargetForAttack : IComponentData
    {
        public Entity TargetTroop;
        public Entity OldTargetTroop;

        public bool IsTargetChanged;
    }

    public struct TroopAISearchTarget : IComponentData, IEnableableComponent
    {
    }

    public struct TroopAICheckTargetValid : IComponentData, IEnableableComponent
    {
    }
    
    public struct TroopStateMoveToDestination : IComponentData, IEnableableComponent
    {
    }

    public struct TroopStateMoveToTarget : IComponentData, IEnableableComponent
    {
    }

    public struct TroopSelected : IComponentData, IEnableableComponent
    {
    }

    public struct TroopSoldier : IBufferElementData
    {
        public Entity Entity;
        public float Radius;
        public float3 Position;
        public int IndexInFormation;
        public float3 PositionInFormation;
    }

    public struct TroopFormationReset : IComponentData, IEnableableComponent
    {
        public float3 TroopPosition;
    }

    public struct TroopHullPoint : IBufferElementData
    {
        public float2 Position;
    }

    public struct TroopAABB : IComponentData
    {
        public float2 Center;

        public float2 Min;
        public float2 Max;

        public float Padding;
    }

    public struct TroopSoldierIndexBuffer : IBufferElementData
    {
        public int Index;
    }

    public struct TroopLowerSoldierIndexBuffer : IBufferElementData
    {
        public int Index;
    }

    public struct TroopUpperSoldierIndexBuffer : IBufferElementData
    {
        public int Index;
    }
}