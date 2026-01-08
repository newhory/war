using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;
#if DO_NOT_USE_ENTITIES_GRAPHICS
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using ZLinq;
#endif

namespace War.Game.Systems
{
    using Navigation;
#if DO_NOT_USE_ENTITIES_GRAPHICS
    using VAT;
#endif
    [UpdateInGroup(typeof(Group.AddPresentationSystemGroup), OrderLast = true)]
    [RequireMatchingQueriesForUpdate]
    public partial
#if DO_NOT_USE_ENTITIES_GRAPHICS
        class
#else
        struct
#endif
        SoldierAddPresentationForNavigationSystem :
#if DO_NOT_USE_ENTITIES_GRAPHICS
        SystemBase
#else
        ISystem
#endif
    {
        private partial struct AddComponentJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter EntityCommandBuffer;

            [ReadOnly] public BlobAssetReference<FlowFieldBlobRoot> FlowFieldBlobRootBlob;


            private void Execute([EntityIndexInQuery] int index, Entity entity, in LocalTransform localTransform, in Velocity velocity, in SoldierDestination soldierDestination, in NavMeshAgentData navMeshAgentData, in MoveSpeed moveSpeed)
            {
                EntityCommandBuffer.AddComponent(index, entity, new UnitPosition { Value = localTransform.Position });
                EntityCommandBuffer.AddComponent(index, entity, new UnitVelocity { Value = velocity.Value });
                EntityCommandBuffer.AddComponent(index, entity, new UnitDestination { Value = soldierDestination.Position, FlowFieldId = -1 });
                EntityCommandBuffer.AddComponent(index, entity, new UnitRadius { Value = navMeshAgentData.Radius });
                EntityCommandBuffer.AddComponent(index, entity, new UnitMaxSpeed { Value = moveSpeed.CurrentMax });
                EntityCommandBuffer.AddComponent(index, entity, new StandingObstacle());
                EntityCommandBuffer.AddComponent(index, entity, new UnitPreferredSide());
                EntityCommandBuffer.AddComponent(index, entity, new BlockAhead());
                EntityCommandBuffer.AddComponent(index, entity, new StandingCooldown());
                EntityCommandBuffer.AddComponent(index, entity, new FlowFieldBlobReference { BlobAssetReference = FlowFieldBlobRootBlob });
            }
        }
#if DO_NOT_USE_ENTITIES_GRAPHICS
        private static Dictionary<SoldierType, ObjectPool<GameObject>> s_blueTeamSoldierViewPool;
        private static Dictionary<SoldierType, ObjectPool<GameObject>> s_redTeamSoldierViewPool;


        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Initialize()
        {
            s_blueTeamSoldierViewPool = null;
            s_redTeamSoldierViewPool = null;
        }

        private readonly List<(Entity entity, PooledGameObject soldierViewComponent)> _pooledGameObjectBuffer = new();


        public static void ResetPool()
        {
            DisposePool();
            InitPool();
        }

        private static void InitPool()
        {
            s_blueTeamSoldierViewPool ??= new Dictionary<SoldierType, ObjectPool<GameObject>>
            {
                { SoldierType.Archer, CreatePool(Setting.Instance.archer.blueTeamPrefab) },
                { SoldierType.Cavalry, CreatePool(Setting.Instance.cavalry.blueTeamPrefab) },
                { SoldierType.Shield, CreatePool(Setting.Instance.shield.blueTeamPrefab) },
                { SoldierType.Spear, CreatePool(Setting.Instance.spear.blueTeamPrefab) }
            };

            s_redTeamSoldierViewPool ??= new Dictionary<SoldierType, ObjectPool<GameObject>>
            {
                { SoldierType.Archer, CreatePool(Setting.Instance.archer.redTeamPrefab) },
                { SoldierType.Cavalry, CreatePool(Setting.Instance.cavalry.redTeamPrefab) },
                { SoldierType.Shield, CreatePool(Setting.Instance.shield.redTeamPrefab) },
                { SoldierType.Spear, CreatePool(Setting.Instance.spear.redTeamPrefab) }
            };
        }

        private static void DisposePool()
        {
            if (s_blueTeamSoldierViewPool is not null)
            {
                foreach (ObjectPool<GameObject> objectPool in s_blueTeamSoldierViewPool.AsValueEnumerable().Select(pair => pair.Value))
                {
                    objectPool.Dispose();
                }

                s_blueTeamSoldierViewPool.Clear();
                s_blueTeamSoldierViewPool = null;
            }

            if (s_redTeamSoldierViewPool is not null)
            {
                foreach (ObjectPool<GameObject> objectPool in s_redTeamSoldierViewPool.AsValueEnumerable().Select(pair => pair.Value))
                {
                    objectPool.Dispose();
                }

                s_redTeamSoldierViewPool.Clear();
                s_redTeamSoldierViewPool = null;
            }
        }

