using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
#if HYBRID_ARROW
using System.Collections.Generic;
using UnityEngine.Pool;
#endif


namespace War.Dots.Component.ComponentSystem
{
    [UpdateInGroup(typeof(Group.SpawnSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct SpawnArrowSystem : ISystem
#if HYBRID_ARROW
        , ISystemStartStop
#endif
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


            public void Execute([EntityIndexInQuery] int entityIndex, DynamicBuffer<SpawnArrow> arrowSpawnDataBuffer)
            {
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

                    EntityCommandBuffer.AddComponent(entityIndex, arrowEntity, new LocalTransform { Position = arrowSpawnData.StartPosition, Scale = ArrowScale });
                    EntityCommandBuffer.AddComponent(entityIndex, arrowEntity, new PhysicsVelocity { Linear = vel });

                    EntityCommandBuffer.AddComponent(entityIndex, arrowEntity, new Arrow { Shooter = arrowSpawnData.Shooter });
                    EntityCommandBuffer.AddComponent(entityIndex, arrowEntity, new Team { Color = arrowSpawnData.ShooterTeamColor });
                    EntityCommandBuffer.AddComponent(entityIndex, arrowEntity, new Movable());
                    EntityCommandBuffer.AddComponent(entityIndex, arrowEntity, new Rotatable());
                    EntityCommandBuffer.AddComponent(entityIndex, arrowEntity, new Forward { Value = math.normalize(vel) });
                    EntityCommandBuffer.AddComponent(entityIndex, arrowEntity, new SoldierTargetForAttack { TargetSoldier = arrowSpawnData.Target });
                    EntityCommandBuffer.AddComponent(entityIndex, arrowEntity, new AttackPower { Value = arrowSpawnData.Damage });

                    EntityCommandBuffer.AddComponent(entityIndex, arrowEntity, new JustCreated());
                }

                arrowSpawnDataBuffer.Clear();
            }
        }

        private partial struct SetCollisionJob : IJobEntity
        {
            [ReadOnly] public int WorldLayer;
            [ReadOnly] public int RedTeamLayer;
            [ReadOnly] public int BlueTeamLayer;


            public void Execute(ref PhysicsCollider physicsCollider, in Team team)
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


        private EntityQuery _spawnArrowQuery;
        private EntityQuery _justCreatedArrowQuery;

#if HYBRID_ARROW
        private static ObjectPool<UnityEngine.GameObject> s_arrowPool;
        private static List<(Entity entity, PooledGameObject soldierViewComponent)> s_pooledGameObjectBuffer;

        public void OnStartRunning(ref SystemState state)
        {
            s_arrowPool ??=
                new ObjectPool<UnityEngine.GameObject>(
                    createFunc: () => UnityEngine.Object.Instantiate(Setting.Instance.arrowRenderMeshPrefab),
                    actionOnGet: gameObject => gameObject.SetActive(true),
                    actionOnRelease: gameObject => gameObject?.SetActive(false),
                    actionOnDestroy: UnityEngine.Object.Destroy,
                    collectionCheck: true, // An Editor-only check that determines if an instance is returned back to the pool. Throws an exception if the instance is already in the pool.
                    defaultCapacity: 10);

            s_pooledGameObjectBuffer = new List<(Entity entity, PooledGameObject soldierViewComponent)>();
        }

        public void OnStopRunning(ref SystemState state)
        {
            s_arrowPool?.Dispose();
            s_arrowPool = null;
        }
#endif
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
            if (_spawnArrowQuery.CalculateEntityCount() == 0)
            {
                return;
            }

            ArrowSpawner arrowSpawner = SystemAPI.GetSingleton<ArrowSpawner>();

            EntityCommandBuffer ecb = new(Allocator.TempJob);
            new SpawnArrowJob
                {
                    EntityCommandBuffer = ecb.AsParallelWriter(),

                    ProtoType = arrowSpawner.ArrowProtoType,
                    ArrowScale = arrowSpawner.ArrowScale,
                    Gravity = UnityEngine.Physics.gravity.y,
                }
                .ScheduleParallel(_spawnArrowQuery, state.Dependency)
                .Complete();
            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            if (_justCreatedArrowQuery.CalculateEntityCount() == 0)
            {
                return;
            }

            new SetCollisionJob
                {
                    WorldLayer = Setting.WorldLayer,
                    RedTeamLayer = Setting.RedTeamLayer,
                    BlueTeamLayer = Setting.BlueTeamLayer,
                }
                .ScheduleParallel(_justCreatedArrowQuery, state.Dependency)
                .Complete();

#if HYBRID_ARROW
            s_pooledGameObjectBuffer.Clear();

            ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (
                (RefRO<JustCreated> _, Entity entity)
                in
                SystemAPI.Query<RefRO<JustCreated>>()
                    .WithAll<Arrow>()
                    .WithNone<UnityTransform>()
                    .WithEntityAccess())
            {
                PooledObject<UnityEngine.GameObject> pooled = s_arrowPool.Get(out UnityEngine.GameObject gameObject);

                gameObject.SetActive(true);

                s_pooledGameObjectBuffer.Add((entity, new PooledGameObject { PooledObject = pooled }));

                ecb.AddComponent(entity, new UnityTransform { Transform = gameObject.transform });
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            foreach (var (entity, pooledGameObject) in s_pooledGameObjectBuffer)
            {
                state.EntityManager.AddComponentData(entity, pooledGameObject);
            }
#endif
            ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (
                (RefRO<JustCreated> _, Entity entity)
                in
                SystemAPI.Query<RefRO<JustCreated>>()
                    .WithAll<Arrow>()
                    .WithEntityAccess())
            {
                ecb.RemoveComponent<JustCreated>(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}