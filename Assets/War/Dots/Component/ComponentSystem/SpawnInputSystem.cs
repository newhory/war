using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;
using RaycastHit = Unity.Physics.RaycastHit;


namespace War.Dots.Component.ComponentSystem
{
    public struct SpawnInput : IComponentData
    {
        public SpawnSoldierData SpawnSoldierData;
    }

    public struct SetSpawnData : IComponentData, IEnableableComponent
    {
    }

    public struct UnsetSpawnData : IComponentData, IEnableableComponent
    {
    }

    public struct SpawnDecalPosition : IComponentData
    {
        public float3 Position;
    }

    [UpdateInGroup(typeof(Group.InputUpdateGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct SpawnInputSystem : ISystem, ISystemStartStop
    {
        private static Entity s_pointInput;

        private static bool s_setCurrentSpawnSoldierData;
        private static SpawnSoldierData s_currentSpawnSoldierData;


        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void InitializeOnLoad()
        {
            s_pointInput  = Entity.Null;
            s_setCurrentSpawnSoldierData = false;
            s_currentSpawnSoldierData = default;
        }


        public static void SetCurrentSpawnSoldierData(SpawnSoldierData spawnSoldierData)
        {
            s_currentSpawnSoldierData = spawnSoldierData;
            s_setCurrentSpawnSoldierData = true;
        }

        public static void UnsetCurrentSpawnSoldierData() => s_setCurrentSpawnSoldierData = false;


        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsWorldSingleton>();
            state.RequireForUpdate<PointInput>();
            state.RequireForUpdate<SpawnInput>();
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

            if (s_pointInput == Entity.Null)
            {
                return;
            }

            if (!s_setCurrentSpawnSoldierData)
            {
                state.EntityManager.SetComponentEnabled<UnsetSpawnData>(s_pointInput, true);

                return;
            }

            state.EntityManager.SetComponentEnabled<SetSpawnData>(s_pointInput, true);
            state.EntityManager.SetComponentData(s_pointInput, new SpawnInput { SpawnSoldierData = s_currentSpawnSoldierData });

            OnPointerMove pointerMove = state.EntityManager.GetComponentData<OnPointerMove>(s_pointInput);

            RaycastInput raycastInput = new()
            {
                Start = pointerMove.Ray.Origin,
                End = pointerMove.Ray.Origin + pointerMove.Ray.Displacement,
                Filter = new CollisionFilter
                {
                    BelongsTo = 1u << Setting.ArrowLayer,
                    CollidesWith = 1u << Setting.WorldLayer,
                }
            };

            if (Cast(raycastInput, out RaycastHit onDragStartRaycastHit))
            {
                state.EntityManager.SetComponentData(s_pointInput, new SpawnDecalPosition { Position = onDragStartRaycastHit.Position });
            }

            if (state.EntityManager.IsComponentEnabled<OnPointerPressEnd>(s_pointInput))
            {
                state.EntityManager.SetComponentEnabled<OnPointerPressEnd>(s_pointInput, false);

                OnPointerDragEnd onPointerDragEnd = state.EntityManager.GetComponentData<OnPointerDragEnd>(s_pointInput);

                raycastInput.Start = onPointerDragEnd.Ray.Origin;
                raycastInput.End = onPointerDragEnd.Ray.Origin + onPointerDragEnd.Ray.Displacement;

                if (Cast(raycastInput, out RaycastHit onDragEndRaycastHit))
                {
                    s_currentSpawnSoldierData.Position = onDragEndRaycastHit.Position;

                    SpawnSoldierSystem.SpawnSoldier(state.EntityManager, s_currentSpawnSoldierData);
                }
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
    }
}