using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;


namespace War.Dots.Component.ComponentSystem
{
    [UpdateInGroup(typeof(Group.JustSpawnedInitializeSystemGroup))]
    [UpdateAfter(typeof(FormationUnitIndexingSystem))]
    [UpdateBefore(typeof(SoldierAddPresentationSystem))]
    public partial struct SoldierInitialPositionSystem : ISystem
    {
        [BurstCompile]
        private partial struct CollectPositionJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<LocalTransform> LocalTransformLookup;

            public NativeArray<float3> Positions;


            public void Execute([EntityIndexInQuery] int index, in FormationUnit formationUnit)
            {
                if (!LocalTransformLookup.TryGetComponent(formationUnit.FormationEntity, out LocalTransform formationLocalTransform))
                {
                    return;
                }

                Positions[index] = formationLocalTransform.TransformPoint(formationUnit.LocalPositionInFormation);
            }
        }

        [BurstCompile]
        private partial struct UpdatePositionJob : IJobEntity
        {
            [ReadOnly] public NativeArray<float3>.ReadOnly Positions;


            public void Execute([EntityIndexInQuery] int index, ref LocalTransform localTransform) => localTransform.Position = Positions[index];
        }


        private EntityQuery _formationUnitQuery;
        private ComponentLookup<LocalTransform> _localTransformLookup;


        public void OnCreate(ref SystemState state)
        {
            _formationUnitQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<Soldier, Alive, Formation, FormationUnit>()
                    .WithAllRW<LocalTransform>()
                    .WithNone<UnityAnimator, UnityNavMeshAgent>()
                    .Build();

            _localTransformLookup = state.GetComponentLookup<LocalTransform>(true);
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            _localTransformLookup.Update(ref state);

            JobHandle dependency = state.Dependency;

            NativeArray<float3> positions = new(_formationUnitQuery.CalculateEntityCount(), Allocator.TempJob);

            dependency = new CollectPositionJob { LocalTransformLookup = _localTransformLookup, Positions = positions }.ScheduleParallel(_formationUnitQuery, dependency);
            dependency = new UpdatePositionJob { Positions = positions.AsReadOnly() }.ScheduleParallel(_formationUnitQuery, dependency);

            dependency = positions.Dispose(dependency);

            state.Dependency = dependency;
        }
    }
}