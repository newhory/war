using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;


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


            private void Execute([EntityIndexInQuery] int index, Entity entity, in DestroyOn destroyOn)
            {
                if (CurrentTime >= destroyOn.DestroyTime)
                {
                    EntityCommandBuffer.DestroyEntity(index, entity);
                }
            }
        }

        [BurstCompile]
        private partial struct CheckLifeChildrenJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter EntityCommandBuffer;

            [ReadOnly] public double CurrentTime;


            private void Execute([EntityIndexInQuery] int index, Entity entity, DynamicBuffer<Child> children,
                in DestroyOn destroyOn)
            {
                if (CurrentTime >= destroyOn.DestroyTime)
                {
                    for (int i = 0, count = children.Length; i < count; ++i)
                    {
                        EntityCommandBuffer.DestroyEntity(index, children[i].Value);
                    }

                    EntityCommandBuffer.DestroyEntity(index, entity);
                }
            }
        }

        [BurstCompile]
        private partial struct CleanUpDamagedJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter EntityCommandBuffer;


            private void Execute([EntityIndexInQuery] int index, Entity entity) =>
                EntityCommandBuffer.RemoveComponent<Damaged>(index, entity);
        }

        [BurstCompile]
        private partial struct CleanUpSpawnHitEffectJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter EntityCommandBuffer;


            private void Execute([EntityIndexInQuery] int index, Entity entity) =>
                EntityCommandBuffer.RemoveComponent<SpawnHitEffect>(index, entity);
        }


        private EntityQuery _cleanUpDamagedQuery;
        private EntityQuery _cleanUpSpawnHitEffectQuery;


        public void OnCreate(ref SystemState state)
        {
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

            EndSimulationEntityCommandBufferSystem ecbSystem =
                state.World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();

            EntityCommandBuffer excludeSoldierEcb = ecbSystem.CreateCommandBuffer();
            dependency =
                new CheckLifeJob
                    {
                        EntityCommandBuffer = excludeSoldierEcb.AsParallelWriter(),

                        CurrentTime = SystemAPI.Time.ElapsedTime
                    }
                    .ScheduleParallel(dependency);
            ecbSystem.AddJobHandleForProducer(dependency);

            excludeSoldierEcb = ecbSystem.CreateCommandBuffer();
            dependency =
                new CheckLifeChildrenJob
                    {
                        EntityCommandBuffer = excludeSoldierEcb.AsParallelWriter(),

                        CurrentTime = SystemAPI.Time.ElapsedTime
                    }
                    .ScheduleParallel(dependency);
            ecbSystem.AddJobHandleForProducer(dependency);

            excludeSoldierEcb = ecbSystem.CreateCommandBuffer();
            dependency =
                new CleanUpDamagedJob { EntityCommandBuffer = excludeSoldierEcb.AsParallelWriter() }.ScheduleParallel(
                    _cleanUpDamagedQuery, dependency);
            ecbSystem.AddJobHandleForProducer(dependency);

            excludeSoldierEcb = ecbSystem.CreateCommandBuffer();
            dependency =
                new CleanUpSpawnHitEffectJob { EntityCommandBuffer = excludeSoldierEcb.AsParallelWriter() }
                    .ScheduleParallel(_cleanUpSpawnHitEffectQuery, dependency);
            ecbSystem.AddJobHandleForProducer(dependency);

            state.Dependency = dependency;
        }
    }
}