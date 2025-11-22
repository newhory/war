using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;


namespace War.Dots.Component.ComponentSystem
{
    [UpdateInGroup(typeof(Group.TroopAISystemGroup))]
    [UpdateBefore(typeof(TroopAISystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct TroopAISearchTargetSystem : ISystem
    {
        [BurstCompile]
        private partial struct SearchTargetJob : IJobEntity
        {
            [ReadOnly] public NativeArray<Entity>.ReadOnly TargetEntities;
            [ReadOnly] public ComponentLookup<LocalTransform> LocalTransformLookup;
            [ReadOnly] public ComponentLookup<Team> TeamLookup;


            public void Execute(Entity entity, ref TroopTargetForAttack targetForAttack, in LocalTransform localTransform, in Team team, in SearchTargetRange searchTargetRange)
            {
                float range = searchTargetRange.Value;
                float3 pos = localTransform.Position;

                float minDistance = float.MaxValue;
                Entity target = Entity.Null;

                foreach (Entity otherEntity in TargetEntities)
                {
                    if (otherEntity == entity ||
                        TeamLookup[otherEntity].Color == team.Color)
                    {
                        continue;
                    }

                    float3 otherPos = LocalTransformLookup[otherEntity].Position;

                    float dist = math.distance(pos, otherPos);
                    if (dist < range && dist < minDistance)
                    {
                        target = otherEntity;
                        minDistance = dist;
                    }
                }

                targetForAttack.TargetTroop = target;
            }
        }

        private EntityQuery _searchTargetQuery;
        private EntityQuery _targetQuery;

        private ComponentLookup<LocalTransform> _localTransformLookup;
        private ComponentLookup<Team> _teamLookup;


        public void OnCreate(ref SystemState state)
        {
            _searchTargetQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<Troop, TroopEntity, TroopAISearchTarget, LocalTransform, Team, SearchTargetRange>()
                    .WithAllRW<TroopTargetForAttack>()
                    .Build();

            _targetQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<Troop, TroopEntity, Team, LocalTransform>()
                    .Build();

            _localTransformLookup = state.GetComponentLookup<LocalTransform>(true);
            _teamLookup = state.GetComponentLookup<Team>(true);
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            _localTransformLookup.Update(ref state);
            _teamLookup.Update(ref state);

            NativeArray<Entity> targetEntities = _targetQuery.ToEntityArray(Allocator.TempJob);

            state.Dependency =
                new SearchTargetJob
                    {
                        TargetEntities = targetEntities.AsReadOnly(),
                        LocalTransformLookup = _localTransformLookup,
                        TeamLookup = _teamLookup,
                    }
                    .ScheduleParallel(_searchTargetQuery, state.Dependency);

            targetEntities.Dispose(state.Dependency);
        }
    }
}