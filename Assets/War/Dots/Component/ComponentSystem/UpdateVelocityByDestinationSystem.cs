using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;


namespace War.Dots.Component.ComponentSystem
{
    [UpdateInGroup(typeof(Group.MoveToDestinationSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct UpdateVelocityByDestinationSystem : ISystem
    {
        [BurstCompile]
        private partial struct CalcVelocityJob : IJobEntity
        {
            private static void Execute(ref Velocity velocity, ref LocalTransform localTransform, ref Forward forward, in SoldierDestination soldierDestination, in MoveSpeed speed)
            {
                float2 position2d = localTransform.Position.xz;
                float2 dest2d = soldierDestination.Position.xz;

                if (math.distance(position2d, dest2d) < 0.001f)
                {
                    velocity.Value.x = 0f;
                    velocity.Value.z = 0f;

                    localTransform.Position.x = soldierDestination.Position.x;
                    localTransform.Position.z = soldierDestination.Position.z;
                }
                else
                {
                    forward.Value.xz = math.normalize(dest2d - position2d);

                    velocity.Value.xz = forward.Value.xz * speed.CurrentMax;
                }
            }
        }


        private EntityQuery _movableQuery;


        public void OnCreate(ref SystemState state) =>
            _movableQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<Movable, SoldierDestination, MoveSpeed>()
                    .WithAllRW<Velocity, LocalTransform>()
                    .WithAllRW<Forward>()
                    .WithNone<PhysicsVelocity, NavMeshAgentData, Navigation.UnitPosition>()
                    .Build();

        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state) => state.Dependency = new CalcVelocityJob().ScheduleParallel(_movableQuery, state.Dependency);
    }
}