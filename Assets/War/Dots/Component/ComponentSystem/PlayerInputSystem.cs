using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;


namespace War.Dots.Component.ComponentSystem
{
    public partial class PlayerInputSystem : SystemBase
    {
        [BurstCompile]
        private partial struct DestroyEntityJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter EntityCommandBuffer;


            public void Execute([EntityIndexInQuery] int index, Entity entity)
            {
                EntityCommandBuffer.DestroyEntity(index, entity);
            }
        }


        private static bool s_isStartBattle;
        private static bool s_isResetBattle;


        public static void StartBattle() => s_isStartBattle = true;
        public static void ResetBattle() => s_isResetBattle = true;


        private EntityQuery _allArmyQuery;
        private EntityQuery _troopGroup;
        private EntityQuery _soldierGroup;
        private EntityQuery _pooledGameObjectQuery;


        protected override void OnCreate()
        {
            _allArmyQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<Troop>()
                    .Build();

            _troopGroup =
                SystemAPI.QueryBuilder()
                    .WithAll<Troop, TroopEntity>()
                    .Build();

            _soldierGroup =
                SystemAPI.QueryBuilder()
                    .WithAll<Troop, Soldier>()
                    .Build();

            _pooledGameObjectQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<PooledGameObject>()
                    .WithOptions(EntityQueryOptions.IncludeDisabledEntities)
                    .Build();
        }

        protected override void OnUpdate()
        {
            if (s_isStartBattle)
            {
                s_isStartBattle = false;

                Entity fieldCameraEntity = SystemAPI.GetSingletonEntity<FieldCamera>();
                EntityManager.SetComponentEnabled<UseFieldCamera>(fieldCameraEntity, true);

                float3 destination = float3.zero;

                EntityCommandBuffer ecb = new(Allocator.Temp);
                foreach (
                    (RefRW<Destination> refTargetDestination, RefRW<Forward> refForward, RefRO<LocalTransform> refLocalTransform, Entity entity)
                    in
                    SystemAPI.Query<RefRW<Destination>, RefRW<Forward>, RefRO<LocalTransform>>().WithAll<TroopEntity>().WithDisabled<AISearchTarget>().WithEntityAccess())
                {
                    refTargetDestination.ValueRW.Position = destination;
                    refForward.ValueRW.Value.xz = math.normalize(destination.xz - refLocalTransform.ValueRO.Position.xz);

                    ecb.SetComponentEnabled<AISearchTarget>(entity, true);
                }

                ecb.Playback(EntityManager);
                ecb.Dispose();
            }

            if (s_isResetBattle)
            {
                s_isResetBattle = false;

                Entity fieldCameraEntity = SystemAPI.GetSingletonEntity<FieldCamera>();
                EntityManager.SetComponentEnabled<ResetFieldCamera>(fieldCameraEntity, true);
                EntityManager.SetComponentEnabled<UseFieldCamera>(fieldCameraEntity, false);

                NativeArray<Entity> pooledGameObjectEntities = _pooledGameObjectQuery.ToEntityArray(Allocator.Temp);
                foreach (Entity pooledGameObjectEntity in pooledGameObjectEntities)
                {
                    EntityManager.GetComponentObject<PooledGameObject>(pooledGameObjectEntity).PooledObject.DisposeRef();
                }

                pooledGameObjectEntities.Dispose();

                EntityManager.DestroyEntity(_allArmyQuery);
            }
        }
    }
}