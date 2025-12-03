using Unity.Entities;
using Unity.Mathematics;


namespace War.Navigation
{
    public struct NavigationGrid : IComponentData
    {
        public float NeighborHashCellSize;
        public int2 NeighborHashGridSize;
        public int MaxMaxNeighborCount;
        
        public float FlowFieldCellSize;
        public int2 FlowFieldGridSize;

        public float3 Center;
        public float3 Extents;

        public float3 Min => Center - Extents;
        public float3 Max => Center + Extents;

        public float3 Size => Extents * 2f;
    }

    public struct UpdateNavigationGrid : IComponentData
    {
    }
    
    // 목적지 엔티티에 부착: 동일한 목적지 그룹(flowId)을 가진 유닛들은 같은 필드를 공유
    public struct FlowFieldTarget : IComponentData
    {
        public int FlowId;
        public float3 Position;
        public float InfluenceRadius; // 선택: 목표 도착 판정에 사용
    }

    public struct UnitPosition : IComponentData
    {
        public float3 Value;
    }

    public struct UnitVelocity : IComponentData
    {
        public float3 Value;
    }

    public struct UnitDestination : IComponentData
    {
        public float3 Value;
        public int FlowFieldId;
    }

    public struct UnitRadius : IComponentData
    {
        public float Value; // 0.5~0.7
    }

    public struct UnitMaxSpeed : IComponentData
    {
        public float Value; // 1~2
    }

    public struct StandingObstacle : IComponentData
    {
        public byte Movable;
        public byte Enabled; // 1 if stationary obstacle
    }

    public struct UnitPreferredSide : IComponentData
    {
        public int Value; // -1,0,1
    }

    public struct BlockAhead : IComponentData
    {
        public float Severity; // 0..1
    }
    
    public struct StandingCooldown : IComponentData
    {
        public float BelowThresholdTime;
    }
}