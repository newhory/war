using Unity.Cinemachine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;


namespace War.Dots.Component.ComponentSystem
{
    [UpdateAfter(typeof(Group.LastUpdateGroup))]
    [UpdateBefore(typeof(Group.ViewSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial class FieldCameraSystem : SystemBase
    {
        private EntityQuery _fieldCameraQuery;
        private EntityQuery _resetFieldCameraQuery;


        protected override void OnCreate()
        {
            RequireForUpdate<FieldCamera>();

            _fieldCameraQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<FieldCamera, UseFieldCamera>()
                    .WithAllRW<FieldCameraRuntimeData>()
                    .Build();

            _resetFieldCameraQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<ResetFieldCamera>()
                    .Build();
        }

        protected override void OnStartRunning()
        {
            ResetCamera();
        }

        private Entity ResetCamera()
        {
            CinemachineCamera cam = FieldCameraInstance.FieldCamera;
            Transform camTransform = cam.transform;

            Entity fieldCameraEntity = SystemAPI.GetSingletonEntity<FieldCamera>();
            FieldCamera fieldCamera = EntityManager.GetComponentData<FieldCamera>(fieldCameraEntity);

            camTransform.position = fieldCamera.OriginalCameraPosition;
            camTransform.rotation = fieldCamera.OriginalCameraRotation;

            return fieldCameraEntity;
        }

        protected override void OnUpdate()
        {
            if (!_resetFieldCameraQuery.IsEmpty)
            {
                EntityManager.SetComponentEnabled<ResetFieldCamera>(ResetCamera(), false);
            }

            if (_fieldCameraQuery.IsEmpty)
            {
                return;
            }

            float3 sumPosition = float3.zero;
            int count = 0;

            foreach (RefRO<LocalTransform> localTransform in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<Alive, NavMeshAgentData>())
            {
                sumPosition += localTransform.ValueRO.Position;
                count++;
            }

            if (count == 0)
            {
                return;
            }

            float3 centroid = sumPosition / count;

            float maxRadius = float.MinValue;

            foreach ((RefRO<LocalTransform> localTransform, RefRO<NavMeshAgentData> navMeshAgentData) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<NavMeshAgentData>>().WithAll<Alive>())
            {
                float distance = math.distance(localTransform.ValueRO.Position, centroid) + navMeshAgentData.ValueRO.Radius;
                if (distance > maxRadius)
                {
                    maxRadius = distance;
                }
            }

            FieldCamera fieldCamera = _fieldCameraQuery.GetSingleton<FieldCamera>();
            FieldCameraRuntimeData fieldCameraRuntimeData = _fieldCameraQuery.GetSingleton<FieldCameraRuntimeData>();
            CinemachineCamera camera = FieldCameraInstance.FieldCamera;

            Quaternion fixedRot = fieldCamera.OriginalCameraRotation;
            float deltaTime = SystemAPI.Time.DeltaTime;
            float aspect = camera.Lens.Aspect;
            float paddedRadius = maxRadius + fieldCamera.Padding;
            Vector3 velocityPos = fieldCameraRuntimeData.VelocityPos;
            Transform cameraTransform = camera.transform;

            fieldCameraRuntimeData.IsOrthographic = camera.Lens.ModeOverride == LensSettings.OverrideModes.Orthographic;

            if (fieldCameraRuntimeData.IsOrthographic)
            {
                // Orthographic: orthographicSize = half-vertical height. We want paddedRadius to fit into half-diagonal.
                // A reasonable approach: desiredSize = paddedRadius (vertical-constrained), but if horizontal is tighter, account for the aspect.
                float desiredSizeByV = math.clamp(paddedRadius, fieldCamera.MinOrthographicSize, fieldCamera.MaxOrthographicSize);
                float desiredSizeByH = math.clamp(paddedRadius / aspect, fieldCamera.MinOrthographicSize, fieldCamera.MaxOrthographicSize);
                float desiredSize = Mathf.Max(desiredSizeByV, desiredSizeByH);

                camera.Lens.OrthographicSize =
                    Mathf.SmoothDamp(
                        camera.Lens.OrthographicSize,
                        desiredSize,
                        ref fieldCameraRuntimeData.VelocitySize,
                        fieldCamera.SmoothTime,
                        Mathf.Infinity,
                        deltaTime);

                // 위치는 fixed forward를 따라 centroid에서 일정 거리만큼 떨어뜨려 배치 (MinDistance 사용)
                Vector3 forward = fixedRot * Vector3.forward;
                float desiredDistance = Mathf.Clamp(fieldCamera.MinDistance, fieldCamera.MinDistance, fieldCamera.MaxDistance);
                Vector3 desiredPos = (Vector3)centroid - forward * desiredDistance;

                cameraTransform.position =
                    Vector3.SmoothDamp(
                        cameraTransform.position,
                        desiredPos,
                        ref velocityPos,
                        fieldCamera.SmoothTime,
                        Mathf.Infinity,
                        deltaTime);
            }
            else
            {
                // perspective: compute half FOVs from camera.fieldOfView (vertical FOV)
                float vFovRad = camera.Lens.FieldOfView * Mathf.Deg2Rad;
                float halfVFov = vFovRad * 0.5f;
                float halfHFov = Mathf.Atan(Mathf.Tan(halfVFov) * aspect);

                float desiredDistance;
                if (paddedRadius <= 0.0001f)
                {
                    desiredDistance = fieldCamera.MinDistance;
                }
                else
                {
                    float dV = paddedRadius / Mathf.Tan(halfVFov);
                    float dH = paddedRadius / Mathf.Tan(halfHFov);
                    desiredDistance = Mathf.Max(dV, dH);
                    desiredDistance = Mathf.Clamp(desiredDistance, fieldCamera.MinDistance, fieldCamera.MaxDistance);
                }

                // the desired position is centroid minus fixed forward * distance
                Vector3 forward = fixedRot * Vector3.forward;
                Vector3 desiredPos = (Vector3)centroid - forward * desiredDistance;

                cameraTransform.position =
                    Vector3.SmoothDamp(
                        cameraTransform.position,
                        desiredPos,
                        ref velocityPos,
                        fieldCamera.SmoothTime,
                        Mathf.Infinity,
                        deltaTime);
            }

            fieldCameraRuntimeData.VelocityPos = velocityPos;

            _fieldCameraQuery.SetSingleton(fieldCameraRuntimeData);
        }
    }
}