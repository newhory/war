using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
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
            private static void Execute(ref LocalTransform localTransform, ref SoldierDestination soldierDestination, in SoldierAttachedTroop soldierAttachedTroop) => soldierDestination.Position = localTransform.Position = soldierAttachedTroop.PositionInFormation;
        }


        private EntityQuery _formationUnitQuery;
        private EntityQuery _formationUnitWithVatQuery;


        public void OnCreate(ref SystemState state)
        {
            _formationUnitQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<Soldier, Alive, SoldierAttachedTroop>()
                    .WithAllRW<LocalTransform, SoldierDestination>()
                    .WithNone<PooledGameObject, VAT.UseVertexAnimation>()
                    .Build();

            _formationUnitWithVatQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<Soldier, Alive, SoldierAttachedTroop>()
                    .WithAllRW<LocalTransform, SoldierDestination>()
                    .WithAll<VAT.WaitForInitialize, VAT.UseVertexAnimation>()
                    .Build();
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            JobHandle dependency = state.Dependency;

            dependency = new UpdatePositionJob().ScheduleParallel(_formationUnitQuery, dependency);
            dependency = new UpdatePositionJob().ScheduleParallel(_formationUnitWithVatQuery, dependency);

            state.Dependency = dependency;
        }
    }
}