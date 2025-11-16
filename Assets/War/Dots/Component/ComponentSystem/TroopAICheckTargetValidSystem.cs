using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;


namespace War.Dots.Component.ComponentSystem
{
    [UpdateInGroup(typeof(Group.TroopAISystemGroup))]
    [UpdateAfter(typeof(TroopAISearchTargetSystem))]
    [UpdateBefore(typeof(TroopAISystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct TroopAICheckTargetValidSystem : ISystem
    {
        [BurstCompile]
        private partial struct CheckTargetValidJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<LocalTransform> LocalTransformLookup;


            public void Execute(ref TargetForAttack targetForAttack)
            {
                if (targetForAttack.Target == Entity.Null)
                {
                    return;
                }

                if (!LocalTransformLookup.EntityExists(targetForAttack.Target))
                {
                    targetForAttack.Target = Entity.Null;
                }
            }
        }


        private EntityQuery _checkTargetValidQuery;
        private ComponentLookup<LocalTransform> _localTransformLookup;


        public void OnCreate(ref SystemState state)
        {
            _checkTargetValidQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<Troop, TroopEntity, AICheckTargetValid>()
                    .WithAllRW<TargetForAttack>()
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