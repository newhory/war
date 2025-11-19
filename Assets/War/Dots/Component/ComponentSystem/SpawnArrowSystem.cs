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
                    EntityCommandBuffer.AddComponent(entityIndex, arrowEntity, new TargetForAttack { Target = arrowSpawnData.Target });
                    EntityCommandBuffer.AddComponent(entityIndex, arrowEntity, new AttackPower { Value = arrowSpawnData.Damage });

                    EntityCommandBuffer.AddComponent(entityIndex, arrowEntity, new JustCreated());
                }

                arrowSpawnDataBuffer.Clear();
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
                    actionOnRelease: gameObject => gameObject.SetActive(false),
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
                    .WithAll<Arrow, JustCreated>()
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

            foreach (
                (RefRW<PhysicsCollider> physicsCollider, RefRO<Team> team)
                in
                SystemAPI.Query<RefRW<PhysicsCollider>, RefRO<Team>>()
                    .WithAll<Arrow, JustCreated>())
            {
                unsafe
                {
                    CollisionFilter collisionFilter = physicsCollider.ValueRO.ColliderPtr->GetCollisionFilter();

                    collisionFilter.CollidesWith = 1u << Setting.GetEnemyLayer(team.ValueRO.Color) | 1u << Setting.WorldLayer;

                    switch (physicsCollider.ValueRO.Value.Value.Type)
                    {
                        case ColliderType.Box:
                            physicsCollider.ValueRW.Value = BoxCollider.Create(((BoxCollider*)physicsCollider.ValueRO.ColliderPtr)->Geometry, collisionFilter);
                            break;
                    }

                    physicsCollider.ValueRO.ColliderPtr->SetCollisionResponse(CollisionResponsePolicy.CollideRaiseCollisionEvents);
                }
            }

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