        private static ObjectPool<GameObject> CreatePool(GameObject prefab) =>
            new(
                createFunc: () => Object.Instantiate(prefab),
                actionOnGet: gameObject => gameObject.SetActive(true),
                actionOnRelease: gameObject => gameObject.SetActive(false),
                actionOnDestroy: Object.Destroy,
                collectionCheck: true, // An Editor-only check that determines if an instance is returned back to the pool. Throws an exception if the instance is already in the pool.
                defaultCapacity: 10);
#endif

        private EntityQuery _soldierForUnitQuery;

#if DO_NOT_USE_ENTITIES_GRAPHICS
        protected override
#else
        public
#endif
            void OnCreate(
#if !DO_NOT_USE_ENTITIES_GRAPHICS
                ref SystemState state
#endif
            )
        {
            _soldierForUnitQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<Soldier, Alive, LocalTransform, Velocity, SoldierDestination, NavMeshAgentData, MoveSpeed>()
                    .WithAll<NavigationAPI>()
                    .WithNone<UnitPosition, UnitVelocity, UnitDestination>()
                    .WithNone<UnitRadius, UnitMaxSpeed, StandingObstacle>()
                    .WithNone<UnitPreferredSide, BlockAhead, StandingCooldown>()
                    .Build();
        }

#if DO_NOT_USE_ENTITIES_GRAPHICS
        protected override
#else
        public
#endif
            void OnUpdate(
#if !DO_NOT_USE_ENTITIES_GRAPHICS
                ref SystemState state
#endif
            )
        {
            EndInitializationEntityCommandBufferSystem ecbSystem =
#if !DO_NOT_USE_ENTITIES_GRAPHICS
                state.
#endif
                    World.GetOrCreateSystemManaged<EndInitializationEntityCommandBufferSystem>();

            EntityCommandBuffer ecb = ecbSystem.CreateCommandBuffer();
#if DO_NOT_USE_ENTITIES_GRAPHICS
            foreach (
                var (soldier, team, localTransform, forward, agentData, acceleration, entity)
                in
                SystemAPI.Query<RefRO<Soldier>, RefRO<Team>, RefRO<LocalTransform>, RefRO<Forward>, RefRO<NavMeshAgentData>, RefRO<Acceleration>>()
                    .WithAll<Alive>()
                    .WithNone<UnityTransform, UnityAnimator>()
                    .WithSharedComponentFilter(new NavigationAPI { Type = NavigationType.Custom })
                    .WithEntityAccess())
            {
                Dictionary<SoldierType, ObjectPool<GameObject>> soldierViewPool =
                    team.ValueRO.Color switch
                    {
                        TeamColor.Blue => s_blueTeamSoldierViewPool,
                        TeamColor.Red => s_redTeamSoldierViewPool,
                        _ => null
                    };

                if (soldierViewPool is null ||
                    !soldierViewPool.TryGetValue(soldier.ValueRO.Type, out ObjectPool<GameObject> pool))
                {
                    continue;
                }

                PooledObject<GameObject> pooled = pool.Get(out GameObject gameObject);
                if (!gameObject)
                {
                    continue;
                }

                Transform pooledTransform = gameObject.transform;
                pooledTransform.position = localTransform.ValueRO.Position;
                pooledTransform.forward = forward.ValueRO.Value;

                _pooledGameObjectBuffer.Add((entity, new PooledGameObject { PooledObject = pooled }));

                if (!gameObject.TryGetComponent(out Animator animator))
                {
                    animator = gameObject.GetComponentInChildren<Animator>();
                }

                if (animator)
                {
                    ecb.AddComponent(entity, new UnityAnimator { Animator = animator });
                }

                if (gameObject.TryGetComponent(out VertexAnimationBehaviour vertexAnimationBehaviour))
                {
                    ecb.AddComponent(entity, new UnityVertexAnimationBehaviour { Behaviour = vertexAnimationBehaviour });
                }

                ecb.AddComponent(entity, new UnityTransform { Transform = pooledTransform });
            }

            foreach (var (entity, pooledGameObjectComponent) in _pooledGameObjectBuffer)
            {
                EntityManager.AddComponentData(entity, pooledGameObjectComponent);
            }

            _pooledGameObjectBuffer.Clear();
#endif
            JobHandle dependency =
#if !DO_NOT_USE_ENTITIES_GRAPHICS
                state.
#endif
                    Dependency;

            _soldierForUnitQuery.SetSharedComponentFilter(new NavigationAPI { Type = NavigationType.Custom });

            dependency =
                new AddComponentJob
                    {
                        EntityCommandBuffer = ecb.AsParallelWriter(),

                        FlowFieldBlobRootBlob = FlowFieldProvider.FlowFieldFlowBlobAssetReference,
                    }
                    .ScheduleParallel(_soldierForUnitQuery, dependency);
            ecbSystem.AddJobHandleForProducer(dependency);

#if !DO_NOT_USE_ENTITIES_GRAPHICS
            state.
#endif
                Dependency = dependency;
        }
#if !DO_NOT_USE_ENTITIES_GRAPHICS
        public void OnDestroy(ref SystemState state)
        {
        }
#endif

#if DO_NOT_USE_ENTITIES_GRAPHICS
        protected override void OnStartRunning() => InitPool();
        protected override void OnDestroy() => DisposePool();
#endif
    }
}