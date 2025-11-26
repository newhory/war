using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using ZLinq;


namespace War.Dots.Component.ComponentSystem
{
    public struct BattleInput : IComponentData
    {
    }

    [UpdateInGroup(typeof(Group.InputUpdateGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct BattleInputSystem : ISystem, ISystemStartStop
    {


        public static Entity PointInput { get; private set; }
        public static Entity CurrentSelectedEntity { get; private set; }
        public static TeamColor CurrentSelectedTeamColor { get; private set; }
        public static Entity CurrentTargetCandidateEntity { get; private set; }


        public static void Reset()
        {
            CurrentSelectedEntity = Entity.Null;
            CurrentSelectedTeamColor = TeamColor.None;
            CurrentTargetCandidateEntity = Entity.Null;
        }

        [BurstCompile]
        private static bool Cast(PhysicsWorldSingleton physicsWorldSingleton, RaycastInput raycastInput, out RaycastHit raycastHit)
        {
#if UNITY_EDITOR
            UnityEngine.Debug.DrawLine(raycastInput.Start, raycastInput.End, UnityEngine.Color.chocolate);
#endif
            CollisionWorld collisionWorld = physicsWorldSingleton.PhysicsWorld.CollisionWorld;

            return collisionWorld.CastRay(raycastInput, out raycastHit);
        }

        [BurstCompile]
        private static bool IsInsidePolygon(float2 point, DynamicBuffer<TroopHullPoint> troopHullPoints)
        {
            bool inside = false;
            int n = troopHullPoints.Length;

            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                float2 a = troopHullPoints[i].Position;
                float2 b = troopHullPoints[j].Position;

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
            if (PointInput == Entity.Null && SystemAPI.HasSingleton<PointInput>())
            {
                PointInput = SystemAPI.GetSingletonEntity<PointInput>();
            }

            PhysicsWorldSingleton physicsWorldSingleton = SystemAPI.GetSingleton<PhysicsWorldSingleton>();

            RaycastInput raycastInput = new()
            {
                Filter = new CollisionFilter
                {
                    BelongsTo = 1u << Setting.ArrowLayer,
                    CollidesWith = 1u << Setting.BlueTeamLayer | 1u << Setting.RedTeamLayer | 1u << Setting.WorldLayer,
                }
            };

            float dragInputOffset = state.EntityManager.GetComponentData<PointInput>(PointInput).DragOffset;

            if (state.EntityManager.IsComponentEnabled<OnPointerPressStart>(PointInput))
            {
                state.EntityManager.SetComponentEnabled<DragEndWorldPosition>(PointInput, false);

                float3 dragStartPosition;

                if (state.EntityManager.IsComponentEnabled<DragStartWorldPosition>(PointInput))
                {
                    if (CurrentTargetCandidateEntity != Entity.Null)
                    {
                        state.EntityManager.SetComponentEnabled<TroopSelected>(CurrentTargetCandidateEntity, false);

                        CurrentTargetCandidateEntity = Entity.Null;
                    }

                    OnPointerDragging onPointerDragging = state.EntityManager.GetComponentData<OnPointerDragging>(PointInput);

                    dragStartPosition = state.EntityManager.GetComponentData<DragStartWorldPosition>(PointInput).Position;

                    raycastInput.Start = onPointerDragging.Ray.Origin;
                    raycastInput.End = onPointerDragging.Ray.Origin + onPointerDragging.Ray.Displacement;

                    // dragging
                    if (Cast(physicsWorldSingleton, raycastInput, out RaycastHit onDraggingRaycastHit))
                    {
                        state.EntityManager.SetComponentEnabled<DraggingWorldPosition>(PointInput, true);
                        state.EntityManager.SetComponentData(PointInput, new DraggingWorldPosition { Position = onDraggingRaycastHit.Position });

                        (Entity currentPickedTroop, TeamColor teamColor) = GetPickedTroop(ref state, onDraggingRaycastHit.Position);

                        if (currentPickedTroop != Entity.Null && teamColor != CurrentSelectedTeamColor)
                        {
                            state.EntityManager.SetComponentEnabled<TroopSelected>(currentPickedTroop, true);

                            CurrentTargetCandidateEntity = currentPickedTroop;
                        }
#if UNITY_EDITOR
                        UnityEngine.Debug.DrawLine(dragStartPosition, onDraggingRaycastHit.Position, UnityEngine.Color.red);
#endif
                    }
                }
                else
                {
                    OnPointerDragStart onPointerDragStart = state.EntityManager.GetComponentData<OnPointerDragStart>(PointInput);
                    OnPointerDragging onPointerDragging = state.EntityManager.GetComponentData<OnPointerDragging>(PointInput);

                    if (CurrentSelectedEntity == Entity.Null ||
                        math.distance(onPointerDragStart.Point, onPointerDragging.Point) >= dragInputOffset)
                    {
                        raycastInput.Start = onPointerDragStart.Ray.Origin;
                        raycastInput.End = onPointerDragStart.Ray.Origin + onPointerDragStart.Ray.Displacement;

                        // drag start
                        if (Cast(physicsWorldSingleton, raycastInput, out RaycastHit onDragStartRaycastHit))
                        {
                            dragStartPosition = onDragStartRaycastHit.Position;

                            state.EntityManager.SetComponentEnabled<DragStartWorldPosition>(PointInput, true);
                            state.EntityManager.SetComponentData(PointInput, new DragStartWorldPosition { Position = dragStartPosition });

                            (Entity currentPickedTroop, TeamColor teamColor) = GetPickedTroop(ref state, dragStartPosition);

                            if (currentPickedTroop != Entity.Null)
                            {
                                state.EntityManager.SetComponentEnabled<TroopSelected>(currentPickedTroop, true);

                                CurrentSelectedEntity = currentPickedTroop;
                                CurrentSelectedTeamColor = teamColor;
                            }
#if UNITY_EDITOR
                            UnityEngine.Debug.DrawLine(dragStartPosition, dragStartPosition + math.up() * 2.5f, UnityEngine.Color.azure, 5f);
#endif
                        }
                    }
                }
            }

            if (state.EntityManager.IsComponentEnabled<OnPointerPressEnd>(PointInput))
            {
                if (CurrentTargetCandidateEntity != Entity.Null)
                {
                    state.EntityManager.SetComponentEnabled<TroopSelected>(CurrentTargetCandidateEntity, false);

                    CurrentTargetCandidateEntity = Entity.Null;
                }

                state.EntityManager.SetComponentEnabled<OnPointerPressEnd>(PointInput, false);

                OnPointerDragEnd onPointerDragEnd = state.EntityManager.GetComponentData<OnPointerDragEnd>(PointInput);

                raycastInput.Start = onPointerDragEnd.Ray.Origin;
                raycastInput.End = onPointerDragEnd.Ray.Origin + onPointerDragEnd.Ray.Displacement;

                if (Cast(physicsWorldSingleton, raycastInput, out RaycastHit onDragEndRaycastHit))
                {
                    state.EntityManager.SetComponentEnabled<DragEndWorldPosition>(PointInput, true);
                    state.EntityManager.SetComponentData(PointInput, new DragEndWorldPosition { Position = onDragEndRaycastHit.Position });

                    (Entity currentPickedTroop, TeamColor teamColor) = GetPickedTroop(ref state, onDragEndRaycastHit.Position);

                    if (CurrentSelectedEntity != currentPickedTroop)
                    {
                        if (CurrentSelectedEntity == Entity.Null) // currentPickedTroop is not Null
                        {
                            state.EntityManager.SetComponentEnabled<TroopSelected>(currentPickedTroop, true);

                            CurrentSelectedEntity = currentPickedTroop;
                            CurrentSelectedTeamColor = teamColor;
                        }
                        else
                        {
                            if (currentPickedTroop == Entity.Null) // _currentSelectedEntity is not Null
                            {
                                if (state.EntityManager.IsComponentEnabled<DragStartWorldPosition>(PointInput))
                                {
                                    state.EntityManager.SetComponentData(CurrentSelectedEntity, new TroopTargetForAttack { TargetTroop = Entity.Null });
                                    state.EntityManager.SetComponentData(CurrentSelectedEntity, new Destination { Position = onDragEndRaycastHit.Position });

                                    state.EntityManager.SetComponentEnabled<TroopAICheckTargetValid>(CurrentSelectedEntity, false);
                                    state.EntityManager.SetComponentEnabled<TroopAISearchTarget>(CurrentSelectedEntity, false);
                                    state.EntityManager.SetComponentEnabled<TroopStateMoveToDestination>(CurrentSelectedEntity, true);
                                }

                                state.EntityManager.SetComponentEnabled<TroopSelected>(CurrentSelectedEntity, false);

                                CurrentSelectedEntity = Entity.Null;
                                CurrentSelectedTeamColor = TeamColor.None;
                            }
                            else // currentPickedTroop and _currentSelectedEntity are not Null
                            {
                                if (CurrentSelectedTeamColor == teamColor)
                                {
                                    state.EntityManager.SetComponentEnabled<TroopSelected>(CurrentSelectedEntity, false);
                                    state.EntityManager.SetComponentEnabled<TroopSelected>(currentPickedTroop, true);

                                    CurrentSelectedEntity = currentPickedTroop;
                                    CurrentSelectedTeamColor = teamColor;
                                }
                                else
                                {
                                    if (state.EntityManager.IsComponentEnabled<DragStartWorldPosition>(PointInput))
                                    {
                                        state.EntityManager.SetComponentData(CurrentSelectedEntity, new TroopTargetForAttack { TargetTroop = currentPickedTroop });

                                        state.EntityManager.SetComponentEnabled<TroopAICheckTargetValid>(CurrentSelectedEntity, true);
                                        state.EntityManager.SetComponentEnabled<TroopAISearchTarget>(CurrentSelectedEntity, false);
                                    }

                                    state.EntityManager.SetComponentEnabled<TroopSelected>(CurrentSelectedEntity, false);

                                    CurrentSelectedEntity = Entity.Null;
                                    CurrentSelectedTeamColor = TeamColor.None;
                                }
                            }
                        }
                    }
#if UNITY_EDITOR
                    UnityEngine.Debug.DrawLine(onDragEndRaycastHit.Position, onDragEndRaycastHit.Position + math.up() * 2.5f, UnityEngine.Color.blue, 5f);
#endif
                }

                state.EntityManager.SetComponentEnabled<DragStartWorldPosition>(PointInput, false);
                state.EntityManager.SetComponentEnabled<DraggingWorldPosition>(PointInput, false);
            }
        }

        public void OnStartRunning(ref SystemState state)
        {
            if (SystemAPI.HasSingleton<PointInput>())
            {
                PointInput = SystemAPI.GetSingletonEntity<PointInput>();
            }
        }

        public void OnStopRunning(ref SystemState state)
        {
        }

        private struct TroopPicked
        {
            public Entity Entity;
            public TeamColor TeamColor;
            public float Distance;
        }

        [BurstCompile]
        private partial struct FillTroopPickedJob : IJobEntity
        {
            [ReadOnly] public float3 WorldPosition;

            public NativeArray<TroopPicked> TroopPickedArray;


            public void Execute([EntityIndexInQuery] int entityIndex, DynamicBuffer<TroopHullPoint> troopHullPoints, in TroopAABB troopAABB, in TroopEntity troopEntity, in Team team)
            {
                TroopPicked troopPicked = new()
                {
                    Entity = Entity.Null,
                    TeamColor = TeamColor.None,
                    Distance = float.MaxValue
                };

                // check AABB
                if (WorldPosition.x < troopAABB.Min.x || WorldPosition.x > troopAABB.Max.x ||
                    WorldPosition.z < troopAABB.Min.y || WorldPosition.z > troopAABB.Max.y)
                {
                    TroopPickedArray[entityIndex] = troopPicked;
                    return;
                }

                float2 worldPosition2d = WorldPosition.xz;

                if (!IsInsidePolygon(worldPosition2d, troopHullPoints))
                {
                    TroopPickedArray[entityIndex] = troopPicked;
                    return;
                }

                troopPicked.Entity = troopEntity.Entity;
                troopPicked.TeamColor = team.Color;
                troopPicked.Distance = math.distance(worldPosition2d, troopAABB.Center);

                TroopPickedArray[entityIndex] = troopPicked;
            }
        }

        private (Entity, TeamColor) GetPickedTroop(ref SystemState state, float3 worldPosition)
        {
            EntityQuery troopQuery = SystemAPI.QueryBuilder().WithAll<Troop, TroopAABB, TroopEntity, Team, TroopHullPoint>().Build();
            if (troopQuery.IsEmpty)
            {
                return (Entity.Null, TeamColor.None);
            }

            NativeArray<TroopPicked> troopPickedArray = new(troopQuery.CalculateEntityCount(), Allocator.TempJob);

            new FillTroopPickedJob
                {
                    WorldPosition = worldPosition,
                    TroopPickedArray = troopPickedArray
                }
                .ScheduleParallel(troopQuery, state.Dependency)
                .Complete();

            TroopPicked result = troopPickedArray.AsValueEnumerable().MinBy(troopPicked => troopPicked.Distance);

            return (result.Entity, result.TeamColor);
        }
    }
}