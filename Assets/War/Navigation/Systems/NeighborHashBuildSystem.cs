using Unity.Collections;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;


namespace War.Navigation
{
    public struct NeighborRecord
    {
        public Entity Entity;
        public float3 Position;
        public float Radius;
        public byte IsObstacle;
        public float3 Velocity;
    }
}

namespace War.Navigation.Systems
{
    [UpdateInGroup(typeof(Group.NavigationSystemGroup))]
    [UpdateAfter(typeof(StandingToggleSystem))]
    [RequireMatchingQueriesForUpdate]
    [BurstCompile]
    public partial struct NeighborHashBuildSystem : ISystem
    {
        [BurstCompile]
        private struct ClearHashJob : IJob
        {
            public NativeParallelMultiHashMap<int, NeighborRecord> Hash;


            public void Execute() => Hash.Clear();
        }

        [BurstCompile]
        private partial struct InsertJob : IJobEntity
        {
            public NativeParallelMultiHashMap<int, NeighborRecord>.ParallelWriter ParallelWriter;

            [ReadOnly] public float CellSize;


            private void Execute(Entity entity, in UnitPosition unitPosition, in UnitRadius unitRadius, in UnitVelocity unitVelocity, in StandingObstacle standingObstacle) =>
                ParallelWriter.Add(
                    NeighborQuery.HashKey(unitPosition.Value, CellSize),
                    new NeighborRecord
                    {
                        Entity = entity,
                        Position = unitPosition.Value,
                        Radius = unitRadius.Value,
                        IsObstacle = standingObstacle.Enabled,
                        Velocity = unitVelocity.Value
                    });
        }


        private NativeParallelMultiHashMap<int, NeighborRecord> _hash;
        private NativeArray<byte> _navMeshMask;


        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NavigationGrid>();

            const int capacity = 1 << 20;

            _hash = new NativeParallelMultiHashMap<int, NeighborRecord>(capacity, Allocator.Persistent);
        }

        public void OnDestroy(ref SystemState state)
        {
            if (_hash.IsCreated)
            {
                _hash.Dispose();
            }

            if (_navMeshMask.IsCreated)
            {
                _navMeshMask.Dispose();
            }
        }

        public void OnUpdate(ref SystemState state)
        {
            NavigationGrid navigationGrid = SystemAPI.GetSingleton<NavigationGrid>();
            
            float cellSize = navigationGrid.NeighborHashCellSize;

            JobHandle dependency = state.Dependency;

            dependency = new ClearHashJob { Hash = _hash }.Schedule(dependency);

            dependency =
                new InsertJob
                    {
                        ParallelWriter = _hash.AsParallelWriter(),

                        CellSize = cellSize
                    }
                    .ScheduleParallel(dependency);

            state.Dependency = dependency;

            NeighborService.Hash = _hash.AsReadOnly();
            NeighborService.CellSize = cellSize;
            NeighborService.MaxNeighborCount = navigationGrid.MaxMaxNeighborCount;
        }
    }

    public static class NeighborService
    {
        public static NativeParallelMultiHashMap<int, NeighborRecord>.ReadOnly Hash;

        public static float CellSize;
        public static int MaxNeighborCount;
    }
}