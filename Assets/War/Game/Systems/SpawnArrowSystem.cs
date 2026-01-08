using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace War.Game.Systems
{
    [UpdateInGroup(typeof(Group.SpawnSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct SpawnArrowSystem : ISystem
    {
        private struct JustCreated : IComponentData
        {
        }

        [BurstCompile]
        private partial struct SpawnArrowJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter EntityCommandBuffer;

            [ReadOnly] public Entity ProtoType;
            [ReadOnly] public float ArrowScale;
            [ReadOnly] public float Gravity;


            private void Execute([EntityIndexInQuery] int entityIndex, DynamicBuffer<SpawnArrow> arrowSpawnDataBuffer)
            {
                if (arrowSpawnDataBuffer.IsEmpty)
                {
                    return;
                }
                
                foreach (SpawnArrow arrowSpawnData in arrowSpawnDataBuffer)
                {
                    Entity arrowEntity = EntityCommandBuffer.Instantiate(entityIndex, ProtoType);

                    float halfDuration = math.sqrt(2f * arrowSpawnData.MaxPoiDeviation / math.abs(Gravity));
                    float duration = halfDuration * 2f;

                    float3 dXZ = arrowSpawnData.EndPosition - arrowSpawnData.StartPosition;
                    dXZ.y = 0;
                    float3 dirXZ = math.normalize(dXZ);

                    float3 vel = dirXZ * math.length(dXZ) / duration;
                    if (math.length(vel) < arrowSpawnData.MinSpeed)
                    {
                        vel = dirXZ * arrowSpawnData.MinSpeed;
                    }

                    vel.y = -0.5f * duration * Gravity;

                    float3 forward = math.normalize(vel);

                    EntityCommandBuffer.AddComponent(entityIndex, arrowEntity, new LocalTransform { Position = arrowSpawnData.StartPosition, Rotation = quaternion.LookRotationSafe(forward, math.up()), Scale = ArrowScale });
                    EntityCommandBuffer.AddComponent(entityIndex, arrowEntity, new PhysicsVelocity { Linear = vel });

                    EntityCommandBuffer.AddComponent(entityIndex, arrowEntity, new Arrow { Shooter = arrowSpawnData.Shooter });
                    EntityCommandBuffer.AddComponent(entityIndex, arrowEntity, new Team { Color = arrowSpawnData.ShooterTeamColor });
                    EntityCommandBuffer.AddComponent(entityIndex, arrowEntity, new Movable());
                    EntityCommandBuffer.AddComponent(entityIndex, arrowEntity, new Rotatable());
                    EntityCommandBuffer.AddComponent(entityIndex, arrowEntity, new Forward { Value = forward });
                    EntityCommandBuffer.AddComponent(entityIndex, arrowEntity, new SoldierTargetForAttack { TargetSoldier = arrowSpawnData.Target });
                    EntityCommandBuffer.AddComponent(entityIndex, arrowEntity, new AttackPower { Value = arrowSpawnData.Damage });

                    EntityCommandBuffer.AddComponent(entityIndex, arrowEntity, new JustCreated());
                }

                arrowSpawnDataBuffer.Clear();
            }
        }

        [BurstCompile]
        private partial struct SetCollisionJob : IJobEntity
        {
            [ReadOnly] public int WorldLayer;
            [ReadOnly] public int RedTeamLayer;
            [ReadOnly] public int BlueTeamLayer;


            private void Execute(ref PhysicsCollider physicsCollider, in Team team)
            {
                unsafe
                {
                    CollisionFilter collisionFilter = physicsCollider.ColliderPtr->GetCollisionFilter();

                    collisionFilter.CollidesWith =
                        1u << team.Color switch { TeamColor.Blue => RedTeamLayer, TeamColor.Red => BlueTeamLayer, _ => 0 } | 1u << WorldLayer;

                    switch (physicsCollider.Value.Value.Type)
                    {
                        case ColliderType.Box:
                            physicsCollider.Value = BoxCollider.Create(((BoxCollider*)physicsCollider.ColliderPtr)->Geometry, collisionFilter);
                            break;
                    }

                    physicsCollider.ColliderPtr->SetCollisionResponse(CollisionResponsePolicy.CollideRaiseCollisionEvents);
                }
            }
        }

        [BurstCompile]
        private partial struct RemoveJustCreatedJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter EntityCommandBuffer;


            private void Execute(Entity entity) => EntityCommandBuffer.RemoveComponent<JustCreated>(entity.Index, entity);
        }


        private EntityQuery _spawnArrowQuery;
        private EntityQuery _justCreatedArrowQuery;


        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ArrowSpawner>();
            state.RequireForUpdate<SpawnArrow>();

            _spawnArrowQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<SpawnArrow>()
                    .Build();

            _justCreatedArrowQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<Arrow, Team, JustCreated>()
                    .WithAllRW<PhysicsCollider>()
                    .Build();
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            JobHandle dependency = state.Dependency;

            ArrowSpawner arrowSpawner = SystemAPI.GetSingleton<ArrowSpawner>();

            EndSimulationEntityCommandBufferSystem ecbSystem = state.World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();

            EntityCommandBuffer ecb = ecbSystem.CreateCommandBuffer();
            dependency =
                new SpawnArrowJob
                    {
                        EntityCommandBuffer = ecb.AsParallelWriter(),

                        ProtoType = arrowSpawner.ArrowProtoType,
                        ArrowScale = arrowSpawner.ArrowScale,
                        Gravity = UnityEngine.Physics.gravity.y,
                    }
                    .ScheduleParallel(_spawnArrowQuery, dependency);
            ecbSystem.AddJobHandleForProducer(dependency);

            dependency =
                new SetCollisionJob
                    {
                        WorldLayer = Setting.WorldLayer,
                        RedTeamLayer = Setting.RedTeamLayer,
                        BlueTeamLayer = Setting.BlueTeamLayer,
                    }
                    .ScheduleParallel(_justCreatedArrowQuery, dependency);

            ecb = ecbSystem.CreateCommandBuffer();
            dependency = new RemoveJustCreatedJob { EntityCommandBuffer = ecb.AsParallelWriter() }.ScheduleParallel(_justCreatedArrowQuery, dependency);
            ecbSystem.AddJobHandleForProducer(dependency);

            state.Dependency = dependency;
        }
    }
}