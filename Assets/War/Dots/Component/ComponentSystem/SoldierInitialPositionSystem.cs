using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;


namespace War.Dots.Component.ComponentSystem
{
    [UpdateInGroup(typeof(Group.JustSpawnedInitializeSystemGroup))]
    [UpdateAfter(typeof(SoldierUpdatePositionInFormationSystem))]
    [UpdateBefore(typeof(SoldierAddPresentationSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct SoldierInitialPositionSystem : ISystem
    {
        [BurstCompile]
        private partial struct UpdatePositionJob : IJobEntity
        {
            private static void Execute(ref LocalTransform localTransform, ref Destination destination, in SoldierAttachedTroop soldierAttachedTroop) =>
                destination.Position = localTransform.Position = soldierAttachedTroop.PositionInFormation;
        }


        private EntityQuery _formationUnitQuery;


        public void OnCreate(ref SystemState state) =>
            _formationUnitQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<Soldier, Alive, SoldierAttachedTroop>()
                    .WithAllRW<LocalTransform, Destination>()
                    .WithNone<PooledGameObject>()
                    .Build();

        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state) => state.Dependency = new UpdatePositionJob().ScheduleParallel(_formationUnitQuery, state.Dependency);
    }
}