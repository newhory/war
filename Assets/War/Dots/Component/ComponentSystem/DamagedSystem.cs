using Unity.Burst;
using Unity.Entities;


namespace War.Dots.Component.ComponentSystem
{
    [UpdateInGroup(typeof(Group.DamagedSystemGroup))]
    [UpdateAfter(typeof(SoldierDamagedSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct DamagedSystem : ISystem
    {
        [BurstCompile]
        private partial struct ApplyDamageJob : IJobEntity
        {
            private static void Execute(DynamicBuffer<Damaged> damagedBuffer, ref Health health)
            {
                foreach (Damaged damaged in damagedBuffer)
                {
                    health.Value -= damaged.HitDamage;
                }

                damagedBuffer.Clear();
            }
        }


        private EntityQuery _damagedQuery;


        public void OnCreate(ref SystemState state) =>
            _damagedQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<Alive, Damaged>()
                    .WithAllRW<Health>()
                    .Build();

        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state) => state.Dependency = new ApplyDamageJob().ScheduleParallel(_damagedQuery, state.Dependency);
    }
}