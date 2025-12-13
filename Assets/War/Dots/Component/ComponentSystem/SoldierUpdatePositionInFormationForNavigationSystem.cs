using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;


namespace War.Dots.Component.ComponentSystem
{
    using Navigation;


    [UpdateInGroup(typeof(Group.SoldierInitializeSystemGroup))]
    [UpdateBefore(typeof(SoldierInitialPositionSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct SoldierUpdatePositionInFormationForNavigationSystem : ISystem
    {
        [BurstCompile]
        private partial struct UpdatePositionInFormationJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter EntityCommandBuffer;

            [ReadOnly] public BufferLookup<TroopSoldier> TroopSoldierLookup;
            [ReadOnly] public int2 NavMeshGridSize;
            [ReadOnly] public float NavMeshCellSize;
            [ReadOnly] public float3 NavMeshMinWorldPosition;
            [ReadOnly] public NativeArray<byte>.ReadOnly NavMeshMask;


            private void Execute([EntityIndexInQuery] int index, Entity soldierEntity, ref SoldierAttachedTroop soldierAttachedTroop)
            {
                EntityCommandBuffer.SetComponentEnabled<SoldierUpdatePositionInFormation>(index, soldierEntity, false);

                if (!TroopSoldierLookup.TryGetBuffer(soldierAttachedTroop.TroopEntity, out DynamicBuffer<TroopSoldier> troopSoldiers))
                {
                    return;
                }

                for (int i = 0, count = troopSoldiers.Length; i < count; ++i)
                {
                    TroopSoldier troopSoldier = troopSoldiers[i];
                    if (troopSoldier.Entity == soldierEntity)
                    {
                        float3 positionInFormation = troopSoldier.PositionInFormation;
                        if (!FlowFieldQuery.IsWalkable(positionInFormation, NavMeshMask, NavMeshGridSize, NavMeshCellSize, NavMeshMinWorldPosition))
                        {
                            if (FlowFieldQuery.TryFindNearestWalkableWorldPosition(positionInFormation, NavMeshMask, NavMeshGridSize, NavMeshCellSize, NavMeshMinWorldPosition, out float3 walkablePosition))
                            {
                                positionInFormation = walkablePosition;
                            }
                        }

                        soldierAttachedTroop.PositionInFormation = positionInFormation;

                        break;
                    }
                }
            }
        }


        private EntityQuery _soldierQuery;
        private BufferLookup<TroopSoldier> _troopSoldierLookup;


        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _soldierQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<Soldier, Alive, SoldierAttachedTroop>()
                    .WithAll<SoldierUpdatePositionInFormation>()
                    .WithAll<UnitPosition>()
                    .Build();

            _troopSoldierLookup = state.GetBufferLookup<TroopSoldier>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            _troopSoldierLookup.Update(ref state);

            JobHandle dependency = state.Dependency;

            EndInitializationEntityCommandBufferSystem ecbSystem = state.World.GetExistingSystemManaged<EndInitializationEntityCommandBufferSystem>();

            EntityCommandBuffer ecb = ecbSystem.CreateCommandBuffer();
            dependency =
                new UpdatePositionInFormationJob
                    {
                        EntityCommandBuffer = ecb.AsParallelWriter(),

                        TroopSoldierLookup = _troopSoldierLookup,
                        NavMeshGridSize = FlowFieldProvider.GridSize,
                        NavMeshCellSize = FlowFieldProvider.CellSize,
                        NavMeshMinWorldPosition = FlowFieldProvider.MinWorldPositionInGrid,
                        NavMeshMask = FlowFieldProvider.NavMeshMask
                    }
                    .ScheduleParallel(_soldierQuery, dependency);
            ecbSystem.AddJobHandleForProducer(dependency);

            state.Dependency = dependency;
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }
}