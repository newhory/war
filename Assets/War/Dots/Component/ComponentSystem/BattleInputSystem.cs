using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;


namespace War.Dots.Component.ComponentSystem
{
    public struct BattleInput : IComponentData
    {
    }

    [RequireMatchingQueriesForUpdate]
    public partial struct BattleInputSystem : ISystem, ISystemStartStop
    {
        private static Entity s_pointInput;


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


        private Entity _currentSelectedEntity;
        private TeamColor _currentSelectedTeamColor;

        private Entity _currentTargetCandidateEntity;


        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsWorldSingleton>();
            state.RequireForUpdate<PointInput>();
            state.RequireForUpdate<BattleInput>();
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            if (s_pointInput == Entity.Null && SystemAPI.HasSingleton<PointInput>())
            {
                s_pointInput = SystemAPI.GetSingletonEntity<PointInput>();
            }

            RaycastInput raycastInput = new()
            {
                Filter = new CollisionFilter
                {
                    BelongsTo = 1u << Setting.ArrowLayer,
                    CollidesWith = 1u << Setting.BlueTeamLayer | 1u << Setting.RedTeamLayer | 1u << Setting.WorldLayer,
                }
            };

            float dragInputOffset = state.EntityManager.GetComponentData<PointInput>(s_pointInput).DragOffset;

