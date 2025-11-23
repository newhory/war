using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;


namespace War.Dots.Component.ComponentSystem
{
    [UpdateInGroup(typeof(Group.DestroyOnSystemGroup))]
    [UpdateAfter(typeof(PooledGameObjectDestroyOnSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct DestroyOnSystem : ISystem
    {
        [BurstCompile]
        private partial struct CheckLifeJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter EntityCommandBuffer;

            [ReadOnly] public double CurrentTime;


            public void Execute(Entity entity, in DestroyOn destroyOn)
            {
                if (CurrentTime >= destroyOn.DestroyTime)
                {
                    EntityCommandBuffer.DestroyEntity(entity.Index, entity);
                }
            }
        }

        [BurstCompile]
        private partial struct CleanUpDamagedJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter EntityCommandBuffer;

            private void Execute(Entity entity)
            {
                EntityCommandBuffer.RemoveComponent<Damaged>(entity.Index, entity);
            }
        }

        [BurstCompile]
        private partial struct CleanUpSpawnHitEffectJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter EntityCommandBuffer;

            private void Execute(Entity entity)
            {
                EntityCommandBuffer.RemoveComponent<SpawnHitEffect>(entity.Index, entity);
            }
        }


        private EntityQuery _destroyOnQuery;
        private EntityQuery _cleanUpDamagedQuery;
        private EntityQuery _cleanUpSpawnHitEffectQuery;


        public void OnCreate(ref SystemState state)
        {
            _destroyOnQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<DestroyOn>()
                    .Build();

            _cleanUpDamagedQuery =
                SystemAPI.QueryBuilder()
                    .WithNone<Alive>()
                    .WithAll<Damaged>()
                    .Build();

            _cleanUpSpawnHitEffectQuery =
                SystemAPI.QueryBuilder()
                    .WithNone<Alive>()
                    .WithAll<SpawnHitEffect>()
                    .Build();
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            JobHandle dependency = state.Dependency;

            EndSimulationEntityCommandBufferSystem ecbSystem = state.World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();

            EntityCommandBuffer excludeSoldierEcb = ecbSystem.CreateCommandBuffer();
            dependency =
                new CheckLifeJob
                    {
                        EntityCommandBuffer = excludeSoldierEcb.AsParallelWriter(),

                        CurrentTime = SystemAPI.Time.ElapsedTime
                    }
                    .ScheduleParallel(_destroyOnQuery, dependency);
            ecbSystem.AddJobHandleForProducer(dependency);

            excludeSoldierEcb = ecbSystem.CreateCommandBuffer();
            dependency = new CleanUpDamagedJob { EntityCommandBuffer = excludeSoldierEcb.AsParallelWriter() }.ScheduleParallel(_cleanUpDamagedQuery, dependency);
            ecbSystem.AddJobHandleForProducer(dependency);

            excludeSoldierEcb = ecbSystem.CreateCommandBuffer();
            dependency = new CleanUpSpawnHitEffectJob { EntityCommandBuffer = excludeSoldierEcb.AsParallelWriter() }.ScheduleParallel(_cleanUpSpawnHitEffectQuery, dependency);
            ecbSystem.AddJobHandleForProducer(dependency);

            state.Dependency = dependency;
        }
    }
}