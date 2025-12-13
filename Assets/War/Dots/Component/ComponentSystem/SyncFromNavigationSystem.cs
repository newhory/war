using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;


namespace War.Dots.Component.ComponentSystem
{
    using Navigation;


    [UpdateInGroup(typeof(Navigation.Systems.Group.NavigationSystemGroup))]
    [UpdateAfter(typeof(Navigation.Systems.UnitMovementSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct SyncFromNavigationSystem : ISystem
    {
        [BurstCompile]
        private partial struct SyncMovableJob : IJobEntity
        {
            private static void Execute(ref LocalTransform localTransform, ref Forward forward, ref Velocity velocity, ref MoveSpeed moveSpeed, in UnitPosition unitPosition, in UnitVelocity unitVelocity, in StandingObstacle standingObstacle)
            {
                localTransform.Position = unitPosition.Value;

                if (standingObstacle.Enabled == 0)
                {
                    forward.Value = math.normalizesafe(unitVelocity.Value, forward.Value);
                }

                velocity.Value = unitVelocity.Value;
                moveSpeed.Current = math.length(unitVelocity.Value.xz);
            }
        }

        [BurstCompile]
        private partial struct SyncUnmovableJob : IJobEntity
        {
            private static void Execute(ref LocalTransform localTransform, ref Velocity velocity, ref MoveSpeed moveSpeed, in UnitPosition unitPosition)
            {
                localTransform.Position = unitPosition.Value;

                velocity.Value = float3.zero;
                moveSpeed.Current = 0f;
            }
        }


        private EntityQuery _movableSoldierForUnitQuery;
        private EntityQuery _unmovableSoldierForUnitQuery;


        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _movableSoldierForUnitQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<Soldier, Alive, UnitPosition, UnitVelocity, StandingObstacle>()
                    .WithAll<Movable>()
                    .WithAllRW<LocalTransform, Forward>()
                    .WithAllRW<Velocity, MoveSpeed>()
                    .Build();

            _unmovableSoldierForUnitQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<Soldier, Alive, UnitPosition>()
                    .WithDisabled<Movable>()
                    .WithAllRW<LocalTransform>()
                    .WithAllRW<Velocity, MoveSpeed>()
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