using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.AI;


namespace War.Dots.Component.ComponentSystem
{
    /// <summary>
    /// Sync <see cref="UnityEngine.AI.NavMeshAgent"/> / <see cref="UnityEngine.AI.NavMeshObstacle"/> -> <see cref="Unity.Entities.IComponentData"/>.
    /// </summary>
    [UpdateInGroup(typeof(Group.FirstUpdateGroup))]
    [UpdateBefore(typeof(SoldierSpatialHashMapBuildSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct SyncFromPresentationSystem : ISystem
    {
        private struct SyncData
        {
            public float3 Position;
            public quaternion Rotation;
            public float3 Velocity;
            public float MoveSpeed;
        }

        [BurstCompile]
        private partial struct SyncJob : IJobEntity
        {
            [ReadOnly] public NativeArray<SyncData>.ReadOnly SyncDataArray;


            private void Execute([EntityIndexInQuery] int index, ref LocalTransform localTransform, ref Velocity velocity, ref MoveSpeed moveSpeed)
            {
                SyncData syncData = SyncDataArray[index];

                localTransform.Position = syncData.Position;
                localTransform.Rotation = syncData.Rotation;

                velocity.Value = syncData.Velocity;
                moveSpeed.Current = syncData.MoveSpeed;
            }
        }


        private EntityQuery _agentsQuery;
        private EntityQuery _obstaclesQuery;

        private NativeList<SyncData> _syncAgentBuffer;
        private NativeList<SyncData> _syncObstacleBuffer;


        public void OnCreate(ref SystemState state)
        {
            _agentsQuery = SystemAPI.QueryBuilder()
                .WithAll<UnityNavMeshAgent>()
                .WithAllRW<LocalTransform, Velocity>()
                .WithAllRW<MoveSpeed>()
                .WithAll<Movable>() // 이동 가능만
                .Build();

            _obstaclesQuery = SystemAPI.QueryBuilder()
                .WithAll<UnityNavMeshObstacle>()
                .WithAllRW<LocalTransform, Velocity>()
                .WithAllRW<MoveSpeed>()
                .WithDisabled<Movable>() // 이동 불가만
                .Build();

            _syncAgentBuffer = new NativeList<SyncData>(Allocator.Domain);
            _syncObstacleBuffer = new NativeList<SyncData>(Allocator.Domain);
        }

        public void OnDestroy(ref SystemState state)
        {
            if (_syncAgentBuffer.IsCreated)
            {
                _syncAgentBuffer.Dispose();
            }

            if (_syncObstacleBuffer.IsCreated)
            {
                _syncObstacleBuffer.Dispose();
            }
        }

        public void OnUpdate(ref SystemState state)
        {
            _syncAgentBuffer.Clear();

            NativeArray<UnityNavMeshAgent> unityNavMeshAgents = _agentsQuery.ToComponentDataArray<UnityNavMeshAgent>(Allocator.TempJob);

            for (int i = 0, count = unityNavMeshAgents.Length; i < count; ++i)
            {
                NavMeshAgent agent = unityNavMeshAgents[i].Agent;
                if (agent)
                {
                    agent.transform.GetPositionAndRotation(out Vector3 position, out Quaternion rotation);
                    float3 agentVelocity = agent.velocity;

                    _syncAgentBuffer.Add(
                        new SyncData
                        {
                            Position = position,
                            Rotation = rotation,
                            Velocity = agentVelocity,
                            MoveSpeed = math.length(agentVelocity.xz)
                        });
                }
            }

            _syncObstacleBuffer.Clear();

            NativeArray<UnityNavMeshObstacle> unityNavMeshObstacles = _obstaclesQuery.ToComponentDataArray<UnityNavMeshObstacle>(Allocator.TempJob);

            for (int i = 0, count = unityNavMeshObstacles.Length; i < count; ++i)
            {
                NavMeshObstacle obstacle = unityNavMeshObstacles[i].Obstacle;
                if (obstacle)
                {
                    obstacle.transform.GetPositionAndRotation(out Vector3 position, out Quaternion rotation);

                    _syncObstacleBuffer.Add(
                        new SyncData
                        {
                            Position = position,
                            Rotation = rotation,
                            Velocity = float3.zero,
                            MoveSpeed = 0f
                        });
                }
            }

            JobHandle dependency = state.Dependency;

            if (_syncAgentBuffer.Length > 0)
            {
                dependency = new SyncJob { SyncDataArray = _syncAgentBuffer.AsReadOnly() }.ScheduleParallel(_agentsQuery, dependency);
            }

            if (_syncObstacleBuffer.Length > 0)
            {
                dependency = new SyncJob { SyncDataArray = _syncObstacleBuffer.AsReadOnly() }.ScheduleParallel(_obstaclesQuery, dependency);
            }

            dependency =
                JobHandle.CombineDependencies(
                    unityNavMeshAgents.Dispose(dependency),
                    unityNavMeshObstacles.Dispose(dependency));

            state.Dependency = dependency;
        }
    }
}