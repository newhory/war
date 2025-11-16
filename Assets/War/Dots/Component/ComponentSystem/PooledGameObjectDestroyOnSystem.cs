using Unity.Entities;


namespace War.Dots.Component.ComponentSystem
{
    [UpdateInGroup(typeof(Group.DestroyOnSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct PooledGameObjectDestroyOnSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            double currentTime = SystemAPI.Time.ElapsedTime;

            foreach (
                (RefRO<DestroyOn> refDestroyOn, Entity entity)
                in
                SystemAPI.Query<RefRO<DestroyOn>>()
                    .WithAll<PooledGameObject>()
                    .WithEntityAccess())
            {
                if (currentTime >= refDestroyOn.ValueRO.DestroyTime &&
                    state.EntityManager.HasComponent<PooledGameObject>(entity))
                {
                    state.EntityManager.GetComponentObject<PooledGameObject>(entity).PooledObject.DisposeRef();
                }
            }
        }
    }
}