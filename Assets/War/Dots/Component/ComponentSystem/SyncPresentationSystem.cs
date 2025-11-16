using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.AI;


namespace War.Dots.Component.ComponentSystem
{
    [UpdateInGroup(typeof(Group.ViewSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct SyncPresentationSystem : ISystem
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
                (RefRO<NavMeshAgentData> agentData, RefRO<Forward> forward, RefRO<Acceleration> acceleration, RefRO<MoveSpeed> moveSpeed, RefRO<Destination> destination, RefRO<UnityNavMeshAgent> unityNavMeshAgent, RefRO<UnityNavMeshObstacle> unityNavMeshObstacle, Entity entity)
                in
                SystemAPI.Query<RefRO<NavMeshAgentData>, RefRO<Forward>, RefRO<Acceleration>, RefRO<MoveSpeed>, RefRO<Destination>, RefRO<UnityNavMeshAgent>, RefRO<UnityNavMeshObstacle>>()
                    .WithEntityAccess())
            {
                bool isMovable = state.EntityManager.IsComponentEnabled<Movable>(entity);

                NavMeshAgent agent = unityNavMeshAgent.ValueRO.Agent.Value;
                NavMeshObstacle obstacle = unityNavMeshObstacle.ValueRO.Obstacle.Value;

                if (isMovable)
                {
                    obstacle.enabled = false;
                    agent.enabled = true;
                }
                else
                {
                    agent.enabled = false;
                    obstacle.enabled = true;
                }

                if (isMovable && agent)
                {
                    if (!Mathf.Approximately(agentData.ValueRO.Radius, agent.radius))
                    {
                        agent.radius = agentData.ValueRO.Radius;
                    }

                    if (!Mathf.Approximately(agent.acceleration, acceleration.ValueRO.Max))
                    {
                        agent.acceleration = acceleration.ValueRO.Max;
                    }

                    if (!Mathf.Approximately(agent.speed, moveSpeed.ValueRO.CurrentMax))
                    {
                        agent.speed = moveSpeed.ValueRO.CurrentMax;
                    }

                    if (!mathf.Approximately(agent.destination, destination.ValueRO.Position))
                    {
                        agent.destination = destination.ValueRO.Position;
                    }
                }

                if (!isMovable && obstacle)
                {
                    float obstacleRadius = agentData.ValueRO.Radius * 0.5f;

                    if (!Mathf.Approximately(obstacleRadius, obstacle.radius))
                    {
                        obstacle.radius = obstacleRadius;
                    }

                    if (!mathf.Approximately(forward.ValueRO.Value, float3.zero))
                    {
                        obstacle.transform.forward = forward.ValueRO.Value;
                    }
                }
            }
        }
    }
}