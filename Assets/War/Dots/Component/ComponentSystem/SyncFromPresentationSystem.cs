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

        private EntityQuery _navMeshQuery;
        private ComponentLookup<Movable> _movableLookup;


        public void OnCreate(ref SystemState state)
        {
            _navMeshQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<NavMeshAgentData, UnityNavMeshAgent, UnityNavMeshObstacle>()
                    .WithAllRW<LocalTransform, Velocity>()
                    .WithAllRW<MoveSpeed>()
                    .WithPresent<Movable>()
                    .Build();

            _movableLookup = SystemAPI.GetComponentLookup<Movable>(true);
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            _movableLookup.Update(ref state);

            int entityCount = _navMeshQuery.CalculateEntityCount();

            NativeArray<SyncData> syncDataArray = new(entityCount, Allocator.TempJob);
            NativeArray<Entity> entityArray = _navMeshQuery.ToEntityArray(Allocator.TempJob);
            NativeArray<UnityNavMeshAgent> unityNavMeshAgents = _navMeshQuery.ToComponentDataArray<UnityNavMeshAgent>(Allocator.TempJob);
            NativeArray<UnityNavMeshObstacle> unityNavMeshObstacles = _navMeshQuery.ToComponentDataArray<UnityNavMeshObstacle>(Allocator.TempJob);

            for (int i = 0; i < entityCount; ++i)
            {
                if (_movableLookup.IsComponentEnabled(entityArray[i]))
                {
                    NavMeshAgent agent = unityNavMeshAgents[i].Agent;
                    if (agent)
                    {
                        Transform agentTransform = agent.transform;
                        float3 agentVelocity = agent.velocity;

                        syncDataArray[i] = new SyncData
                        {
                            Position = agentTransform.position,
                            Rotation = agentTransform.rotation,
                            Velocity = agentVelocity,
                            MoveSpeed = math.length(agentVelocity.xz)
                        };
                    }
                }
                else
                {
                    NavMeshObstacle obstacle = unityNavMeshObstacles[i].Obstacle;
                    if (obstacle)
                    {
                        Transform obstacleTransform = obstacle.transform;

                        syncDataArray[i] = new SyncData
                        {
                            Position = obstacleTransform.position,
                            Rotation = obstacleTransform.rotation,
                            Velocity = float3.zero,
                            MoveSpeed = 0f
                        };
                    }
                }
            }

            JobHandle dependency = state.Dependency;

            dependency = new SyncJob { SyncDataArray = syncDataArray.AsReadOnly() }.ScheduleParallel(_navMeshQuery, dependency);

            dependency =
                JobHandle.CombineDependencies(
                    syncDataArray.Dispose(dependency),
                    entityArray.Dispose(dependency),
                    JobHandle.CombineDependencies(
                        unityNavMeshAgents.Dispose(dependency),
                        unityNavMeshObstacles.Dispose(dependency)));

            state.Dependency = dependency;
        }
    }
}