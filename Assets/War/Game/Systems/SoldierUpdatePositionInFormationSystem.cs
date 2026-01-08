using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace War.Game.Systems
{
    using Navigation;
    
    [UpdateInGroup(typeof(Group.SoldierInitializeSystemGroup))]
    [UpdateBefore(typeof(SoldierInitialPositionSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct SoldierUpdatePositionInFormationSystem : ISystem
    {
        [BurstCompile]
        private partial struct UpdatePositionInFormationJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter EntityCommandBuffer;

            [ReadOnly] public BufferLookup<TroopSoldier> TroopSoldierLookup;


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
                        soldierAttachedTroop.PositionInFormation = troopSoldier.PositionInFormation;
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
                    .WithNone<UnitPosition>()
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

                        TroopSoldierLookup = _troopSoldierLookup
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