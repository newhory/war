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
            [ReadOnly] public LocalTransform FormationLocalTransform;


            public void Execute(ref Destination destination, in FormationUnit formationUnit) =>
                destination.Position = FormationLocalTransform.TransformPoint(formationUnit.LocalPositionInFormation);
        }


        private EntityQuery _formationQuery;
        private EntityQuery _formationUnitQuery;


        public void OnCreate(ref SystemState state)
        {
            _formationQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<FormationEntity, Formation, LocalTransform>()
                    .Build();

            _formationUnitQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<Soldier, Alive, StateMoveInFormation, Formation, FormationUnit>()
                    .WithAllRW<Destination>()
                    .Build();
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            state.EntityManager.GetAllUniqueSharedComponents(out NativeList<Formation> formations, Allocator.TempJob);
            if (formations.Length == 0)
            {
                formations.Dispose();
                return;
            }

            foreach (Formation formation in formations)
            {
                _formationQuery.SetSharedComponentFilter(formation);
                if (_formationQuery.CalculateEntityCount() != 1)
                {
                    continue;
                }

                _formationUnitQuery.SetSharedComponentFilter(formation);

                int formationUnitEntityCount = _formationUnitQuery.CalculateEntityCount();
                if (formationUnitEntityCount < 1)
                {
                    continue;
                }

                new UpdateDestinationJob
                    {
                        FormationLocalTransform = _formationQuery.GetSingleton<LocalTransform>(),
                    }
                    .ScheduleParallel(_formationUnitQuery, state.Dependency)
                    .Complete();
            }

            formations.Dispose();
        }
    }
}