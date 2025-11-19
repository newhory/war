using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;


namespace War.Dots.Component.ComponentSystem
{
    [UpdateInGroup(typeof(Group.DestroyOnSystemGroup))]
    [UpdateBefore(typeof(PooledGameObjectDestroyOnSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct SoldierDestroyOnSystem : ISystem
    {
        [BurstCompile]
        private partial struct CatchNeedToUpdateFormationId : IJobEntity
        {
            public NativeParallelHashSet<int>.ParallelWriter NeedToUpdateFormationIds;

            [ReadOnly] public double CurrentTime;


            public void Execute(in DestroyOn destroyOn, in Formation formation)
            {
                if (CurrentTime >= destroyOn.DestroyTime)
                {
                    NeedToUpdateFormationIds.Add(formation.Id);
                }
            }
        }

        [BurstCompile]
        private struct AddResetFormationUnitIndex : IJob
        {
            [ReadOnly] public NativeParallelHashSet<int>.ReadOnly NeedToUpdateFormationIds;

            public DynamicBuffer<ResetFormationUnitIndex> ResetFormationUnitIndexBuffer;


            public void Execute()
            {
                if (NeedToUpdateFormationIds.IsEmpty)
                {
                    return;
                }

                foreach (int formationId in NeedToUpdateFormationIds)
                {
                    ResetFormationUnitIndexBuffer.Add(new ResetFormationUnitIndex { Formation = new Formation { Id = formationId } });
                }
            }
        }


        private EntityQuery _destroyOnSoldierQuery;


        public void OnCreate(ref SystemState state) =>
            _destroyOnSoldierQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<Soldier, DestroyOn, Formation>()
                    .Build();

        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            JobHandle dependency = state.Dependency;

            NativeParallelHashSet<int> needToUpdateFormationIds = new(100, Allocator.TempJob);

            dependency =
                new CatchNeedToUpdateFormationId
                    {
                        NeedToUpdateFormationIds = needToUpdateFormationIds.AsParallelWriter(),

                        CurrentTime = SystemAPI.Time.ElapsedTime,
                    }
                    .ScheduleParallel(_destroyOnSoldierQuery, dependency);

            dependency =
                new AddResetFormationUnitIndex
                    {
                        NeedToUpdateFormationIds = needToUpdateFormationIds.AsReadOnly(),

                        ResetFormationUnitIndexBuffer = FormationUnitIndexingSystem.GetResetFormationUnitIndexBuffer(state.EntityManager)
                    }
                    .Schedule(dependency);

            needToUpdateFormationIds.Dispose(dependency);
            
            dependency.Complete();
        }
    }
}