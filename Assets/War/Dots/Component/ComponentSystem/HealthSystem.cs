using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;


namespace War.Dots.Component.ComponentSystem
{
    [UpdateInGroup(typeof(Group.HealthSystemGroup))]
    [UpdateAfter(typeof(SoldierHealthSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct HealthSystem : ISystem
    {
        [BurstCompile]
        private partial struct CheckHealthJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter EntityCommandBuffer;

            [ReadOnly] public double CurrentTime;


            public void Execute(Entity entity, in Health health)
            {
                if (health.Value <= 0)
                {
                    EntityCommandBuffer.AddComponent(entity.Index, entity, new DestroyOn { DestroyTime = CurrentTime + 2.0f });
                }
            }
        }


        private EntityQuery _healthQuery;


        public void OnCreate(ref SystemState state) =>
            _healthQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<Health>()
                    .WithNone<DestroyOn>()
                    .Build();

        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            JobHandle dependency = state.Dependency;

            EndSimulationEntityCommandBufferSystem ecbSystem = state.World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();

            EntityCommandBuffer excludeSoldierEcb = ecbSystem.CreateCommandBuffer();
            dependency =
                new CheckHealthJob
                    {
                        EntityCommandBuffer = excludeSoldierEcb.AsParallelWriter(),

                        CurrentTime = SystemAPI.Time.ElapsedTime
                    }
                    .ScheduleParallel(_healthQuery, dependency);
            ecbSystem.AddJobHandleForProducer(dependency);

            state.Dependency = dependency;
        }
    }
}