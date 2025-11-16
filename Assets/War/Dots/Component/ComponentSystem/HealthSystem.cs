using Unity.Burst;
using Unity.Collections;
using Unity.Entities;


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


            public void Execute([EntityIndexInQuery] int entityIndex, Entity entity, in Health health)
            {
                if (health.Value <= 0)
                {
                    EntityCommandBuffer.AddComponent(entityIndex, entity, new DestroyOn { DestroyTime = CurrentTime + 2.0f });
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
            using EntityCommandBuffer excludeSoldierEcb = new(Allocator.TempJob);
            new CheckHealthJob
                {
                    EntityCommandBuffer = excludeSoldierEcb.AsParallelWriter(),

                    CurrentTime = SystemAPI.Time.ElapsedTime
                }
                .ScheduleParallel(_healthQuery, state.Dependency)
                .Complete();
            excludeSoldierEcb.Playback(state.EntityManager);
        }
    }
}