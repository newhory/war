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
    }

    public struct TroopTargetForAttack : IComponentData
    {
        public Entity TargetTroop;
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
        public float3 Position;
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