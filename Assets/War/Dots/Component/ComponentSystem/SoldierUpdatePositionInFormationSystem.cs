using Unity.Burst;
using Unity.Collections;
using Unity.Entities;


namespace War.Dots.Component.ComponentSystem
{
    [UpdateInGroup(typeof(Group.JustSpawnedInitializeSystemGroup))]
    [UpdateAfter(typeof(TroopFormationResetSystem))]
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
                    .Build();

            _troopSoldierLookup = state.GetBufferLookup<TroopSoldier>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _troopSoldierLookup.Update(ref state);
            
            EntityCommandBuffer ecb = new(Allocator.TempJob);
            new UpdatePositionInFormationJob
                {
                    EntityCommandBuffer = ecb.AsParallelWriter(),

                    TroopSoldierLookup = _troopSoldierLookup
                }
                .ScheduleParallel(_soldierQuery, state.Dependency)
                .Complete();
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }
}