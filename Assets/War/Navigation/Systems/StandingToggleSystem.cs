using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;


namespace War.Navigation.Systems
{
    [BurstCompile]
    [UpdateInGroup(typeof(Group.NavigationSystemGroup))]
    [UpdateAfter(typeof(NavigationGridSystem))]
    public partial struct StandingToggleSystem : ISystem
    {
        [BurstCompile]
        public partial struct ToggleJob : IJobEntity
        {
            private static void Execute(ref StandingObstacle standingObstacle, ref UnitPosition unitPosition, in UnitDestination unitDestination, ref UnitVelocity unitVelocity)
            {
                if (standingObstacle.Movable == 0)
                {
                    standingObstacle.Enabled = 1;

                    return;
                }

                if (math.distance(unitPosition.Value.xz, unitDestination.Value.xz) < 0.1f)
                {
                    unitPosition.Value = unitDestination.Value;
                    unitVelocity.Value = float3.zero;

                    standingObstacle.Enabled = 1;

                    return;
                }

                standingObstacle.Enabled = 0;
            }
        }


        public void OnCreate(ref SystemState state)
        {
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state) => state.Dependency = new ToggleJob().ScheduleParallel(state.Dependency);
    }
}