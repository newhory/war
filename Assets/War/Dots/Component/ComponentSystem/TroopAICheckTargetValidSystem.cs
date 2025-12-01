using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;


namespace War.Dots.Component.ComponentSystem
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
            [ReadOnly] public ComponentLookup<LocalTransform> LocalTransformLookup;


            private void Execute(ref TroopTargetForAttack targetForAttack)
            {
                if (targetForAttack.TargetTroop == Entity.Null)
                {
                    return;
                }

                if (!LocalTransformLookup.EntityExists(targetForAttack.TargetTroop))
                {
                    targetForAttack.TargetTroop = Entity.Null;
                }
            }
        }


        private EntityQuery _checkTargetValidQuery;
        private ComponentLookup<LocalTransform> _localTransformLookup;


        public void OnCreate(ref SystemState state)
        {
            _checkTargetValidQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<Troop, Alive, TroopEntity, TroopAICheckTargetValid>()
                    .WithAllRW<TroopTargetForAttack>()
                    .Build();

            _localTransformLookup = state.GetComponentLookup<LocalTransform>(true);
        }
        
        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            _localTransformLookup.Update(ref state);

            state.Dependency = new CheckTargetValidJob { LocalTransformLookup = _localTransformLookup }.ScheduleParallel(_checkTargetValidQuery, state.Dependency);
        }
    }
}