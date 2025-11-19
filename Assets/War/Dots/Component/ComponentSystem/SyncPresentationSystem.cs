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
                (RefRO<NavMeshAgentData> agentData, RefRO<Acceleration> acceleration, RefRO<MoveSpeed> moveSpeed, RefRO<Destination> destination, RefRO<UnityNavMeshAgent> unityNavMeshAgent, RefRO<UnityNavMeshObstacle> unityNavMeshObstacle)
                in
                SystemAPI.Query<RefRO<NavMeshAgentData>, RefRO<Acceleration>, RefRO<MoveSpeed>, RefRO<Destination>, RefRO<UnityNavMeshAgent>, RefRO<UnityNavMeshObstacle>>()
                    .WithAll<Movable>())
            {
                NavMeshObstacle obstacle = unityNavMeshObstacle.ValueRO.Obstacle;
                if (obstacle && obstacle.enabled)
                {
                    obstacle.enabled = false;
                }

                NavMeshAgent agent = unityNavMeshAgent.ValueRO.Agent;
                if (agent)
                {
                    if (!agent.enabled)
                    {
                        agent.enabled = true;
                    }

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
                    
                    float3 agentDestination = agent.destination;

                    if (!mathf.Approximately(agentDestination.xz, destination.ValueRO.Position.xz))
                    {
                        agent.destination = destination.ValueRO.Position;
                    }
                }
            }

            foreach (
                (RefRO<NavMeshAgentData> agentData, RefRO<Forward> forward, RefRO<UnityNavMeshAgent> unityNavMeshAgent, RefRO<UnityNavMeshObstacle> unityNavMeshObstacle)
                in
                SystemAPI.Query<RefRO<NavMeshAgentData>, RefRO<Forward>, RefRO<UnityNavMeshAgent>, RefRO<UnityNavMeshObstacle>>()
                    .WithDisabled<Movable>())
            {
                NavMeshAgent agent = unityNavMeshAgent.ValueRO.Agent;
                if (agent && agent.enabled)
                {
                    agent.enabled = false;
                }

                NavMeshObstacle obstacle = unityNavMeshObstacle.ValueRO.Obstacle;
                if (obstacle)
                {
                    if (!obstacle.enabled)
                    {
                        obstacle.enabled = true;
                    }

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