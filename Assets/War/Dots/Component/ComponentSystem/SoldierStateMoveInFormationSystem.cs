using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;


namespace War.Dots.Component.ComponentSystem
{
    [UpdateInGroup(typeof(Group.SoldierStateSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct SoldierStateMoveInFormationSystem : ISystem
    {
        [BurstCompile]
        private partial struct UpdateDestinationJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<LocalTransform> LocalTransformLookup;


            public void Execute(ref Destination destination, in FormationUnit formationUnit)
            {
                if (!LocalTransformLookup.TryGetComponent(formationUnit.FormationEntity, out LocalTransform formationLocalTransform))
                {
                    return;
                }

                destination.Position = formationLocalTransform.TransformPoint(formationUnit.LocalPositionInFormation);
            }
        }


        private EntityQuery _formationUnitQuery;
        private ComponentLookup<LocalTransform> _localTransformLookup;


        public void OnCreate(ref SystemState state)
        {
            _formationUnitQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<Soldier, Alive, SoldierStateMoveInFormation, Formation, FormationUnit>()
                    .WithAllRW<Destination>()
                    .Build();

            _localTransformLookup = state.GetComponentLookup<LocalTransform>(true);
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            _localTransformLookup.Update(ref state);

            state.Dependency = new UpdateDestinationJob { LocalTransformLookup = _localTransformLookup }.ScheduleParallel(_formationUnitQuery, state.Dependency);
        }
    }
}