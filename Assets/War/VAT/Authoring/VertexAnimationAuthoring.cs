using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEngine;


namespace War.VAT.Authoring
{
    public class VertexAnimationAuthoring : MonoBehaviour
    {
        [SerializeField] private VatData vatData;
        [SerializeField] private string defaultClip = "idle";


        private class VertexAnimationAuthoringBaker : Baker<VertexAnimationAuthoring>
        {
            public override void Bake(VertexAnimationAuthoring authoring)
            {
                Entity mainEntity = GetEntity(TransformUsageFlags.Dynamic);

                DynamicBuffer<Child> children = AddBuffer<Child>(mainEntity);
                foreach (VatMeshData meshData in authoring.vatData.meshData)
                {
                    Entity childEntity = CreateAdditionalEntity(TransformUsageFlags.Renderable);

                    AddComponent(childEntity, new Parent { Value = mainEntity });
                    AddComponent(childEntity, new LocalTransform
                    {
                        Position = float3.zero,
                        Rotation = quaternion.identity,
                        Scale = 1.0f
                    });

                    children.Add(new Child { Value = childEntity });

                    AddComponent<FrameIndexProperty>(childEntity);
                    AddComponent<InitializeVertexAnimation>(childEntity);

                    AddComponentObject(childEntity, meshData);
                }

                DynamicBuffer<ClipData> clipDataBuffer = AddBuffer<ClipData>(mainEntity);
                foreach (VatClipData clipData in authoring.vatData.clipData)
                {
                    clipDataBuffer.Add(new ClipData(clipData));
                }

                AddComponent(mainEntity, new CurrentClipProperty
                {
                    ClipKeyword = authoring.defaultClip,
                    AccumulatedTime = 0f,
                });
            }
        }
    }
}