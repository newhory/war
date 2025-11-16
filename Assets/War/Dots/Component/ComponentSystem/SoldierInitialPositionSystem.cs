using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;


namespace War.Dots.Component.ComponentSystem
{
    [UpdateAfter(typeof(TransformSystemGroup))]
    [UpdateBefore(typeof(Group.ViewSystemGroup))]
    public partial struct SoldierInitialPositionSystem : ISystem
    {
        [BurstCompile]
        private partial struct UpdatePositionJob : IJobEntity
        {
            [ReadOnly] public LocalTransform FormationLocalTransform;


            public void Execute(ref LocalTransform localTransform, in FormationUnit formationUnit) => localTransform.Position = FormationLocalTransform.TransformPoint(formationUnit.LocalPositionInFormation);
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
                    .WithAll<Soldier, Alive, Formation, FormationUnit>()
                    .WithAllRW<LocalTransform>()
                    .WithNone<UnityAnimator, UnityNavMeshAgent>()
                    .Build();
        }
        
        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            state.Dependency.Complete();
            
            state.EntityManager.GetAllUniqueSharedComponents(out NativeList<Formation> formations, Allocator.TempJob);
            if (formations.Length > 0)
            {
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

                    new UpdatePositionJob
                        {
                            FormationLocalTransform = _formationQuery.GetSingleton<LocalTransform>(),
                        }
                        .ScheduleParallel(_formationUnitQuery, state.Dependency)
                        .Complete();
                }
            }
            
            formations.Dispose();
        }
    }
}