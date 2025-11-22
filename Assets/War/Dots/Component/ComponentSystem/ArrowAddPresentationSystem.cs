#if HYBRID_ARROW
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine.Pool;


namespace War.Dots.Component.ComponentSystem
{
    [UpdateInGroup(typeof(Group.AddPresentationSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial class ArrowAddPresentationSystem : SystemBase
    {
        private static ObjectPool<UnityEngine.GameObject> s_arrowPool;
        private static List<(Entity entity, PooledGameObject soldierViewComponent)> s_pooledGameObjectBuffer;

        protected override void OnStartRunning()
        {
            s_arrowPool ??=
                new ObjectPool<UnityEngine.GameObject>(
                    createFunc: () => UnityEngine.Object.Instantiate(Setting.Instance.arrowRenderMeshPrefab),
                    actionOnGet: gameObject => gameObject.SetActive(true),
                    actionOnRelease: gameObject =>
                    {
                        if (gameObject)
                        {
                            gameObject.SetActive(false);
                        }
                    },
                    actionOnDestroy: UnityEngine.Object.Destroy,
                    collectionCheck: true, // An Editor-only check that determines if an instance is returned back to the pool. Throws an exception if the instance is already in the pool.
                    defaultCapacity: 10);

            s_pooledGameObjectBuffer = new List<(Entity entity, PooledGameObject soldierViewComponent)>();
        }

        protected override void OnStopRunning()
        {
            s_arrowPool?.Dispose();
            s_arrowPool = null;
        }
        
        protected override void OnUpdate()
        {
            s_pooledGameObjectBuffer.Clear();

            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (
                (RefRO<Arrow> _, Entity entity)
                in
                SystemAPI.Query<RefRO<Arrow>>()
                    .WithNone<UnityTransform>()
                    .WithEntityAccess())
            {
                PooledObject<UnityEngine.GameObject> pooled = s_arrowPool.Get(out UnityEngine.GameObject gameObject);

                gameObject.SetActive(true);

                s_pooledGameObjectBuffer.Add((entity, new PooledGameObject { PooledObject = pooled }));

                ecb.AddComponent(entity, new UnityTransform { Transform = gameObject.transform });
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();

            foreach (var (entity, pooledGameObject) in s_pooledGameObjectBuffer)
            {
                EntityManager.AddComponentData(entity, pooledGameObject);
            }
        }
    }
}
#endif