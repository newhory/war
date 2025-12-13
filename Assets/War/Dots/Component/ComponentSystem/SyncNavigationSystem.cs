using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;


namespace War.Dots.Component.ComponentSystem
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
                ref SoldierDestination soldierDestination, in MoveSpeed moveSpeed, in FlowFieldBlobReference flowFieldBlobReference)
            {
                standingObstacle.Movable = 1;

                unitMaxSpeed.Value = moveSpeed.CurrentMax;

                if (mathf.Approximately(soldierDestination.Position, soldierDestination.OldPosition))
                {
                    return;
                }

                unitDestination.FlowFieldId = -1;
                unitDestination.Value.xz = soldierDestination.Position.xz;

                for (int i = 0, count = flowFieldBlobReference.BlobAssetReference.Value.FlowFieldTargets.Length; i < count; ++i)
                {
                    ref FlowFieldTarget target = ref flowFieldBlobReference.BlobAssetReference.Value.FlowFieldTargets[i];
                    if (target.AreaBounds.Contains(soldierDestination.Position.xz))
                    {
                        unitDestination.FlowFieldId = target.FlowId;
                        break;
                    }
                }

                soldierDestination.OldPosition = soldierDestination.Position;
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
                    .WithAll<Soldier, Alive, MoveSpeed, SoldierDestination, FlowFieldBlobReference>()
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