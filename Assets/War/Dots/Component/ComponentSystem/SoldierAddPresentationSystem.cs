using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Pool;
using ZLinq;


namespace War.Dots.Component.ComponentSystem
{
    [UpdateInGroup(typeof(Group.ViewSystemGroup))]
    public partial class SoldierAddPresentationSystem : SystemBase
    {
        private Dictionary<SoldierType, ObjectPool<GameObject>> _blueTeamSoldierViewPool;
        private Dictionary<SoldierType, ObjectPool<GameObject>> _redTeamSoldierViewPool;

        private readonly List<(Entity entity, PooledGameObject soldierViewComponent)> _pooledGameObjectBuffer = new();


        private static ObjectPool<GameObject> CreatePool(GameObject prefab) =>
            new(
                createFunc: () => Object.Instantiate(prefab),
                actionOnGet: gameObject => gameObject.SetActive(true),
                actionOnRelease: gameObject => gameObject.SetActive(false),
                actionOnDestroy: Object.Destroy,
                collectionCheck: true, // An Editor-only check that determines if an instance is returned back to the pool. Throws an exception if the instance is already in the pool.
                defaultCapacity: 10);


        protected override void OnStartRunning()
        {
            _blueTeamSoldierViewPool ??= new Dictionary<SoldierType, ObjectPool<GameObject>>
            {
                { SoldierType.Archer, CreatePool(Setting.Instance.archer.blueTeamPrefab) },
                { SoldierType.Cavalry, CreatePool(Setting.Instance.cavalry.blueTeamPrefab) },
                { SoldierType.Shield, CreatePool(Setting.Instance.shield.blueTeamPrefab) },
                { SoldierType.Spear, CreatePool(Setting.Instance.spear.blueTeamPrefab) }
            };

            _redTeamSoldierViewPool ??= new Dictionary<SoldierType, ObjectPool<GameObject>>
            {
                { SoldierType.Archer, CreatePool(Setting.Instance.archer.redTeamPrefab) },
                { SoldierType.Cavalry, CreatePool(Setting.Instance.cavalry.redTeamPrefab) },
                { SoldierType.Shield, CreatePool(Setting.Instance.shield.redTeamPrefab) },
                { SoldierType.Spear, CreatePool(Setting.Instance.spear.redTeamPrefab) }
            };
        }

        protected override void OnDestroy()
        {
            if (_blueTeamSoldierViewPool is not null)
            {
                foreach (ObjectPool<GameObject> objectPool in _blueTeamSoldierViewPool.AsValueEnumerable().Select(pair => pair.Value))
                {
                    objectPool.Dispose();
                }
            
                _blueTeamSoldierViewPool.Clear();
                _blueTeamSoldierViewPool = null;
            }

            if (_redTeamSoldierViewPool is not null)
            {
                foreach (ObjectPool<GameObject> objectPool in _redTeamSoldierViewPool.AsValueEnumerable().Select(pair => pair.Value))
                {
                    objectPool.Dispose();
                }
            
                _redTeamSoldierViewPool.Clear();
                _redTeamSoldierViewPool = null;
            }
        }

        protected override void OnUpdate()
        {
            EntityCommandBuffer ecbSetSoldierView = new(Allocator.TempJob);

            foreach (
                var (soldier, team, localTransform, forward, entity)
                in
                SystemAPI.Query<RefRO<Soldier>, RefRO<Team>, RefRO<LocalTransform>, RefRO<Forward>>()
                    .WithAll<Alive>()
                    .WithNone<UnityAnimator, UnityNavMeshAgent, UnityNavMeshObstacle>()
                    .WithEntityAccess())
            {
                Dictionary<SoldierType, ObjectPool<GameObject>> soldierViewPool =
                    team.ValueRO.Color switch
                    {
                        TeamColor.Blue => _blueTeamSoldierViewPool,
                        TeamColor.Red => _redTeamSoldierViewPool,
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
                    ecbSetSoldierView.AddComponent(entity, new UnityAnimator { Animator = animator });
                }

                if (!gameObject.TryGetComponent(out NavMeshAgent navMeshAgent))
                {
                    navMeshAgent = gameObject.GetComponentInChildren<NavMeshAgent>();
                }

                if (navMeshAgent)
                {
                    ecbSetSoldierView.AddComponent(entity, new UnityNavMeshAgent { Agent = navMeshAgent });

                    navMeshAgent.updateRotation = true;
                }

                if (!gameObject.TryGetComponent(out NavMeshObstacle navMeshObstacle))
                {
                    navMeshObstacle = gameObject.GetComponentInChildren<NavMeshObstacle>();
                }

                if (navMeshObstacle)
                {
                    ecbSetSoldierView.AddComponent(entity, new UnityNavMeshObstacle { Obstacle = navMeshObstacle });
                }
            }

            ecbSetSoldierView.Playback(EntityManager);
            ecbSetSoldierView.Dispose();

            foreach (var (entity, pooledGameObjectComponent) in _pooledGameObjectBuffer)
            {
                EntityManager.AddComponentData(entity, pooledGameObjectComponent);
            }

            _pooledGameObjectBuffer.Clear();
        }
    }
}