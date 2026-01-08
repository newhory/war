using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace War.Game.Systems
{
    [UpdateInGroup(typeof(Group.InputUpdateGroup), OrderFirst = true)]
    public partial class PlayerInputSystem : SystemBase
    {
        private static bool s_isStartBattle;
        private static bool s_isResetBattle;

        private static Entity s_pointInput;


        public static int SoldierCount { get; private set; }


        public static void StartBattle() => s_isStartBattle = true;
        public static void ResetBattle() => s_isResetBattle = true;


        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void InitializeOnLoad()
        {
            s_isStartBattle = s_isResetBattle = false;
            s_pointInput = Entity.Null;
        }


        public static void OnPointerPressStarted(EntityManager entityManager, float3 positionOnGround, float2 inputPoint, Unity.Physics.Ray ray)
        {
            if (s_pointInput == Entity.Null)
            {
                return;
            }

            entityManager.SetComponentEnabled<OnPointerPressStart>(s_pointInput, true);

            entityManager.SetComponentData(s_pointInput, new OnPointerDragStart { PositionOnGround = positionOnGround, Point = inputPoint, Ray = ray });
            entityManager.SetComponentData(s_pointInput, new OnPointerDragging { PositionOnGround = positionOnGround, Point = inputPoint, Ray = ray });
        }

        public static void OnPointerDragging(EntityManager entityManager, float3 positionOnGround, float2 inputPoint, Unity.Physics.Ray ray)
        {
            if (s_pointInput == Entity.Null)
            {
                return;
            }

            entityManager.SetComponentData(s_pointInput, new OnPointerDragging { PositionOnGround = positionOnGround, Point = inputPoint, Ray = ray });
        }

        public static void OnPointerPressCanceled(EntityManager entityManager, float3 positionOnGround)
        {
            if (s_pointInput == Entity.Null)
            {
                return;
            }

            entityManager.SetComponentEnabled<OnPointerPressStart>(s_pointInput, false);
            entityManager.SetComponentEnabled<OnPointerPressEnd>(s_pointInput, true);

            entityManager.SetComponentData(s_pointInput, new OnPointerDragEnd { PositionOnGround = positionOnGround });
        }

        public static void OnPointerMove(EntityManager entityManager, float3 positionOnGround)
        {
            if (s_pointInput == Entity.Null)
            {
                return;
            }

            entityManager.SetComponentData(s_pointInput, new OnPointerMove { PositionOnGround = positionOnGround });
        }


        private EntityQuery _allArmyQuery;
        private EntityQuery _troopGroup;
        private EntityQuery _soldierGroup;
        private EntityQuery _pooledGameObjectQuery;
        private EntityQuery _childObjectQuery;


        protected override void OnCreate()
        {
            _allArmyQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<Troop>()
                    .Build();

            _troopGroup =
                SystemAPI.QueryBuilder()
                    .WithAll<Troop, Alive, TroopEntity>()
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

            _childObjectQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<Parent>()
                    .Build();
        }

        protected override void OnUpdate()
        {
            if (s_pointInput == Entity.Null)
            {
                if (SystemAPI.HasSingleton<PointInput>())
                {
                    s_pointInput = SystemAPI.GetSingletonEntity<PointInput>();
                }
                else
                {
                    EntityCommandBuffer ecb = new(Allocator.Temp);

                    foreach (var (pointInput, entity) in SystemAPI.Query<RefRO<PointInput>>().WithEntityAccess())
                    {
                        if (s_pointInput == Entity.Null)
                        {
                            s_pointInput = World.EntityManager.CreateSingleton(pointInput.ValueRO, "Player Input");

                            ecb.AddComponent(s_pointInput, new OnPointerPressStart());
                            ecb.AddComponent(s_pointInput, new OnPointerPressEnd());
                            ecb.AddComponent(s_pointInput, new OnPointerMove());

                            ecb.AddComponent(s_pointInput, new OnPointerDragStart());
                            ecb.AddComponent(s_pointInput, new OnPointerDragging());
                            ecb.AddComponent(s_pointInput, new OnPointerDragEnd());

                            ecb.AddComponent(s_pointInput, new DragStartWorldPosition());
                            ecb.AddComponent(s_pointInput, new DraggingWorldPosition());
                            ecb.AddComponent(s_pointInput, new DragEndWorldPosition());

                            ecb.SetComponentEnabled<OnPointerPressStart>(s_pointInput, false);
                            ecb.SetComponentEnabled<OnPointerPressEnd>(s_pointInput, false);

                            ecb.SetComponentEnabled<DragStartWorldPosition>(s_pointInput, false);
                            ecb.SetComponentEnabled<DraggingWorldPosition>(s_pointInput, false);
                            ecb.SetComponentEnabled<DragEndWorldPosition>(s_pointInput, false);

                            ecb.AddComponent(s_pointInput, new SpawnInput());
                        }

                        ecb.DestroyEntity(entity);
                    }

                    ecb.Playback(EntityManager);
                    ecb.Dispose();
                }
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
                    (RefRW<LocalTransform> refLocalTransform, Entity entity)
                    in
                    SystemAPI.Query<RefRW<LocalTransform>>().WithAll<TroopEntity>().WithDisabled<TroopAISearchTarget>().WithEntityAccess())
                {
                    ecb.SetComponentEnabled<TroopFormationReset>(entity, true);
                    ecb.SetComponent(entity, new TroopFormationReset { TroopPosition = destination });

                    ecb.SetComponentEnabled<TroopAISearchTarget>(entity, true);
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

                BattleInputSystem.Reset();

                EntityManager.DestroyEntity(_childObjectQuery);
                EntityManager.DestroyEntity(_allArmyQuery);

                SoldierAddPresentationSystem.ResetPool();
#if DO_NOT_USE_ENTITIES_GRAPHICS
                SoldierAddPresentationForNavigationSystem.ResetPool();
                ArrowAddPresentationSystem.ResetPool();
#endif
                Resources.UnloadUnusedAssets();
                System.GC.Collect();
            }
        }
    }
}