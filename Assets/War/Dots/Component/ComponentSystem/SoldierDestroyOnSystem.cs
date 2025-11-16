using Unity.Burst;
using Unity.Collections;
using Unity.Entities;


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
            double currentTime = SystemAPI.Time.ElapsedTime;

            NativeParallelHashSet<int> needToUpdateFormationIds = new(100, Allocator.TempJob);

            new CatchNeedToUpdateFormationId
                {
                    NeedToUpdateFormationIds = needToUpdateFormationIds.AsParallelWriter(),

                    CurrentTime = currentTime,
                }
                .ScheduleParallel(_destroyOnSoldierQuery, state.Dependency)
                .Complete();

            if (!needToUpdateFormationIds.IsEmpty)
            {
                DynamicBuffer<ResetFormationUnitIndex> resetFormationUnitIndexBuffer = FormationUnitIndexingSystem.GetResetFormationUnitIndexBuffer(state.EntityManager);

                foreach (int formationId in needToUpdateFormationIds)
                {
                    resetFormationUnitIndexBuffer.Add(new ResetFormationUnitIndex { Formation = new Formation { Id = formationId } });
                }
            }

            needToUpdateFormationIds.Dispose();
        }
    }
}