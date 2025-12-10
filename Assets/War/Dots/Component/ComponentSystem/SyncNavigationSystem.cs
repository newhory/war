using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;


namespace War.Dots.Component.ComponentSystem
{
    using Navigation;


    [UpdateInGroup(typeof(Group.ViewSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct SyncNavigationSystem : ISystem
    {
        [BurstCompile]
        private partial struct SyncMovableJob : IJobEntity
        {
            [ReadOnly] public int2 NavMeshGridSize;
            [ReadOnly] public float NavMeshCellSize;
            [ReadOnly] public float3 NavMeshMinWorldPosition;
            [ReadOnly] public NativeArray<byte>.ReadOnly NavMeshMask;


            private void Execute(
                ref StandingObstacle standingObstacle, ref UnitDestination unitDestination, ref UnitMaxSpeed unitMaxSpeed,
                ref Destination destination, in MoveSpeed moveSpeed, in FlowFieldBlobReference flowFieldBlobReference)
            {
                standingObstacle.Movable = 1;

                unitMaxSpeed.Value = moveSpeed.CurrentMax;

                if (mathf.Approximately(destination.Position, destination.OldPosition))
                {
                    return;
                }

                unitDestination.FlowFieldId = -1;
                unitDestination.Value.xz = destination.Position.xz;

                for (int i = 0, count = flowFieldBlobReference.BlobAssetReference.Value.FlowFieldTargets.Length; i < count; ++i)
                {
                    ref FlowFieldTarget target = ref flowFieldBlobReference.BlobAssetReference.Value.FlowFieldTargets[i];
                    if (target.AreaBounds.Contains(destination.Position.xz))
                    {
                        unitDestination.FlowFieldId = target.FlowId;
                        break;
                    }
                }

                if (unitDestination.FlowFieldId < 0 &&
                    FlowFieldQuery.TryFindNearestWalkableWorldPosition(
                        unitDestination.Value, NavMeshMask, NavMeshGridSize, NavMeshCellSize, NavMeshMinWorldPosition, out float3 walkablePosition))
                {
                    float2 walkablePositionXZ = walkablePosition.xz;

                    for (int i = 0, count = flowFieldBlobReference.BlobAssetReference.Value.FlowFieldTargets.Length; i < count; ++i)
                    {
                        ref FlowFieldTarget target = ref flowFieldBlobReference.BlobAssetReference.Value.FlowFieldTargets[i];
                        if (target.AreaBounds.Contains(walkablePositionXZ))
                        {
                            unitDestination.FlowFieldId = target.FlowId;
                            destination.Position.xz = walkablePositionXZ;
                            unitDestination.Value.xz = walkablePositionXZ;
                            break;
                        }
                    }
                }

                destination.OldPosition = destination.Position;
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
                    .WithAll<Soldier, Alive, MoveSpeed, Destination, FlowFieldBlobReference>()
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

            dependency =
                new SyncMovableJob
                    {
                        NavMeshGridSize = FlowFieldProvider.GridSize,
                        NavMeshCellSize = FlowFieldProvider.CellSize,
                        NavMeshMinWorldPosition = FlowFieldProvider.MinWorldPositionInGrid,
                        NavMeshMask = FlowFieldProvider.NavMeshMask
                    }
                    .ScheduleParallel(_movableSoldierForUnitQuery, dependency);
            dependency = new SyncUnmovableJob().ScheduleParallel(_unmovableSoldierForUnitQuery, dependency);

            state.Dependency = dependency;
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }
}