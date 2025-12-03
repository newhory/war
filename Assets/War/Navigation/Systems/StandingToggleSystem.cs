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
            public float DeltaTime;
            public float SpeedThreshold; // 예: 0.05
            public float EnableDelay; // 예: 0.5 s


            private void Execute(
                ref StandingObstacle standingObstacle, ref StandingCooldown standingCooldown,
                ref UnitPosition unitPosition, in UnitDestination unitDestination, ref UnitVelocity unitVelocity)
            {
                if (standingObstacle.Movable == 0)
                {
                    standingObstacle.Enabled = 1;

                    return;
                }

                if (math.distance(unitPosition.Value, unitDestination.Value) < 0.05f)
                {
                    unitPosition.Value = unitDestination.Value;
                    unitVelocity.Value = float3.zero;

                    standingObstacle.Enabled = 1;

                    return;
                }

                standingObstacle.Enabled = 0;

                float speed = math.length(unitVelocity.Value);
                if (speed < SpeedThreshold)
                {
                    standingCooldown.BelowThresholdTime += DeltaTime;
                    if (standingCooldown.BelowThresholdTime >= EnableDelay)
                    {
                        standingObstacle.Enabled = 1;
                    }
                }
                else
                {
                    standingCooldown.BelowThresholdTime = 0f;
                    standingObstacle.Enabled = 0;
                }
            }
        }


        public void OnCreate(ref SystemState state)
        {
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state) =>
            state.Dependency =
                new ToggleJob
                    {
                        DeltaTime = SystemAPI.Time.DeltaTime,
                        SpeedThreshold = 0.05f,
                        EnableDelay = 0.5f
                    }
                    .ScheduleParallel(state.Dependency);
    }
}