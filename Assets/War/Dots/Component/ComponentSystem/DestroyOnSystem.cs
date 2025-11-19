using Unity.Burst;
using Unity.Collections;
using Unity.Entities;


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


        private EntityQuery _destroyOnQuery;


        public void OnCreate(ref SystemState state) =>
            _destroyOnQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<DestroyOn>()
                    .Build();

        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            using EntityCommandBuffer excludeSoldierEcb = new(Allocator.TempJob);
            new CheckLifeJob
                {
                    EntityCommandBuffer = excludeSoldierEcb.AsParallelWriter(),

                    CurrentTime = SystemAPI.Time.ElapsedTime
                }
                .ScheduleParallel(_destroyOnQuery, state.Dependency)
                .Complete();
            excludeSoldierEcb.Playback(state.EntityManager);
        }
    }
}