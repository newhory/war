using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace War.Game.Systems
{
    [UpdateInGroup(typeof(Group.FirstUpdateGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct SoldierSpatialHashMapBuildSystem : ISystem
    {
        [BurstCompile]
        private partial struct MakeSpatialHashMapJob : IJobEntity
        {
            public NativeParallelMultiHashMap<int, Entity>.ParallelWriter SpatialHashMap;


            private void Execute(Entity entity, in LocalTransform localTransform) => SpatialHashMap.Add(HashCell(localTransform.Position, DefaultCellSize), entity);
        }


        public const float DefaultCellSize = 3.0f;


        public static int3 CellFromPos(float3 pos) => CellFromPos(pos, DefaultCellSize);
        public static int HashFromCell(int3 cell) => (cell.x * 73856093) ^ (cell.y * 19349663) ^ (cell.z * 83492791);

        private static int3 CellFromPos(float3 pos, float cellSize) => new((int)math.floor(pos.x / cellSize), 0, (int)math.floor(pos.z / cellSize));
        private static int HashCell(float3 pos, float cellSize) => HashFromCell(CellFromPos(pos, cellSize));


        private EntityQuery _soldierAgentQuery;


        public void OnCreate(ref SystemState state)
        {
            _soldierAgentQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<Soldier, Alive, NavMeshAgentData, LocalTransform>()
                    .Build();

            state.EntityManager.CreateSingleton<SoldierSpatialHashMap>();
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            int soldierCount = _soldierAgentQuery.CalculateEntityCount();

            RefRW<SoldierSpatialHashMap> soldierSpatialHashMap = SystemAPI.GetSingletonRW<SoldierSpatialHashMap>();
            if (!soldierSpatialHashMap.ValueRO.SpatialHashMap.IsCreated)
            {
                soldierSpatialHashMap.ValueRW.SpatialHashMap = new NativeParallelMultiHashMap<int, Entity>(soldierCount, Allocator.Domain);
            }
            else
            {
                if (soldierSpatialHashMap.ValueRW.SpatialHashMap.Capacity < soldierCount)
                {
                    soldierSpatialHashMap.ValueRW.SpatialHashMap.Capacity = math.max(soldierCount, soldierSpatialHashMap.ValueRO.SpatialHashMap.Capacity * 2);
                }

                soldierSpatialHashMap.ValueRW.SpatialHashMap.Clear();
            }

            state.Dependency = new MakeSpatialHashMapJob { SpatialHashMap = soldierSpatialHashMap.ValueRW.SpatialHashMap.AsParallelWriter() }.ScheduleParallel(_soldierAgentQuery, state.Dependency);
        }
    }
}