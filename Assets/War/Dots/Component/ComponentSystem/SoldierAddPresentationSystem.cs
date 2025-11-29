using System.Collections.Generic;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Pool;
using ZLinq;


namespace War.Dots.Component.ComponentSystem
{
    [UpdateInGroup(typeof(Group.JustSpawnedInitializeSystemGroup), OrderLast = true)]
    [RequireMatchingQueriesForUpdate]
    public partial class SoldierAddPresentationSystem : SystemBase
    {
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


        protected override void OnStartRunning() => InitPool();
        protected override void OnDestroy() => DisposePool();

        protected override void OnUpdate()
        {
            EndInitializationEntityCommandBufferSystem ecbSystem = World.GetOrCreateSystemManaged<EndInitializationEntityCommandBufferSystem>();
            EntityCommandBuffer ecb = ecbSystem.CreateCommandBuffer();

            foreach (
                var (soldier, team, localTransform, forward, agentData, acceleration, entity)
                in
                SystemAPI.Query<RefRO<Soldier>, RefRO<Team>, RefRO<LocalTransform>, RefRO<Forward>, RefRO<NavMeshAgentData>, RefRO<Acceleration>>()
                    .WithAll<Alive>()
                    .WithNone<UnityAnimator, UnityNavMeshAgent, UnityNavMeshObstacle>()
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

                if (!gameObject.TryGetComponent(out NavMeshAgent navMeshAgent))
                {
                    navMeshAgent = gameObject.GetComponentInChildren<NavMeshAgent>();
                }

                if (navMeshAgent)
                {
                    ecb.AddComponent(entity, new UnityNavMeshAgent { Agent = navMeshAgent });

                    navMeshAgent.updateRotation = true;
                    navMeshAgent.radius = agentData.ValueRO.Radius;
                    navMeshAgent.acceleration = acceleration.ValueRO.Max;
                }

                if (!gameObject.TryGetComponent(out NavMeshObstacle navMeshObstacle))
                {
                    navMeshObstacle = gameObject.GetComponentInChildren<NavMeshObstacle>();
                }

                if (navMeshObstacle)
                {
                    ecb.AddComponent(entity, new UnityNavMeshObstacle { Obstacle = navMeshObstacle });
                    
                    navMeshObstacle.radius = agentData.ValueRO.Radius * 0.5f;
                }
            }

            foreach (var (entity, pooledGameObjectComponent) in _pooledGameObjectBuffer)
            {
                EntityManager.AddComponentData(entity, pooledGameObjectComponent);
            }

            _pooledGameObjectBuffer.Clear();
        }
    }
}