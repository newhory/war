using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;


namespace War.Dots.Component.ComponentSystem
{
    [UpdateInGroup(typeof(Group.ViewSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct SyncUnityTransformSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            foreach (
                (RefRO<UnityTransform> unityTransform, RefRO<LocalTransform> localTransform)
                in
                SystemAPI.Query<RefRO<UnityTransform>, RefRO<LocalTransform>>())
            {
                Transform transform = unityTransform.ValueRO.Transform.Value;
                if (transform)
                {
                    transform.SetLocalPositionAndRotation(localTransform.ValueRO.Position, localTransform.ValueRO.Rotation);
                    transform.localScale = new float3(localTransform.ValueRO.Scale);
                }
            }
        }
    }
}