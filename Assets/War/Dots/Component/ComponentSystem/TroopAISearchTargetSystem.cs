using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;
using Unity.Mathematics;


namespace War.Dots.Component.ComponentSystem
{
    [UpdateInGroup(typeof(Group.AISystemGroup))]
    [UpdateBefore(typeof(TroopAISystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct TroopAISearchTargetSystem : ISystem
    {
        [BurstCompile]
        private partial struct SearchTargetJob : IJobEntity
        {
            [ReadOnly] public NativeArray<Entity>.ReadOnly TargetCandidateTroopEntities;
            [ReadOnly] public NativeArray<Team>.ReadOnly TargetCandidateTroopTeams;
            [ReadOnly] public NativeArray<LocalTransform>.ReadOnly TargetCandidateTroopTransforms;


            private void Execute(Entity entity, ref TroopTargetForAttack targetForAttack, in LocalTransform localTransform, in Team team, in SearchTargetRange searchTargetRange)
            {
                if (targetForAttack.TargetTroop != Entity.Null ||
                    TargetCandidateTroopEntities.Length == 0)
                {
                    return;
                }

                float range = searchTargetRange.Value;
                float3 pos = localTransform.Position;

                float minDistance = float.MaxValue;
                Entity target = Entity.Null;

                for (int i = 0, targetCandidateTroopCount = TargetCandidateTroopEntities.Length; i < targetCandidateTroopCount; i++)
                {
                    Entity targetCandidateTroopEntity = TargetCandidateTroopEntities[i];
                    if (targetCandidateTroopEntity == entity ||
                        TargetCandidateTroopTeams[i].Color == team.Color)
                    {
                        continue;
                    }

                    float dist = math.distance(pos, TargetCandidateTroopTransforms[i].Position);
                    if (dist < range && dist < minDistance)
                    {
                        target = targetCandidateTroopEntity;
                        minDistance = dist;
                    }
                }

                targetForAttack.TargetTroop = target;
            }
        }


        private EntityQuery _searchTargetTroopQuery;
        private EntityQuery _targetCandidateTroopQuery;


        public void OnCreate(ref SystemState state)
        {
            _searchTargetTroopQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<Troop, Alive, TroopEntity, TroopAISearchTarget, LocalTransform, Team, SearchTargetRange>()
                    .WithAllRW<TroopTargetForAttack>()
                    .Build();

            _targetCandidateTroopQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<Troop, Alive, TroopEntity, Team, LocalTransform>()
                    .Build();
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            JobHandle dependency = state.Dependency;

            NativeArray<Entity> targetCandidateEntities = _targetCandidateTroopQuery.ToEntityArray(Allocator.TempJob);
            NativeArray<Team> targetCandidateTroopTeams = _targetCandidateTroopQuery.ToComponentDataArray<Team>(Allocator.TempJob);
            NativeArray<LocalTransform> targetCandidateTroopTransforms = _targetCandidateTroopQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);

            dependency =
                new SearchTargetJob
                    {
                        TargetCandidateTroopEntities = targetCandidateEntities.AsReadOnly(),
                        TargetCandidateTroopTeams = targetCandidateTroopTeams.AsReadOnly(),
                        TargetCandidateTroopTransforms = targetCandidateTroopTransforms.AsReadOnly(),
                    }
                    .ScheduleParallel(_searchTargetTroopQuery, dependency);

            state.Dependency =
                JobHandle.CombineDependencies(
                    targetCandidateEntities.Dispose(dependency),
                    targetCandidateTroopTeams.Dispose(dependency),
                    targetCandidateTroopTransforms.Dispose(dependency));
        }
    }
}