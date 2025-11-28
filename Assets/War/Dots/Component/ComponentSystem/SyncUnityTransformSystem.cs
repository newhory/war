using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine.Jobs;
using ZLinq;


namespace War.Dots.Component.ComponentSystem
{
    [UpdateInGroup(typeof(Group.ViewSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct SyncUnityTransformSystem : ISystem
    {
        private struct SyncTransformJob : IJobParallelForTransform
        {
            [ReadOnly] public NativeArray<LocalTransform> LocalTransforms;


            public void Execute(int index, TransformAccess transform)
            {
                LocalTransform localTransform = LocalTransforms[index];

                transform.SetLocalPositionAndRotation(localTransform.Position, localTransform.Rotation);
                transform.localScale = new float3(localTransform.Scale);
            }
        }


        private EntityQuery _syncTransformQuery;


        public void OnCreate(ref SystemState state) =>
            _syncTransformQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<UnityTransform, LocalTransform>()
                    .Build();

        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            NativeArray<LocalTransform> localTransforms = _syncTransformQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);
            NativeArray<UnityTransform> unityTransforms = _syncTransformQuery.ToComponentDataArray<UnityTransform>(Allocator.TempJob);

            JobHandle dependency = state.Dependency;

            dependency =
                new SyncTransformJob
                    {
                        LocalTransforms = localTransforms
                    }
                    .Schedule(
                        new TransformAccessArray(unityTransforms.AsValueEnumerable().Select(unityTransform => unityTransform.Transform.Value).ToArray()),
                        dependency);

            dependency = JobHandle.CombineDependencies(localTransforms.Dispose(dependency), unityTransforms.Dispose(dependency));

            state.Dependency = dependency;
        }
    }
}