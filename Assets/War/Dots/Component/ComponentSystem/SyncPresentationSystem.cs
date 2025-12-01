using Unity.Entities;
using UnityEngine;
using UnityEngine.AI;


namespace War.Dots.Component.ComponentSystem
{
    /// <summary>
    /// Sync <see cref="Unity.Entities.IComponentData"/> -> <see cref="UnityEngine.AI.NavMeshAgent"/> / <see cref="UnityEngine.AI.NavMeshObstacle"/>.
    /// </summary>
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
                MoveSpeed moveSpeedValue = moveSpeed.ValueRO;
                if (!Mathf.Approximately(moveSpeedValue.CurrentMax, moveSpeedValue.OldMax))
                {
                    moveSpeed.ValueRW.OldMax = moveSpeedValue.CurrentMax;

                    isSpeedChanged = true;
                }

                bool isDestinationChanged = false;
                Destination destinationValue = destination.ValueRO;
                if (!mathf.Approximately(destinationValue.Position.xz, destinationValue.OldPosition.xz))
                {
                    destination.ValueRW.OldPosition = destinationValue.Position;

                    isDestinationChanged = true;
                }

                if (isSpeedChanged || isDestinationChanged)
                {
                    NavMeshAgent agent = unityNavMeshAgent.ValueRO.Agent;
                    if (agent)
                    {
                        if (isSpeedChanged)
                        {
                            agent.speed = moveSpeedValue.CurrentMax;
                        }

                        if (isDestinationChanged)
                        {
                            agent.destination = destinationValue.Position;
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
                Forward forwardValue = forward.ValueRO;
                if (!mathf.Approximately(forwardValue.Value, forwardValue.OldValue))
                {
                    forward.ValueRW.OldValue = forwardValue.Value;

                    isForwardChanged = true;
                }

                if (isForwardChanged)
                {
                    NavMeshObstacle obstacle = unityNavMeshObstacle.ValueRO.Obstacle;
                    if (obstacle)
                    {
                        obstacle.transform.forward = forwardValue.Value;
                    }
                }
            }
        }
    }
}