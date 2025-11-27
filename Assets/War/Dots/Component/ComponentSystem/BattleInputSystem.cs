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


        private EntityQuery _troopQuery;


        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsWorldSingleton>();
            state.RequireForUpdate<PointInput>();
            state.RequireForUpdate<BattleInput>();

            _troopQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<Troop, TroopAABB, TroopEntity, Team, TroopHullPoint>()
                    .Build();
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

                // drag is not started yet
                if (!state.EntityManager.IsComponentEnabled<DragStartWorldPosition>(PointInput))
                {
                    OnPointerDragStart onPointerDragStart = state.EntityManager.GetComponentData<OnPointerDragStart>(PointInput);
                    OnPointerDragging onPointerDragging = state.EntityManager.GetComponentData<OnPointerDragging>(PointInput);

                    raycastInput.Start = onPointerDragStart.Ray.Origin;
                    raycastInput.End = onPointerDragStart.Ray.Origin + onPointerDragStart.Ray.Displacement;

                    if (Cast(physicsWorldSingleton, raycastInput, out RaycastHit onPressStartRaycastHit))
                    {
                        float3 pressStartPosition = onPressStartRaycastHit.Position;

                        (Entity currentPickedTroop, TeamColor teamColor) = GetPickedTroop(ref state, pressStartPosition);
                        if (CurrentSelectedEntity != currentPickedTroop)
                        {
                            if (CurrentSelectedEntity != Entity.Null)
                            {
                                state.EntityManager.SetComponentEnabled<TroopSelected>(CurrentSelectedEntity, false);
                            }

                            if (currentPickedTroop != Entity.Null)
                            {
                                state.EntityManager.SetComponentEnabled<TroopSelected>(currentPickedTroop, true);

                                CurrentSelectedEntity = currentPickedTroop;
                                CurrentSelectedTeamColor = teamColor;
                            }
                            else
                            {
                                CurrentSelectedEntity = Entity.Null;
                                CurrentSelectedTeamColor = TeamColor.None;
                            }
                        }
                        else if (CurrentSelectedEntity != Entity.Null &&
                                 math.distance(onPointerDragStart.Point, onPointerDragging.Point) >= dragInputOffset)
                        {
                            // drag start
                            state.EntityManager.SetComponentEnabled<DragStartWorldPosition>(PointInput, true);
                            state.EntityManager.SetComponentData(PointInput, new DragStartWorldPosition { Position = pressStartPosition });
                        }
#if UNITY_EDITOR
                        UnityEngine.Debug.DrawLine(pressStartPosition, pressStartPosition + math.up() * 2.5f, UnityEngine.Color.azure, 5f);
#endif
                    }
                }
                else
                {
                    float3 dragStartPosition = state.EntityManager.GetComponentData<DragStartWorldPosition>(PointInput).Position;

                    OnPointerDragging onPointerDragging = state.EntityManager.GetComponentData<OnPointerDragging>(PointInput);

                    raycastInput.Start = onPointerDragging.Ray.Origin;
                    raycastInput.End = onPointerDragging.Ray.Origin + onPointerDragging.Ray.Displacement;

                    // dragging
                    if (Cast(physicsWorldSingleton, raycastInput, out RaycastHit onDraggingRaycastHit))
                    {
                        state.EntityManager.SetComponentEnabled<DraggingWorldPosition>(PointInput, true);
                        state.EntityManager.SetComponentData(PointInput, new DraggingWorldPosition { Position = onDraggingRaycastHit.Position });

                        (Entity currentPickedTroop, TeamColor teamColor) = GetPickedTroop(ref state, onDraggingRaycastHit.Position);
                        if (CurrentSelectedEntity != currentPickedTroop)
                        {
                            if (CurrentTargetCandidateEntity != currentPickedTroop)
                            {
                                if (CurrentTargetCandidateEntity != Entity.Null)
                                {
                                    state.EntityManager.SetComponentEnabled<TroopSelected>(CurrentTargetCandidateEntity, false);
                                }

                                if (currentPickedTroop != Entity.Null && teamColor != CurrentSelectedTeamColor)
                                {
                                    state.EntityManager.SetComponentEnabled<TroopSelected>(currentPickedTroop, true);

                                    CurrentTargetCandidateEntity = currentPickedTroop;
                                }
                                else
                                {
                                    CurrentTargetCandidateEntity = Entity.Null;
                                }
                            }
                        }
#if UNITY_EDITOR
                        UnityEngine.Debug.DrawLine(dragStartPosition, onDraggingRaycastHit.Position, UnityEngine.Color.red);
#endif
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

                // drag had started
                if (state.EntityManager.IsComponentEnabled<DragStartWorldPosition>(PointInput))
                {
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
                            if (currentPickedTroop == Entity.Null) // CurrentSelectedEntity is not Null
                            {
                                state.EntityManager.SetComponentData(CurrentSelectedEntity, new TroopTargetForAttack { TargetTroop = Entity.Null });
                                state.EntityManager.SetComponentData(CurrentSelectedEntity, new Destination { Position = onDragEndRaycastHit.Position });

                                state.EntityManager.SetComponentEnabled<TroopAICheckTargetValid>(CurrentSelectedEntity, false);
                                state.EntityManager.SetComponentEnabled<TroopAISearchTarget>(CurrentSelectedEntity, false);
                                state.EntityManager.SetComponentEnabled<TroopStateMoveToDestination>(CurrentSelectedEntity, true);
                            }
                            else // currentPickedTroop and _currentSelectedEntity are not Null
                            {
                                if (CurrentSelectedTeamColor != teamColor)
                                {
                                    state.EntityManager.SetComponentData(CurrentSelectedEntity, new TroopTargetForAttack { TargetTroop = currentPickedTroop });

                                    state.EntityManager.SetComponentEnabled<TroopAICheckTargetValid>(CurrentSelectedEntity, true);
                                    state.EntityManager.SetComponentEnabled<TroopAISearchTarget>(CurrentSelectedEntity, false);
                                }
                            }

                            state.EntityManager.SetComponentEnabled<TroopSelected>(CurrentSelectedEntity, false);
                            CurrentSelectedEntity = Entity.Null;
                            CurrentSelectedTeamColor = TeamColor.None;
                        }
#if UNITY_EDITOR
                        UnityEngine.Debug.DrawLine(onDragEndRaycastHit.Position, onDragEndRaycastHit.Position + math.up() * 2.5f, UnityEngine.Color.blue, 5f);
#endif
                    }

                    state.EntityManager.SetComponentEnabled<DragStartWorldPosition>(PointInput, false);
                    state.EntityManager.SetComponentEnabled<DraggingWorldPosition>(PointInput, false);
                }
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

        private (Entity, TeamColor) GetPickedTroop(ref SystemState state, float3 worldPosition)
        {
            if (_troopQuery.IsEmpty)
            {
                return (Entity.Null, TeamColor.None);
            }

            NativeArray<TroopPicked> troopPickedArray = new(_troopQuery.CalculateEntityCount(), Allocator.TempJob);

            new FillTroopPickedJob
                {
                    WorldPosition = worldPosition,
                    TroopPickedArray = troopPickedArray
                }
                .ScheduleParallel(_troopQuery, state.Dependency)
                .Complete();

            TroopPicked result = troopPickedArray.AsValueEnumerable().MinBy(troopPicked => troopPicked.Distance);

            return (result.Entity, result.TeamColor);
        }
    }
}