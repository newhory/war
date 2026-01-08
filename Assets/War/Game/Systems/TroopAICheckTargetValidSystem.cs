using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace War.Game.Systems
{
    [UpdateInGroup(typeof(Group.AISystemGroup))]
    [UpdateAfter(typeof(TroopAISearchTargetSystem))]
    [UpdateBefore(typeof(TroopAISystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct TroopAICheckTargetValidSystem : ISystem
    {
        [BurstCompile]
        private partial struct CheckTargetValidJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<Alive> AliveLookup;


            private void Execute(ref TroopTargetForAttack targetForAttack)
            {
                if (targetForAttack.TargetTroop == Entity.Null)
                {
                    return;
                }

                if (!AliveLookup.HasComponent(targetForAttack.TargetTroop) ||
                    !AliveLookup.IsComponentEnabled(targetForAttack.TargetTroop))
                {
                    targetForAttack.TargetTroop = Entity.Null;
                }
            }
        }


        private EntityQuery _checkTargetValidQuery;
        private ComponentLookup<Alive> _aliveLookup;


        public void OnCreate(ref SystemState state)
        {
            _checkTargetValidQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<Troop, Alive, TroopEntity, TroopAICheckTargetValid>()
                    .WithAllRW<TroopTargetForAttack>()
                    .Build();

            _aliveLookup = state.GetComponentLookup<Alive>(true);
        }
        
        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            _aliveLookup.Update(ref state);

            state.Dependency = new CheckTargetValidJob { AliveLookup = _aliveLookup }.ScheduleParallel(_checkTargetValidQuery, state.Dependency);
        }
    }
}