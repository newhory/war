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
            entityManager.SetComponentEnabled<OnPressEnd>(s_pointInput, true);

            entityManager.SetComponentData(s_pointInput, new OnDragEnd { Point = point, Ray = ray });
        }


        private EntityQuery _allArmyQuery;
        private EntityQuery _troopGroup;
        private EntityQuery _soldierGroup;
        private EntityQuery _pooledGameObjectQuery;

        private Entity _currentSelectedEntity;
        private TeamColor _currentSelectedTeamColor;

        private Entity _currentTargetCandidateEntity;


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
                    if (_currentTargetCandidateEntity != Entity.Null)
                    {
                        EntityManager.SetComponentEnabled<TroopSelected>(_currentTargetCandidateEntity, false);

                        _currentTargetCandidateEntity = Entity.Null;
                    }

                    OnDragging onDragging = EntityManager.GetComponentData<OnDragging>(s_pointInput);

                    dragStartPosition = EntityManager.GetComponentData<DragStartPosition>(s_pointInput).Position;

                    raycastInput.Start = onDragging.Ray.Origin;
                    raycastInput.End = onDragging.Ray.Origin + onDragging.Ray.Displacement;

                    if (Cast(raycastInput, out RaycastHit onDraggingRaycastHit))
                    {
                        (Entity currentPickedTroop, TeamColor teamColor) = GetPickedTroop(onDraggingRaycastHit.Position);

                        if (currentPickedTroop != Entity.Null && teamColor != _currentSelectedTeamColor)
                        {
                            EntityManager.SetComponentEnabled<TroopSelected>(currentPickedTroop, true);

                            _currentTargetCandidateEntity = currentPickedTroop;
                        }
#if UNITY_EDITOR
                        UnityEngine.Debug.DrawLine(dragStartPosition, onDraggingRaycastHit.Position, UnityEngine.Color.red);
#endif
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

                            (Entity currentPickedTroop, TeamColor teamColor) = GetPickedTroop(dragStartPosition);

                            if (currentPickedTroop != Entity.Null)
                            {
                                EntityManager.SetComponentEnabled<TroopSelected>(currentPickedTroop, true);

                                _currentSelectedEntity = currentPickedTroop;
                                _currentSelectedTeamColor = teamColor;
                            }
#if UNITY_EDITOR
                            UnityEngine.Debug.DrawLine(dragStartPosition, dragStartPosition + math.up() * 2.5f, UnityEngine.Color.azure, 5f);
#endif
                        }
                    }
                }
            }

            if (EntityManager.IsComponentEnabled<OnPressEnd>(s_pointInput))
            {
                if (_currentTargetCandidateEntity != Entity.Null)
                {
                    EntityManager.SetComponentEnabled<TroopSelected>(_currentTargetCandidateEntity, false);

                    _currentTargetCandidateEntity = Entity.Null;
                }

                EntityManager.SetComponentEnabled<OnPressEnd>(s_pointInput, false);

                OnDragEnd onDragEnd = EntityManager.GetComponentData<OnDragEnd>(s_pointInput);

                raycastInput.Start = onDragEnd.Ray.Origin;
                raycastInput.End = onDragEnd.Ray.Origin + onDragEnd.Ray.Displacement;

                if (Cast(raycastInput, out RaycastHit onDragEndRaycastHit))
                {
                    (Entity currentPickedTroop, TeamColor teamColor) = GetPickedTroop(onDragEndRaycastHit.Position);

                    if (_currentSelectedEntity != currentPickedTroop)
                    {
                        if (_currentSelectedEntity == Entity.Null) // currentPickedTroop is not Null
                        {
                            EntityManager.SetComponentEnabled<TroopSelected>(currentPickedTroop, true);

                            _currentSelectedEntity = currentPickedTroop;
                            _currentSelectedTeamColor = teamColor;
                        }
                        else
                        {
                            if (currentPickedTroop == Entity.Null) // _currentSelectedEntity is not Null
                            {
                                if (EntityManager.IsComponentEnabled<DragStartPosition>(s_pointInput))
                                {
                                    EntityManager.SetComponentData(_currentSelectedEntity, new Destination { Position = onDragEndRaycastHit.Position });

                                    EntityManager.SetComponentEnabled<AICheckTargetValid>(_currentSelectedEntity, false);
                                    EntityManager.SetComponentEnabled<AISearchTarget>(_currentSelectedEntity, false);
                                    EntityManager.SetComponentEnabled<StateMoveInFormation>(_currentSelectedEntity, true);
                                }

                                EntityManager.SetComponentEnabled<TroopSelected>(_currentSelectedEntity, false);

                                _currentSelectedEntity = Entity.Null;
                                _currentSelectedTeamColor = TeamColor.None;
                            }
                            else // currentPickedTroop and _currentSelectedEntity are not Null
                            {
                                if (_currentSelectedTeamColor == teamColor)
                                {
                                    EntityManager.SetComponentEnabled<TroopSelected>(currentPickedTroop, true);

                                    _currentSelectedEntity = currentPickedTroop;
                                    _currentSelectedTeamColor = teamColor;
                                }
                                else
                                {
                                    if (EntityManager.IsComponentEnabled<DragStartPosition>(s_pointInput))
                                    {
                                        EntityManager.SetComponentData(_currentSelectedEntity, new TargetForAttack { Target = currentPickedTroop });

                                        EntityManager.SetComponentEnabled<AICheckTargetValid>(_currentSelectedEntity, true);
                                        EntityManager.SetComponentEnabled<AISearchTarget>(_currentSelectedEntity, false);
                                    }

                                    EntityManager.SetComponentEnabled<TroopSelected>(_currentSelectedEntity, false);

                                    _currentSelectedEntity = Entity.Null;
                                    _currentSelectedTeamColor = TeamColor.None;
                                }
                            }
                        }
                    }
#if UNITY_EDITOR
                    UnityEngine.Debug.DrawLine(onDragEndRaycastHit.Position, onDragEndRaycastHit.Position + math.up() * 2.5f, UnityEngine.Color.blue, 5f);
#endif
                }

                EntityManager.SetComponentEnabled<DragStartPosition>(s_pointInput, false);
            }
        }

        private bool Cast(RaycastInput raycastInput, out RaycastHit raycastHit)
        {
            UnityEngine.Debug.DrawLine(raycastInput.Start, raycastInput.End, UnityEngine.Color.chocolate);

            PhysicsWorldSingleton physicsWorldSingleton = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
            CollisionWorld collisionWorld = physicsWorldSingleton.PhysicsWorld.CollisionWorld;

            return collisionWorld.CastRay(raycastInput, out raycastHit);
        }

        private (Entity, TeamColor) GetPickedTroop(float3 worldPosition)
        {
            Entity newSelectedEntity = Entity.Null;
            TeamColor teamColor = TeamColor.None;

            float minDistance = float.MaxValue;

            foreach (
                (RefRO<TroopAABB> troopAABB, RefRO<TroopEntity> troopEntity, RefRO<Team> team, DynamicBuffer<TroopHullPoint> troopHullPoints)
                in
                SystemAPI.Query<RefRO<TroopAABB>, RefRO<TroopEntity>, RefRO<Team>, DynamicBuffer<TroopHullPoint>>()
                    .WithAll<Troop>()
                    .WithDisabled<TroopSelected>())
            {
                // check AABB
                if (worldPosition.x < troopAABB.ValueRO.Min.x || worldPosition.x > troopAABB.ValueRO.Max.x ||
                    worldPosition.z < troopAABB.ValueRO.Min.y || worldPosition.z > troopAABB.ValueRO.Max.y)
                {
                    continue;
                }

                float2 worldPosition2d = worldPosition.xz;

                if (!IsInsidePolygon(worldPosition2d, troopHullPoints))
                {
                    continue;
                }

                float distance = math.distance(worldPosition2d, troopAABB.ValueRO.Center);
                if (distance < minDistance)
                {
                    minDistance = distance;

                    newSelectedEntity = troopEntity.ValueRO.Entity;
                    teamColor = team.ValueRO.Color;
                }
            }

            return (newSelectedEntity, teamColor);
        }

        private static bool IsInsidePolygon(float2 point, DynamicBuffer<TroopHullPoint> troopHullPoints)
        {
            bool inside = false;
            int n = troopHullPoints.Length;

            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                float2 a = troopHullPoints[i].Position.xz;
                float2 b = troopHullPoints[j].Position.xz;

                bool intersect =
                    a.y > point.y != b.y > point.y &&
                    point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y + 1e-6f) + a.x;

                if (intersect)
                {
                    inside = !inside;
                }
            }

            return inside;
        }
    }
}