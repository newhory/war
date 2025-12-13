#if HYBRID_ARROW
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Pool;


namespace War.Dots.Component.ComponentSystem
{
    [UpdateInGroup(typeof(Group.AddPresentationSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct ArrowAddPresentationSystem : ISystem, ISystemStartStop
    {
        private static ObjectPool<GameObject> s_arrowPool;
        private static List<(Entity entity, PooledGameObject soldierViewComponent)> s_pooledGameObjectBuffer;


        public static void ResetPool()
        {
            DisposePool();
            InitPool();
        }

        private static void InitPool() =>
            s_arrowPool ??=
                new ObjectPool<GameObject>(
                    createFunc: () => Object.Instantiate(Setting.Instance.arrowRenderMeshPrefab),
                    actionOnGet: gameObject => gameObject.SetActive(true),
                    actionOnRelease: gameObject =>
                    {
                        if (gameObject)
                        {
                            gameObject.SetActive(false);
                        }
                    },
                    actionOnDestroy: Object.Destroy,
                    collectionCheck: true, // An Editor-only check that determines if an instance is returned back to the pool. Throws an exception if the instance is already in the pool.
                    defaultCapacity: 10);

        private static void DisposePool()
        {
            s_arrowPool?.Dispose();
            s_arrowPool = null;
        }


        public void OnCreate(ref SystemState state) => s_pooledGameObjectBuffer = new List<(Entity entity, PooledGameObject soldierViewComponent)>();

        public void OnDestroy(ref SystemState state) => DisposePool();

        public void OnUpdate(ref SystemState state)
        {
            s_pooledGameObjectBuffer.Clear();

            EndInitializationEntityCommandBufferSystem ecbSystem = state.World.GetOrCreateSystemManaged<EndInitializationEntityCommandBufferSystem>();
            EntityCommandBuffer ecb = ecbSystem.CreateCommandBuffer();

            foreach (
                (RefRO<Arrow> _, Entity entity)
                in
                SystemAPI.Query<RefRO<Arrow>>()
                    .WithNone<PooledGameObject>()
                    .WithEntityAccess())
            {
                s_pooledGameObjectBuffer.Add((entity, new PooledGameObject { PooledObject = s_arrowPool.Get(out GameObject gameObject) }));

                ecb.AddComponent(entity, new UnityTransform { Transform = gameObject.transform });
            }

            foreach (var (entity, pooledGameObject) in s_pooledGameObjectBuffer)
            {
                state.EntityManager.AddComponentData(entity, pooledGameObject);
            }
        }

        public void OnStartRunning(ref SystemState state) => InitPool();

        public void OnStopRunning(ref SystemState state)
        {
        }
    }
}
#endif