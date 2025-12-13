using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Pool;
using ZLinq;


namespace War.Dots.Component.ComponentSystem
{
    using Navigation;


    [UpdateInGroup(typeof(Group.AddPresentationSystemGroup), OrderLast = true)]
    [RequireMatchingQueriesForUpdate]
    public partial class SoldierAddPresentationForNavigationSystem : SystemBase
    {
        private partial struct AddComponentJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter EntityCommandBuffer;

            [ReadOnly] public BlobAssetReference<FlowFieldBlobRoot> FlowFieldBlobRootBlob;


            private void Execute([EntityIndexInQuery] int index, Entity entity, in LocalTransform localTransform, in Velocity velocity, in Destination destination, in NavMeshAgentData navMeshAgentData, in MoveSpeed moveSpeed)
            {
                EntityCommandBuffer.AddComponent(index, entity, new UnitPosition { Value = localTransform.Position });
                EntityCommandBuffer.AddComponent(index, entity, new UnitVelocity { Value = velocity.Value });
                EntityCommandBuffer.AddComponent(index, entity, new UnitDestination { Value = destination.Position, FlowFieldId = -1 });
                EntityCommandBuffer.AddComponent(index, entity, new UnitRadius { Value = navMeshAgentData.Radius });
                EntityCommandBuffer.AddComponent(index, entity, new UnitMaxSpeed { Value = moveSpeed.CurrentMax });
                EntityCommandBuffer.AddComponent(index, entity, new StandingObstacle());
                EntityCommandBuffer.AddComponent(index, entity, new UnitPreferredSide());
                EntityCommandBuffer.AddComponent(index, entity, new BlockAhead());
                EntityCommandBuffer.AddComponent(index, entity, new StandingCooldown());
                EntityCommandBuffer.AddComponent(index, entity, new FlowFieldBlobReference { BlobAssetReference = FlowFieldBlobRootBlob });
            }
        }

        private static Dictionary<SoldierType, ObjectPool<GameObject>> s_blueTeamSoldierViewPool;
        private static Dictionary<SoldierType, ObjectPool<GameObject>> s_redTeamSoldierViewPool;

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


        private EntityQuery _soldierForUnitQuery;


        protected override void OnCreate()
        {
            _soldierForUnitQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<Soldier, Alive, LocalTransform, Velocity, Destination, NavMeshAgentData, MoveSpeed>()
                    .WithAll<NavigationAPI>()
                    .WithNone<UnitPosition, UnitVelocity, UnitDestination>()
                    .WithNone<UnitRadius, UnitMaxSpeed, StandingObstacle>()
                    .WithNone<UnitPreferredSide, BlockAhead, StandingCooldown>()
                    .Build();
        }

        protected override void OnUpdate()
        {
            EndInitializationEntityCommandBufferSystem ecbSystem = World.GetOrCreateSystemManaged<EndInitializationEntityCommandBufferSystem>();
            EntityCommandBuffer ecb = ecbSystem.CreateCommandBuffer();

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

                ecb.AddComponent(entity, new UnityTransform { Transform = pooledTransform });
            }

            foreach (var (entity, pooledGameObjectComponent) in _pooledGameObjectBuffer)
            {
                EntityManager.AddComponentData(entity, pooledGameObjectComponent);
            }

            _pooledGameObjectBuffer.Clear();

            JobHandle dependency = Dependency;

            _soldierForUnitQuery.SetSharedComponentFilter(new NavigationAPI { Type = NavigationType.Custom });

            ecb = ecbSystem.CreateCommandBuffer();
            dependency =
                new AddComponentJob
                    {
                        EntityCommandBuffer = ecb.AsParallelWriter(),
                        
                        FlowFieldBlobRootBlob = FlowFieldProvider.FlowFieldFlowBlobAssetReference,
                    }
                    .ScheduleParallel(_soldierForUnitQuery, dependency);
            ecbSystem.AddJobHandleForProducer(dependency);

            Dependency = dependency;
        }

        protected override void OnStartRunning() => InitPool();
        protected override void OnDestroy() => DisposePool();
    }
}