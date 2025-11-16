using Unity.Burst;
using Unity.Collections;
using Unity.Entities;


namespace War.Dots.Component.ComponentSystem
{
    [UpdateInGroup(typeof(Group.SoldierAISystemGroup))]
    [UpdateAfter(typeof(SoldierAISearchTargetSystem))]
    [UpdateBefore(typeof(SoldierAISystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct SoldierAICheckTargetValidSystem : ISystem
    {
        [BurstCompile]
        private partial struct CheckTargetValidJob : IJobEntity
        {
            [ReadOnly] public BufferLookup<Damaged> DamagedLookup;


            public void Execute(ref TargetForAttack targetForAttack)
            {
                if (targetForAttack.Target == Entity.Null)
                {
                    return;
                }

                if (!DamagedLookup.HasBuffer(targetForAttack.Target))
                {
                    targetForAttack.Target = Entity.Null;
                }
            }
        }


        private EntityQuery _checkTargetValidQuery;
        private BufferLookup<Damaged> _damagedLookup;


        public void OnCreate(ref SystemState state)
        {
            _checkTargetValidQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<Soldier, Alive, AICheckTargetValid>()
                    .WithAllRW<TargetForAttack>()
                    .Build();
            
            _damagedLookup = state.GetBufferLookup<Damaged>(true);
        }
        
        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            _damagedLookup.Update(ref state);

            state.Dependency =
                new CheckTargetValidJob
                    {
                        DamagedLookup = _damagedLookup
                    }
                    .ScheduleParallel(_checkTargetValidQuery, state.Dependency);
        }
    }
}