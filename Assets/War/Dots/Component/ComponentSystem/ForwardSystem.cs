using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;


namespace War.Dots.Component.ComponentSystem
{
    using Navigation;
    
    
    [UpdateInGroup(typeof(Group.UpdatePositionSystemGroup), OrderLast = true)]
    [RequireMatchingQueriesForUpdate]
    public partial struct ForwardSystem : ISystem
    {
        [BurstCompile]
        private partial struct UpdateForwardJob : IJobEntity
        {
            private static void Execute(ref LocalTransform localTransform, in Forward forward)
            {
                if (!mathf.Approximately(forward.Value, float3.zero))
                {
                    localTransform.Rotation = quaternion.LookRotation(forward.Value, math.up());
                }
            }
        }


        private EntityQuery _syncTransformToGroup;


        public void OnCreate(ref SystemState state) =>
            _syncTransformToGroup =
                SystemAPI.QueryBuilder()
                    .WithAll<Rotatable, Forward>()
                    .WithAllRW<LocalTransform>()
                    .WithNone<UnityNavMeshAgent>()
                    .Build();

        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state) => state.Dependency = new UpdateForwardJob().ScheduleParallel(_syncTransformToGroup, state.Dependency);
    }
}