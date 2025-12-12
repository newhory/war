using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;


namespace War.Dots.Component.ComponentSystem
{
    [UpdateInGroup(typeof(Group.DestroyOnSystemGroup))]
    [UpdateBefore(typeof(PooledGameObjectDestroyOnSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct SoldierDestroyOnSystem : ISystem
    {
        [BurstCompile]
        private partial struct CatchNeedToUpdateFormation : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter EntityCommandBuffer;

            [ReadOnly] public double CurrentTime;
            [ReadOnly] public ComponentLookup<LocalTransform> LocalTransformLookup;
            [ReadOnly] public ComponentLookup<TroopFormationReset> TroopFormationResetLookup;


            private void Execute([EntityIndexInQuery] int index, in DestroyOn destroyOn, in SoldierAttachedTroop soldierAttachedTroop)
            {
                if (CurrentTime >= destroyOn.DestroyTime)
                {
                    if (TroopFormationResetLookup.HasComponent(soldierAttachedTroop.TroopEntity) &&
                        !TroopFormationResetLookup.IsComponentEnabled(soldierAttachedTroop.TroopEntity))
                    {
                        float3 troopPosition = LocalTransformLookup[soldierAttachedTroop.TroopEntity].Position;

                        EntityCommandBuffer.SetComponentEnabled<TroopFormationReset>(index, soldierAttachedTroop.TroopEntity, true);
                        EntityCommandBuffer.SetComponent(index, soldierAttachedTroop.TroopEntity, new TroopFormationReset { TroopPosition = troopPosition });
                    }
                }
            }
        }


        private EntityQuery _destroyOnSoldierQuery;
        private ComponentLookup<LocalTransform> _localTransformLookup;
        private ComponentLookup<TroopFormationReset> _troopFormationResetLookup;


        public void OnCreate(ref SystemState state)
        {
            _destroyOnSoldierQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<Soldier, DestroyOn, SoldierAttachedTroop>()
                    .Build();

            _localTransformLookup = state.GetComponentLookup<LocalTransform>(true);
            _troopFormationResetLookup = state.GetComponentLookup<TroopFormationReset>(true);
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            _localTransformLookup.Update(ref state);
            _troopFormationResetLookup.Update(ref state);

            JobHandle dependency = state.Dependency;

            EndSimulationEntityCommandBufferSystem ecbSystem = state.World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();

            EntityCommandBuffer ecb = ecbSystem.CreateCommandBuffer();
            dependency =
                new CatchNeedToUpdateFormation
                    {
                        EntityCommandBuffer = ecb.AsParallelWriter(),

                        CurrentTime = SystemAPI.Time.ElapsedTime,
                        LocalTransformLookup = _localTransformLookup,
                        TroopFormationResetLookup = _troopFormationResetLookup,
                    }
                    .ScheduleParallel(_destroyOnSoldierQuery, dependency);
            ecbSystem.AddJobHandleForProducer(dependency);

            state.Dependency = dependency;
        }
    }
}