            if (state.EntityManager.IsComponentEnabled<OnPointerPressStart>(s_pointInput))
            {
                float3 dragStartPosition;

                if (state.EntityManager.IsComponentEnabled<DragStartPosition>(s_pointInput))
                {
                    if (_currentTargetCandidateEntity != Entity.Null)
                    {
                        state.EntityManager.SetComponentEnabled<TroopSelected>(_currentTargetCandidateEntity, false);

                        _currentTargetCandidateEntity = Entity.Null;
                    }

                    OnPointerDragging onPointerDragging = state.EntityManager.GetComponentData<OnPointerDragging>(s_pointInput);

                    dragStartPosition = state.EntityManager.GetComponentData<DragStartPosition>(s_pointInput).Position;

                    raycastInput.Start = onPointerDragging.Ray.Origin;
                    raycastInput.End = onPointerDragging.Ray.Origin + onPointerDragging.Ray.Displacement;

                    if (Cast(raycastInput, out RaycastHit onDraggingRaycastHit))
                    {
                        (Entity currentPickedTroop, TeamColor teamColor) = GetPickedTroop(ref state, onDraggingRaycastHit.Position);

                        if (currentPickedTroop != Entity.Null && teamColor != _currentSelectedTeamColor)
                        {
                            state.EntityManager.SetComponentEnabled<TroopSelected>(currentPickedTroop, true);

                            _currentTargetCandidateEntity = currentPickedTroop;
                        }
#if UNITY_EDITOR
                        UnityEngine.Debug.DrawLine(dragStartPosition, onDraggingRaycastHit.Position, UnityEngine.Color.red);
#endif
                    }
                }
                else
                {
                    OnPointerDragStart onPointerDragStart = state.EntityManager.GetComponentData<OnPointerDragStart>(s_pointInput);
                    OnPointerDragging onPointerDragging = state.EntityManager.GetComponentData<OnPointerDragging>(s_pointInput);

                    if (math.distance(onPointerDragStart.Point, onPointerDragging.Point) >= dragInputOffset)
                    {
                        raycastInput.Start = onPointerDragStart.Ray.Origin;
                        raycastInput.End = onPointerDragStart.Ray.Origin + onPointerDragStart.Ray.Displacement;

                        if (Cast(raycastInput, out RaycastHit onDragStartRaycastHit))
                        {
                            dragStartPosition = onDragStartRaycastHit.Position;

                            state.EntityManager.SetComponentEnabled<DragStartPosition>(s_pointInput, true);
                            state.EntityManager.SetComponentData(s_pointInput, new DragStartPosition { Position = dragStartPosition });

                            (Entity currentPickedTroop, TeamColor teamColor) = GetPickedTroop(ref state, dragStartPosition);

                            if (currentPickedTroop != Entity.Null)
                            {
                                state.EntityManager.SetComponentEnabled<TroopSelected>(currentPickedTroop, true);

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

            if (state.EntityManager.IsComponentEnabled<OnPointerPressEnd>(s_pointInput))
            {
                if (_currentTargetCandidateEntity != Entity.Null)
                {
                    state.EntityManager.SetComponentEnabled<TroopSelected>(_currentTargetCandidateEntity, false);

                    _currentTargetCandidateEntity = Entity.Null;
                }

                state.EntityManager.SetComponentEnabled<OnPointerPressEnd>(s_pointInput, false);

                OnPointerDragEnd onPointerDragEnd = state.EntityManager.GetComponentData<OnPointerDragEnd>(s_pointInput);

                raycastInput.Start = onPointerDragEnd.Ray.Origin;
                raycastInput.End = onPointerDragEnd.Ray.Origin + onPointerDragEnd.Ray.Displacement;

                if (Cast(raycastInput, out RaycastHit onDragEndRaycastHit))
                {
                    (Entity currentPickedTroop, TeamColor teamColor) = GetPickedTroop(ref state, onDragEndRaycastHit.Position);

                    if (_currentSelectedEntity != currentPickedTroop)
                    {
                        if (_currentSelectedEntity == Entity.Null) // currentPickedTroop is not Null
                        {
                            state.EntityManager.SetComponentEnabled<TroopSelected>(currentPickedTroop, true);

                            _currentSelectedEntity = currentPickedTroop;
                            _currentSelectedTeamColor = teamColor;
                        }
                        else
                        {
                            if (currentPickedTroop == Entity.Null) // _currentSelectedEntity is not Null
                            {
                                if (state.EntityManager.IsComponentEnabled<DragStartPosition>(s_pointInput))
                                {
                                    state.EntityManager.SetComponentData(_currentSelectedEntity, new Destination { Position = onDragEndRaycastHit.Position });

                                    state.EntityManager.SetComponentEnabled<TroopAICheckTargetValid>(_currentSelectedEntity, false);
                                    state.EntityManager.SetComponentEnabled<TroopAISearchTarget>(_currentSelectedEntity, false);
                                    state.EntityManager.SetComponentEnabled<StateMoveInFormation>(_currentSelectedEntity, true);
                                }

                                state.EntityManager.SetComponentEnabled<TroopSelected>(_currentSelectedEntity, false);

                                _currentSelectedEntity = Entity.Null;
                                _currentSelectedTeamColor = TeamColor.None;
                            }
                            else // currentPickedTroop and _currentSelectedEntity are not Null
                            {
                                if (_currentSelectedTeamColor == teamColor)
                                {
                                    state.EntityManager.SetComponentEnabled<TroopSelected>(currentPickedTroop, true);

                                    _currentSelectedEntity = currentPickedTroop;
                                    _currentSelectedTeamColor = teamColor;
                                }
                                else
                                {
                                    if (state.EntityManager.IsComponentEnabled<DragStartPosition>(s_pointInput))
                                    {
                                        state.EntityManager.SetComponentData(_currentSelectedEntity, new TroopTargetForAttack { TargetTroop = currentPickedTroop });

                                        state.EntityManager.SetComponentEnabled<TroopAICheckTargetValid>(_currentSelectedEntity, true);
                                        state.EntityManager.SetComponentEnabled<TroopAISearchTarget>(_currentSelectedEntity, false);
                                    }

                                    state.EntityManager.SetComponentEnabled<TroopSelected>(_currentSelectedEntity, false);

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

                state.EntityManager.SetComponentEnabled<DragStartPosition>(s_pointInput, false);
            }
        }

        public void OnStartRunning(ref SystemState state)
        {
            if (SystemAPI.HasSingleton<PointInput>())
            {
                s_pointInput = SystemAPI.GetSingletonEntity<PointInput>();
            }
        }

        public void OnStopRunning(ref SystemState state)
        {
        }

        private bool Cast(RaycastInput raycastInput, out RaycastHit raycastHit)
        {
#if UNITY_EDITOR
            UnityEngine.Debug.DrawLine(raycastInput.Start, raycastInput.End, UnityEngine.Color.chocolate);
#endif
            PhysicsWorldSingleton physicsWorldSingleton = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
            CollisionWorld collisionWorld = physicsWorldSingleton.PhysicsWorld.CollisionWorld;

            return collisionWorld.CastRay(raycastInput, out raycastHit);
        }

        private (Entity, TeamColor) GetPickedTroop(ref SystemState state, float3 worldPosition)
        {
            Entity newSelectedEntity = Entity.Null;
            TeamColor teamColor = TeamColor.None;

            float minDistance = float.MaxValue;

            foreach (
                (RefRO<TroopAABB> troopAABB, RefRO<TroopEntity> troopEntity, RefRO<Team> team, DynamicBuffer<TroopHullPoint> troopHullPoints)
                in
                SystemAPI.Query<RefRO<TroopAABB>, RefRO<TroopEntity>, RefRO<Team>, DynamicBuffer<TroopHullPoint>>()
                    .WithAll<Troop>())
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
    }
}