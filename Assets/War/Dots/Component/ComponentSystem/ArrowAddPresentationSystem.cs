#if HYBRID_ARROW
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Pool;


namespace War.Dots.Component.ComponentSystem
{
    [UpdateInGroup(typeof(Group.JustSpawnedInitializeSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial class ArrowAddPresentationSystem : SystemBase
    {
        private static ObjectPool<GameObject> s_arrowPool;
        private static List<(Entity entity, PooledGameObject soldierViewComponent)> s_pooledGameObjectBuffer;


        protected override void OnCreate() => s_pooledGameObjectBuffer = new List<(Entity entity, PooledGameObject soldierViewComponent)>();

        protected override void OnStartRunning() =>
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

        protected override void OnDestroy()
        {
            s_arrowPool?.Dispose();
            s_arrowPool = null;
        }

        protected override void OnUpdate()
        {
            s_pooledGameObjectBuffer.Clear();
            
            EndInitializationEntityCommandBufferSystem ecbSystem = World.GetOrCreateSystemManaged<EndInitializationEntityCommandBufferSystem>();
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
                EntityManager.AddComponentData(entity, pooledGameObject);
            }
        }
    }
}
#endif