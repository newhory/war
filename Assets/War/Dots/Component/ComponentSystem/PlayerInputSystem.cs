using Unity.Burst;
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

        public static int SoldierCount { get; private set; }


        public static void StartBattle() => s_isStartBattle = true;
        public static void ResetBattle() => s_isResetBattle = true;

        private static Entity s_pointInput;


        public static void OnPressStarted(EntityManager entityManager, float2 point, Ray ray)
        {
            entityManager.SetComponentEnabled<OnPressStart>(s_pointInput, true);

            entityManager.SetComponentData(s_pointInput, new OnDragStart { Point = point, Ray = ray });
            entityManager.SetComponentData(s_pointInput, new OnDragging { Point = point, Ray = ray });
        }

        public static void OnDragging(EntityManager entityManager, float2 point, Ray ray)
        {
            entityManager.SetComponentData(s_pointInput, new OnDragging { Point = point, Ray = ray });
        }

        public static void OnPressCanceled(EntityManager entityManager, float2 point, Ray ray)
        {
            entityManager.SetComponentEnabled<OnPressStart>(s_pointInput, false);
            entityManager.SetComponentEnabled<DragStartPosition>(s_pointInput, false);
            entityManager.SetComponentEnabled<OnPressEnd>(s_pointInput, true);

            entityManager.SetComponentData(s_pointInput, new OnDragEnd { Point = point, Ray = ray });
        }


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

        protected override void OnStartRunning()
        {
            s_pointInput = SystemAPI.GetSingletonEntity<PointInput>();
        }

        protected override void OnUpdate()
        {
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

            RaycastInput raycastInput = new()
            {
                Filter = new CollisionFilter
                {
                    BelongsTo = 1u << Setting.ArrowLayer,
                    CollidesWith = 1u << Setting.BlueTeamLayer | 1u << Setting.RedTeamLayer | 1u << Setting.WorldLayer,
                }
            };

            float dragInputOffset = EntityManager.GetComponentData<PointInput>(s_pointInput).DragOffset;

            if (EntityManager.IsComponentEnabled<OnPressStart>(s_pointInput))
            {
                float3 dragStartPosition;

                if (EntityManager.IsComponentEnabled<DragStartPosition>(s_pointInput))
                {
                    OnDragging onDragging = EntityManager.GetComponentData<OnDragging>(s_pointInput);
                    
                    dragStartPosition = EntityManager.GetComponentData<DragStartPosition>(s_pointInput).Position;

                    raycastInput.Start = onDragging.Ray.Origin;
                    raycastInput.End = onDragging.Ray.Origin + onDragging.Ray.Displacement;

                    if (Cast(raycastInput, out RaycastHit onDraggingRaycastHit))
                    {
                        // todo : select target candidate troop
                        UnityEngine.Debug.DrawLine(dragStartPosition, onDraggingRaycastHit.Position, UnityEngine.Color.red);
                    }
                }
                else
                {
                    OnDragStart onDragStart = EntityManager.GetComponentData<OnDragStart>(s_pointInput);
                    OnDragging onDragging = EntityManager.GetComponentData<OnDragging>(s_pointInput);

                    if (math.distance(onDragStart.Point, onDragging.Point) >= dragInputOffset)
                    {
                        raycastInput.Start = onDragStart.Ray.Origin;
                        raycastInput.End = onDragStart.Ray.Origin + onDragStart.Ray.Displacement;

                        if (Cast(raycastInput, out RaycastHit onDragStartRaycastHit))
                        {
                            dragStartPosition = onDragStartRaycastHit.Position;

                            EntityManager.SetComponentEnabled<DragStartPosition>(s_pointInput, true);
                            EntityManager.SetComponentData(s_pointInput, new DragStartPosition { Position = dragStartPosition });

                            // todo : select troop
                            UnityEngine.Debug.DrawLine(dragStartPosition, dragStartPosition + math.up() * 2.5f, UnityEngine.Color.azure, 5f);
                        }
                    }
                }
            }

            if (EntityManager.IsComponentEnabled<OnPressEnd>(s_pointInput))
            {
                EntityManager.SetComponentEnabled<OnPressEnd>(s_pointInput, false);

                OnDragEnd onDragEnd = EntityManager.GetComponentData<OnDragEnd>(s_pointInput);

                raycastInput.Start = onDragEnd.Ray.Origin;
                raycastInput.End = onDragEnd.Ray.Origin + onDragEnd.Ray.Displacement;

                if (Cast(raycastInput, out RaycastHit onDragEndRaycastHit))
                {
                    // todo : select target or troop
                    UnityEngine.Debug.DrawLine(onDragEndRaycastHit.Position, onDragEndRaycastHit.Position + math.up() * 2.5f, UnityEngine.Color.blue, 5f);
                }
            }
        }

        private bool Cast(RaycastInput raycastInput, out RaycastHit raycastHit)
        {
            UnityEngine.Debug.DrawLine(raycastInput.Start, raycastInput.End, UnityEngine.Color.chocolate);

            PhysicsWorldSingleton physicsWorldSingleton = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
            CollisionWorld collisionWorld = physicsWorldSingleton.PhysicsWorld.CollisionWorld;

            return collisionWorld.CastRay(raycastInput, out raycastHit);
        }
    }
}