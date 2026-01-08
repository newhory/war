using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;

namespace War.Game.Systems
{
    using Navigation;

    [UpdateInGroup(typeof(Navigation.Systems.Group.NavigationSystemGroup), OrderFirst = true)]
    [RequireMatchingQueriesForUpdate]
    public partial struct SyncNavigationSystem : ISystem
    {
        [BurstCompile]
        private partial struct SyncMovableJob : IJobEntity
        {
            private static void Execute(
                ref StandingObstacle standingObstacle, ref UnitDestination unitDestination, ref UnitMaxSpeed unitMaxSpeed,
                ref SoldierDestination soldierDestination, in MoveSpeed moveSpeed, in SoldierAttachedTroop soldierAttachedTroop)
            {
                standingObstacle.Movable = 1;

                unitMaxSpeed.Value = moveSpeed.CurrentMax;

                unitDestination.FlowFieldId = soldierAttachedTroop.TroopFlowFieldId;
                unitDestination.Value.xz = soldierDestination.Position.xz;
            }
        }

        [BurstCompile]
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
                    .WithAll<Soldier, Alive, MoveSpeed, SoldierDestination, SoldierAttachedTroop>()
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