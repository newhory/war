using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.AI;


namespace War.Dots.Component.ComponentSystem
{
    [UpdateInGroup(typeof(Group.FirstUpdateGroup))]
    [UpdateBefore(typeof(SoldierSpatialHashMapBuildSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct SyncFromPresentationSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            foreach (
                (RefRW<LocalTransform> localTransform, RefRW<Velocity> velocity, RefRW<MoveSpeed> moveSpeed, RefRO<UnityNavMeshAgent> unityNavMeshAgent)
                in
                SystemAPI.Query<RefRW<LocalTransform>, RefRW<Velocity>, RefRW<MoveSpeed>, RefRO<UnityNavMeshAgent>>()
                    .WithAll<Alive, NavMeshAgentData>())
            {
                NavMeshAgent agent = unityNavMeshAgent.ValueRO.Agent.Value;
                if (agent)
                {
                    Transform agentTransform = agent.transform;

                    localTransform.ValueRW.Position = agentTransform.position;
                    localTransform.ValueRW.Rotation = agentTransform.rotation;

                    float3 agentVelocity = agent.velocity;
                    
                    velocity.ValueRW.Value = agentVelocity;
                    moveSpeed.ValueRW.Current = math.length(agentVelocity.xz);
                }
            }
        }
    }
}