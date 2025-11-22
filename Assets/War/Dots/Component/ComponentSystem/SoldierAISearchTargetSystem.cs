using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;


namespace War.Dots.Component.ComponentSystem
{
    [UpdateInGroup(typeof(Group.SoldierAISystemGroup))]
    [UpdateBefore(typeof(SoldierAISystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct SoldierAISearchTargetSystem : ISystem
    {
        [BurstCompile]
        private partial struct SearchTargetJob : IJobEntity
        {
            [ReadOnly] public NativeParallelMultiHashMap<int, Entity>.ReadOnly SpatialHashMap;
            [ReadOnly] public ComponentLookup<LocalTransform> SoldierPositionLookup;
            [ReadOnly] public ComponentLookup<Team> TeamLookup;
            [ReadOnly] public BufferLookup<Damaged> DamagedLookup;


            public void Execute(Entity entity, ref SoldierTargetForAttack targetForAttack, in LocalTransform localTransform, in Team team, in SearchTargetRange searchTargetRange)
            {
                float range = searchTargetRange.Value;

                int cellCount = (int)math.ceil(range / SoldierSpatialHashMapBuildSystem.DefaultCellSize);
                if (cellCount == 0)
                {
                    return;
                }

                float3 pos = localTransform.Position;
                int3 baseCell = SoldierSpatialHashMapBuildSystem.CellFromPos(pos);

                float minDistance = float.MaxValue;
                Entity target = targetForAttack.TargetSoldier;

                for (int dx = -cellCount; dx <= cellCount; dx++)
                {
                    for (int dz = -cellCount; dz <= cellCount; dz++)
                    {
                        int3 cell = new(baseCell.x + dx, 0, baseCell.z + dz);
                        int cellKey = SoldierSpatialHashMapBuildSystem.HashFromCell(cell);

                        if (SpatialHashMap.TryGetFirstValue(cellKey, out Entity otherEntity, out NativeParallelMultiHashMapIterator<int> iterator))
                        {
                            do
                            {
                                if (otherEntity == entity ||
                                    TeamLookup[otherEntity].Color == team.Color ||
                                    !DamagedLookup.HasBuffer(otherEntity))
                                {
                                    continue;
                                }

                                float3 otherPos = SoldierPositionLookup[otherEntity].Position;
                                float dist = math.distance(pos, otherPos);
                                if (dist < range && dist < minDistance)
                                {
                                    target = otherEntity;
                                    minDistance = dist;
                                }
                            } while (SpatialHashMap.TryGetNextValue(out otherEntity, ref iterator));
                        }
                    }
                }

                targetForAttack.TargetSoldier = target;
            }
        }


        private EntityQuery _searchTargetQuery;
        private ComponentLookup<LocalTransform> _soldierPositionLookup;
        private ComponentLookup<Team> _teamLookup;
        private BufferLookup<Damaged> _damagedLookup;


        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SoldierSpatialHashMap>();

            _searchTargetQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<Soldier, Alive, Team, SoldierAISearchTarget, SearchTargetRange, NavMeshAgentData, LocalTransform>()
                    .WithAllRW<SoldierTargetForAttack>()
                    .Build();

            _soldierPositionLookup = state.GetComponentLookup<LocalTransform>(true);
            _teamLookup = state.GetComponentLookup<Team>(true);
            _damagedLookup = state.GetBufferLookup<Damaged>(true);
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            SoldierSpatialHashMap soldierSpatialHashMap = SystemAPI.GetSingleton<SoldierSpatialHashMap>();
            if (!soldierSpatialHashMap.SpatialHashMap.IsCreated)
            {
                return;
            }

            _soldierPositionLookup.Update(ref state);
            _teamLookup.Update(ref state);
            _damagedLookup.Update(ref state);

            state.Dependency =
                new SearchTargetJob
                    {
                        SpatialHashMap = soldierSpatialHashMap.SpatialHashMap.AsReadOnly(),
                        SoldierPositionLookup = _soldierPositionLookup,
                        TeamLookup = _teamLookup,
                        DamagedLookup = _damagedLookup
                    }
                    .ScheduleParallel(_searchTargetQuery, state.Dependency);
        }
    }
}