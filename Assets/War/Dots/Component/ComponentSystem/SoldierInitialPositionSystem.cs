using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;


namespace War.Dots.Component.ComponentSystem
{
    [UpdateInGroup(typeof(Group.SoldierInitializeSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct SoldierInitialPositionSystem : ISystem
    {
        [BurstCompile]
        private partial struct UpdatePositionJob : IJobEntity
        {
            private static void Execute(ref LocalTransform localTransform, ref SoldierDestination soldierDestination, in SoldierAttachedTroop soldierAttachedTroop) =>
                soldierDestination.Position = localTransform.Position = soldierAttachedTroop.PositionInFormation;
        }


        private EntityQuery _formationUnitQuery;


        public void OnCreate(ref SystemState state) =>
            _formationUnitQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<Soldier, Alive, SoldierAttachedTroop>()
                    .WithAllRW<LocalTransform, SoldierDestination>()
                    .WithNone<PooledGameObject>()
                    .Build();

        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state) => state.Dependency = new UpdatePositionJob().ScheduleParallel(_formationUnitQuery, state.Dependency);
    }
}