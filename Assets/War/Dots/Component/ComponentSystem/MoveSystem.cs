using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Physics;


namespace War.Dots.Component.ComponentSystem
{
    [UpdateInGroup(typeof(Group.MoveSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct MoveSystem : ISystem
    {
        [BurstCompile]
        private partial struct UpdatePositionJob : IJobEntity
        {
            [ReadOnly] public float DeltaTime;


            public void Execute(ref Velocity velocity, ref LocalTransform localTransform, in Acceleration acceleration)
            {
                float3 currentVelocity = velocity.Value;

                currentVelocity += acceleration.Value * DeltaTime;

                velocity.Value = currentVelocity;
                localTransform.Position += currentVelocity * DeltaTime;
            }
        }


        private EntityQuery _moveGroup;


        public void OnCreate(ref SystemState state) =>
            _moveGroup =
                SystemAPI.QueryBuilder()
                    .WithAll<Movable, Acceleration>()
                    .WithAllRW<Velocity, LocalTransform>()
                    .WithNone<PhysicsVelocity, UnityNavMeshAgent>()
                    .Build();

        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state) => state.Dependency = new UpdatePositionJob { DeltaTime = SystemAPI.Time.DeltaTime }.ScheduleParallel(_moveGroup, state.Dependency);
    }
}