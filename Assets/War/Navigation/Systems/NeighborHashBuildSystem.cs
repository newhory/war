using Unity.Collections;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.AI;


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

        [BurstCompile]
        private struct InjectNavMeshMask : IJobParallelFor
        {
            public NativeParallelMultiHashMap<int, NeighborRecord>.ParallelWriter ParallelWriter;

            [ReadOnly] public NativeArray<byte>.ReadOnly NavMeshMask;
            [ReadOnly] public float3 MinWorldPositionInGrid;
            [ReadOnly] public int2 GridSize;
            [ReadOnly] public float CellSize;


            public void Execute(int index)
            {
                if (NavMeshMask[index] == 0)
                {
                    return;
                }

                int2 cell = new(index % GridSize.x, index / GridSize.x);

                float3 center = new(
                    MinWorldPositionInGrid.x + cell.x * CellSize + CellSize * 0.5f,
                    0,
                    MinWorldPositionInGrid.z + cell.y * CellSize + CellSize * 0.5f
                );

                ParallelWriter.Add(
                    NeighborQuery.HashKey(center, CellSize),
                    new NeighborRecord
                    {
                        Entity = Entity.Null,
                        Position = center,
                        Velocity = float3.zero,
                        Radius = CellSize * 0.5f,
                        IsObstacle = 1
                    });
            }
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

            int2 gridSize = navigationGrid.NeighborHashGridSize;
            float cellSize = navigationGrid.NeighborHashCellSize;
            float3 minWorldPositionInGrid = navigationGrid.Min;

            if (!_navMeshMask.IsCreated)
            {
                _navMeshMask = new NativeArray<byte>(gridSize.x * gridSize.y, Allocator.Persistent);

                float2 currentWorldPositionInGrid = minWorldPositionInGrid.xz;

                for (int gridY = 0; gridY < gridSize.y; gridY++)
                {
                    currentWorldPositionInGrid.x = minWorldPositionInGrid.x;

                    for (int gridX = 0; gridX < gridSize.x; gridX++)
                    {
                        Vector3 center = new(currentWorldPositionInGrid.x + cellSize * 0.5f, 0, currentWorldPositionInGrid.y + cellSize * 0.5f);

                        if (!NavMesh.SamplePosition(center, out NavMeshHit _, cellSize, NavMesh.AllAreas))
                        {
                            int index = gridX + gridY * gridSize.x;

                            _navMeshMask[index] = 1;
                        }

                        currentWorldPositionInGrid.x += cellSize;
                    }

                    currentWorldPositionInGrid.y += cellSize;
                }
            }

            JobHandle dependency = state.Dependency;

            dependency = new ClearHashJob { Hash = _hash }.Schedule(dependency);

            dependency =
                new InsertJob
                    {
                        ParallelWriter = _hash.AsParallelWriter(),

                        CellSize = cellSize
                    }
                    .ScheduleParallel(dependency);

            dependency =
                new InjectNavMeshMask
                    {
                        ParallelWriter = _hash.AsParallelWriter(),

                        NavMeshMask = _navMeshMask.AsReadOnly(),
                        MinWorldPositionInGrid = minWorldPositionInGrid,
                        GridSize = gridSize,
                        CellSize = cellSize
                    }
                    .Schedule(_navMeshMask.Length, 64, dependency);

            state.Dependency = dependency;

            NeighborService.Hash = _hash.AsReadOnly();
            NeighborService.CellSize = cellSize;
            NeighborService.GridSize = gridSize;
            NeighborService.MaxNeighborCount = navigationGrid.MaxMaxNeighborCount;
        }
    }

    public static class NeighborService
    {
        public static NativeParallelMultiHashMap<int, NeighborRecord>.ReadOnly Hash;

        public static float CellSize;
        public static int2 GridSize;
        public static int MaxNeighborCount;
    }
}