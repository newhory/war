using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;

namespace War.Game.Systems
{
    public struct SpawnInput : IComponentData
    {
    }

    [UpdateInGroup(typeof(Group.InputUpdateGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct SpawnInputSystem : ISystem, ISystemStartStop
    {
        private static Entity s_pointInput;


        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void InitializeOnLoad() => s_pointInput = Entity.Null;


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
            if (!War.PointInput.IsSetCurrentSpawnSoldierData)
            {
                return;
            }

            if (s_pointInput == Entity.Null && SystemAPI.HasSingleton<PointInput>())
            {
                s_pointInput = SystemAPI.GetSingletonEntity<PointInput>();
            }

            if (s_pointInput == Entity.Null)
            {
                return;
            }

            if (state.EntityManager.IsComponentEnabled<OnPointerPressEnd>(s_pointInput))
            {
                state.EntityManager.SetComponentEnabled<OnPointerPressEnd>(s_pointInput, false);

                OnPointerDragEnd onPointerDragEnd = state.EntityManager.GetComponentData<OnPointerDragEnd>(s_pointInput);

                SpawnSoldierData spawnSoldierData = War.PointInput.CurrentSpawnSoldierData;

                spawnSoldierData.Position = onPointerDragEnd.PositionOnGround;
                spawnSoldierData.Rotation = quaternion.LookRotationSafe(new float3(0, onPointerDragEnd.PositionOnGround.y, onPointerDragEnd.PositionOnGround.z) - onPointerDragEnd.PositionOnGround, Vector3.up);

                SpawnSoldierSystem.SpawnSoldier(state.EntityManager, spawnSoldierData);
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
    }
}