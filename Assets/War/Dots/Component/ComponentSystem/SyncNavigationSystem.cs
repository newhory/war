using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;


namespace War.Dots.Component.ComponentSystem
{
    using Navigation;


    [UpdateInGroup(typeof(Group.ViewSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct SyncNavigationSystem : ISystem
    {
        private partial struct SyncMovableJob : IJobEntity
        {
            private static void Execute(ref StandingObstacle standingObstacle, ref UnitDestination unitDestination, ref UnitMaxSpeed unitMaxSpeed, in Destination destination, in MoveSpeed moveSpeed)
            {
                standingObstacle.Movable = 1;

                unitDestination.Value = destination.Position;
                unitDestination.FlowFieldId = -1;

                unitMaxSpeed.Value = moveSpeed.CurrentMax;
            }
        }

        private partial struct SyncUnmovableJob : IJobEntity
        {
            private static void Execute(ref StandingObstacle standingObstacle) => standingObstacle.Movable = 0;
        }


        private EntityQuery _movableSoldierForUnitQuery;
        private EntityQuery _unmovableSoldierForUnitQuery;


        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _movableSoldierForUnitQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<Soldier, Alive, MoveSpeed, Destination>()
                    .WithAll<Movable>()
                    .WithAllRW<StandingObstacle>()
                    .WithAllRW<UnitDestination, UnitMaxSpeed>()
                    .Build();

            _unmovableSoldierForUnitQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<Soldier, Alive>()
                    .WithDisabled<Movable>()
                    .WithAllRW<StandingObstacle>()
                    .Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            JobHandle dependency = state.Dependency;

            dependency = new SyncMovableJob().ScheduleParallel(_movableSoldierForUnitQuery, dependency);
            dependency = new SyncUnmovableJob().ScheduleParallel(_unmovableSoldierForUnitQuery, dependency);

            state.Dependency = dependency;
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }
}