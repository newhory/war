using Unity.Entities;
using UnityEngine;


namespace War.Dots.Component.Authoring
{
    public class FieldCameraAuthoring : MonoBehaviour
    {
        [Header("Original Camera Position / Rotation")] [SerializeField]
        private Vector3 originalCameraPosition;

        [SerializeField] private Quaternion originalCameraRotation;

        [Header("Zoom / Distance")] [SerializeField]
        private float padding = 1f; // extra margin around objects

        [SerializeField] private float smoothTime = 0.3f; // damping for position/zoom/rotation
        [SerializeField] private float minDistance = 2f;
        [SerializeField] private float maxDistance = 200f;

        [Header("Orthographic")] [SerializeField]
        private float minOrthographicSize = 1f;

        [SerializeField] private float maxOrthographicSize = 200f;


        private class Baker : Baker<FieldCameraAuthoring>
        {
            public override void Bake(FieldCameraAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(
                    entity,
                    new FieldCamera
                    {
                        OriginalCameraPosition = authoring.originalCameraPosition,
                        OriginalCameraRotation = authoring.originalCameraRotation,

                        Padding = authoring.padding,
                        SmoothTime = authoring.smoothTime,
                        MinDistance = authoring.minDistance,
                        MaxDistance = authoring.maxDistance,

                        MinOrthographicSize = authoring.minOrthographicSize,
                        MaxOrthographicSize = authoring.maxOrthographicSize,
                    });

                AddComponent(entity, new FieldCameraRuntimeData());
                AddComponent(entity, new UseFieldCamera());
                AddComponent(entity, new ResetFieldCamera());

                SetComponentEnabled<UseFieldCamera>(entity, false);
                SetComponentEnabled<ResetFieldCamera>(entity, false);
            }
        }
    }
}