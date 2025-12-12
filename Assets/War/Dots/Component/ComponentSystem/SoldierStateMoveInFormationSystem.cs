using Unity.Burst;
using Unity.Entities;


namespace War.Dots.Component.ComponentSystem
{
    [UpdateInGroup(typeof(Group.SoldierStateSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct SoldierStateMoveInFormationSystem : ISystem
    {
        [BurstCompile]
        private partial struct UpdateDestinationJob : IJobEntity
        {
            private static void Execute(ref Destination destination, in SoldierAttachedTroop soldierAttachedTroop) => destination.Position.xz = soldierAttachedTroop.PositionInFormation.xz;
        }


        private EntityQuery _soldierQuery;


        public void OnCreate(ref SystemState state)
        {
            _soldierQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<Soldier, Alive, SoldierStateMoveInFormation>()
                    .WithAll<SoldierAttachedTroop>()
                    .WithAllRW<Destination>()
                    .Build();
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state) => state.Dependency = new UpdateDestinationJob().ScheduleParallel(_soldierQuery, state.Dependency);
    }
}