using Unity.Entities;
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
                (RefRW<MovableChanged> movableChanged, RefRO<UnityNavMeshAgent> unityNavMeshAgent, RefRO<UnityNavMeshObstacle> unityNavMeshObstacle)
                in
                SystemAPI.Query<RefRW<MovableChanged>, RefRO<UnityNavMeshAgent>, RefRO<UnityNavMeshObstacle>>()
                    .WithAll<Movable>())
            {
                if (!movableChanged.ValueRO.IsMovable)
                {
                    NavMeshObstacle obstacle = unityNavMeshObstacle.ValueRO.Obstacle;
                    if (obstacle && obstacle.enabled)
                    {
                        obstacle.enabled = false;
                    }

                    NavMeshAgent agent = unityNavMeshAgent.ValueRO.Agent;
                    if (agent && !agent.enabled)
                    {
                        agent.enabled = true;
                    }

                    movableChanged.ValueRW.IsMovable = true;
                }
            }

            foreach (
                (RefRW<MovableChanged> movableChanged, RefRO<UnityNavMeshAgent> unityNavMeshAgent, RefRO<UnityNavMeshObstacle> unityNavMeshObstacle)
                in
                SystemAPI.Query<RefRW<MovableChanged>, RefRO<UnityNavMeshAgent>, RefRO<UnityNavMeshObstacle>>()
                    .WithDisabled<Movable>())
            {
                if (movableChanged.ValueRO.IsMovable)
                {
                    NavMeshAgent agent = unityNavMeshAgent.ValueRO.Agent;
                    if (agent && agent.enabled)
                    {
                        agent.enabled = false;
                    }

                    NavMeshObstacle obstacle = unityNavMeshObstacle.ValueRO.Obstacle;
                    if (obstacle && !obstacle.enabled)
                    {
                        obstacle.enabled = true;
                    }

                    movableChanged.ValueRW.IsMovable = false;
                }
            }

            foreach (
                (RefRW<MoveSpeed> moveSpeed, RefRW<Destination> destination, RefRO<UnityNavMeshAgent> unityNavMeshAgent)
                in
                SystemAPI.Query<RefRW<MoveSpeed>, RefRW<Destination>, RefRO<UnityNavMeshAgent>>()
                    .WithAll<Movable>()
                    .WithChangeFilter<MoveSpeed>()
                    .WithChangeFilter<Destination>())
            {
                bool isSpeedChanged = false;

                if (!Mathf.Approximately(moveSpeed.ValueRO.CurrentMax, moveSpeed.ValueRO.OldMax))
                {
                    moveSpeed.ValueRW.OldMax = moveSpeed.ValueRO.CurrentMax;

                    isSpeedChanged = true;
                }

                bool isDestinationChanged = false;

                if (!mathf.Approximately(destination.ValueRO.Position.xz, destination.ValueRO.OldPosition.xz))
                {
                    destination.ValueRW.OldPosition = destination.ValueRO.Position;

                    isDestinationChanged = true;
                }

                if (isSpeedChanged || isDestinationChanged)
                {
                    NavMeshAgent agent = unityNavMeshAgent.ValueRO.Agent;
                    if (agent)
                    {
                        if (isSpeedChanged)
                        {
                            agent.speed = moveSpeed.ValueRO.CurrentMax;
                        }

                        if (isDestinationChanged)
                        {
                            agent.destination = destination.ValueRO.Position;
                        }
                    }
                }
            }

            foreach (
                (RefRW<Forward> forward, RefRO<UnityNavMeshObstacle> unityNavMeshObstacle)
                in
                SystemAPI.Query<RefRW<Forward>, RefRO<UnityNavMeshObstacle>>()
                    .WithDisabled<Movable>()
                    .WithChangeFilter<Forward>())
            {
                bool isForwardChanged = false;

                if (!mathf.Approximately(forward.ValueRO.Value, forward.ValueRO.OldValue))
                {
                    forward.ValueRW.OldValue = forward.ValueRO.Value;

                    isForwardChanged = true;
                }

                if (isForwardChanged)
                {
                    NavMeshObstacle obstacle = unityNavMeshObstacle.ValueRO.Obstacle;
                    if (obstacle)
                    {
                        obstacle.transform.forward = forward.ValueRO.Value;
                    }
                }
            }
        }
    }
}