using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;


namespace War.Dots.Component.ComponentSystem
{
    [UpdateInGroup(typeof(Group.FirstUpdateGroup), OrderLast = true)]
    public partial class PlayerInputSystem : SystemBase
    {
        private static bool s_isStartBattle;
        private static bool s_isResetBattle;

        public static int SoldierCount { get; private set; }


        public static void StartBattle() => s_isStartBattle = true;
        public static void ResetBattle() => s_isResetBattle = true;

        private static Entity s_pointInput;


        public static void OnPointerPressStarted(EntityManager entityManager, float2 point, Ray ray)
        {
            entityManager.SetComponentEnabled<OnPointerPressStart>(s_pointInput, true);

            entityManager.SetComponentData(s_pointInput, new OnPointerDragStart { Point = point, Ray = ray });
            entityManager.SetComponentData(s_pointInput, new OnPointerDragging { Point = point, Ray = ray });
        }

        public static void OnPointerDragging(EntityManager entityManager, float2 point, Ray ray)
        {
            entityManager.SetComponentData(s_pointInput, new OnPointerDragging { Point = point, Ray = ray });
        }

        public static void OnPointerPressCanceled(EntityManager entityManager, float2 point, Ray ray)
        {
            entityManager.SetComponentEnabled<OnPointerPressStart>(s_pointInput, false);
            entityManager.SetComponentEnabled<OnPointerPressEnd>(s_pointInput, true);

            entityManager.SetComponentData(s_pointInput, new OnPointerDragEnd { Point = point, Ray = ray });
        }

        public static void OnPointerMove(EntityManager entityManager, float2 point, Ray ray)
        {
            entityManager.SetComponentData(s_pointInput, new OnPointerMove { Point = point, Ray = ray });
        }


        private EntityQuery _allArmyQuery;
        private EntityQuery _troopGroup;
        private EntityQuery _soldierGroup;
        private EntityQuery _pooledGameObjectQuery;


        protected override void OnCreate()
        {
            s_pointInput = Entity.Null;

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

        protected override void OnStartRunning()
        {
            if (SystemAPI.HasSingleton<PointInput>())
            {
                s_pointInput = SystemAPI.GetSingletonEntity<PointInput>();
            }
        }

        protected override void OnUpdate()
        {
            if (s_pointInput == Entity.Null && SystemAPI.HasSingleton<PointInput>())
            {
                s_pointInput = SystemAPI.GetSingletonEntity<PointInput>();
            }

            SoldierCount = _soldierGroup.CalculateEntityCount();

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

                EntityManager.AddComponent<BattleInput>(s_pointInput);
                EntityManager.RemoveComponent<SpawnInput>(s_pointInput);
            }

            if (s_isResetBattle)
            {
                s_isResetBattle = false;

                EntityManager.RemoveComponent<BattleInput>(s_pointInput);
                EntityManager.AddComponent<SpawnInput>(s_pointInput);

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