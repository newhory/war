using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;


namespace War.Dots.Component.ComponentSystem
{
    [UpdateInGroup(typeof(Group.TroopAISystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct TroopAISystem : ISystem
    {
        [BurstCompile]
        private partial struct SearchTargetJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter EntityCommandBuffer;


            public void Execute([EntityIndexInQuery] int entityIndex, Entity entity, in TargetForAttack targetForAttack)
            {
                if (targetForAttack.Target != Entity.Null)
                {
                    EntityCommandBuffer.SetComponentEnabled<AISearchTarget>(entityIndex, entity, false);
                    EntityCommandBuffer.SetComponentEnabled<AICheckTargetValid>(entityIndex, entity, true);
                }
            }
        }

        [BurstCompile]
        private partial struct CheckTargetValidJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter EntityCommandBuffer;

            [ReadOnly] public ComponentLookup<LocalTransform> LocalTransformLookup;


            public void Execute([EntityIndexInQuery] int entityIndex, Entity entity, ref Destination moveToDestination, in AICheckTargetValid checkTargetValid, in LocalTransform transform, in TargetForAttack targetForAttack)
            {
                if (targetForAttack.Target != Entity.Null)
                {
                    float3 otherPos = LocalTransformLookup[targetForAttack.Target].Position;

                    moveToDestination.Position = otherPos;

                    EntityCommandBuffer.SetComponentEnabled<StateMoveToTarget>(entityIndex, entity, true);
                }
                else
                {
                    EntityCommandBuffer.SetComponentEnabled<AISearchTarget>(entityIndex, entity, true);
                    EntityCommandBuffer.SetComponentEnabled<AICheckTargetValid>(entityIndex, entity, false);

                    EntityCommandBuffer.SetComponentEnabled<StateMoveToTarget>(entityIndex, entity, false);
                }
            }
        }

        [BurstCompile]
        private partial struct UpdateSoldierAISearchTargetJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter EntityCommandBuffer;

            [ReadOnly] public ComponentLookup<TargetForAttack> TargetForAttackLookup;


            public void Execute([EntityIndexInQuery] int entityIndex, Entity entity, in SoldierAttachedTroop soldierAttachedTroop)
            {
                if (TargetForAttackLookup.TryGetRefRO(soldierAttachedTroop.TroopEntity, out RefRO<TargetForAttack> refTargetForAttack) &&
                    refTargetForAttack.ValueRO.Target != Entity.Null)
                {
                    EntityCommandBuffer.SetComponentEnabled<AISearchTarget>(entityIndex, entity, true);
                }
            }
        }


        private EntityQuery _searchTargetQuery;
        private EntityQuery _checkTargetValidQuery;

        private ComponentLookup<LocalTransform> _localTransformLookup;

        private EntityQuery _targetCandidateSoldierQuery;


        public void OnCreate(ref SystemState state)
        {
            _searchTargetQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<Troop, TroopEntity, AISearchTarget, TargetForAttack>()
                    .WithDisabled<AICheckTargetValid>()
                    .Build();

            _checkTargetValidQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<Troop, TroopEntity, AICheckTargetValid, LocalTransform, TargetForAttack>()
                    .WithDisabled<AISearchTarget>()
                    .WithAllRW<Destination>()
                    .Build();

            _localTransformLookup = state.GetComponentLookup<LocalTransform>(true);
            
            _targetCandidateSoldierQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<Soldier, Troop, Alive, NavMeshAgentData, LocalTransform>()
                    .Build(); 
        }
        
        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            EntityCommandBuffer searchTargetEcb = new(Allocator.TempJob);
            new SearchTargetJob
                {
                    EntityCommandBuffer = searchTargetEcb.AsParallelWriter()
                }
                .ScheduleParallel(_searchTargetQuery, state.Dependency)
                .Complete();
            searchTargetEcb.Playback(state.EntityManager);
            searchTargetEcb.Dispose();

            _localTransformLookup.Update(ref state);

            EntityCommandBuffer checkTargetValidEcb = new(Allocator.TempJob);
            new CheckTargetValidJob
                {
                    EntityCommandBuffer = checkTargetValidEcb.AsParallelWriter(),

                    LocalTransformLookup = _localTransformLookup
                }
                .ScheduleParallel(_checkTargetValidQuery, state.Dependency)
                .Complete();
            checkTargetValidEcb.Playback(state.EntityManager);
            checkTargetValidEcb.Dispose();

            NativeArray<Entity> troopsHasTargetEntities = _checkTargetValidQuery.ToEntityArray(Allocator.TempJob);
            NativeArray<TargetForAttack> troopsHasTarget = _checkTargetValidQuery.ToComponentDataArray<TargetForAttack>(Allocator.TempJob);

            for (int i = 0, count = troopsHasTargetEntities.Length; i < count; ++i)
            {
                Troop targetTroop = state.EntityManager.GetSharedComponent<Troop>(troopsHasTarget[i].Target);
                
                _targetCandidateSoldierQuery.SetSharedComponentFilter(targetTroop);
                
                NativeArray<Entity> targetCandidateSoldierEntities = _targetCandidateSoldierQuery.ToEntityArray(Allocator.TempJob);
                int targetCandidateSoldierCount = targetCandidateSoldierEntities.Length;
                if (targetCandidateSoldierCount == 0)
                {
                    continue;
                }
                
                NativeArray<LocalTransform> targetCandidateSoldierPositions = _targetCandidateSoldierQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);
                NativeBitArray alreadyTargeted = new(targetCandidateSoldierCount, Allocator.TempJob);
                
                Troop troop = state.EntityManager.GetSharedComponent<Troop>(troopsHasTargetEntities[i]);
                
                using EntityCommandBuffer ecb = new(Allocator.TempJob);

                foreach (
                    (RefRW<TargetForAttack> refTargetForAttack, RefRO<LocalTransform> refSoldierPosition, Entity entity)
                    in
                    SystemAPI.Query<RefRW<TargetForAttack>, RefRO<LocalTransform>>()
                        .WithAll<Soldier, Alive, NavMeshAgentData>()
                        .WithSharedComponentFilter(troop)
                        .WithEntityAccess())
                {
                    if (refTargetForAttack.ValueRO.Target != Entity.Null)
                    {
                        continue;
                    }
                    
                    bool isFindTarget = false;
                    int targetIndex = 0;
                    float minDistance = float.MaxValue;

                    for (int j = 0; j < targetCandidateSoldierCount; j++)
                    {
                        if (alreadyTargeted.IsSet(j))
                        {
                            continue;
                        }

                        isFindTarget = true;

                        LocalTransform otherAgentData = targetCandidateSoldierPositions[j];
                        float dist = math.distance(refSoldierPosition.ValueRO.Position, otherAgentData.Position);
                        if (dist < minDistance)
                        {
                            targetIndex = j;
                            minDistance = dist;
                        }
                    }

                    if (!isFindTarget)
                    {
                        for (int j = 0; j < targetCandidateSoldierCount; j++)
                        {
                            LocalTransform otherAgentData = targetCandidateSoldierPositions[j];
                            float dist = math.distance(refSoldierPosition.ValueRO.Position, otherAgentData.Position);
                            if (dist < minDistance)
                            {
                                targetIndex = j;
                                minDistance = dist;
                            }
                        }
                    }
                    else
                    {
                        alreadyTargeted.Set(targetIndex, true);
                    }
                    
                    refTargetForAttack.ValueRW.Target = targetCandidateSoldierEntities[targetIndex];
                    
                    ecb.SetComponentEnabled<AISearchTarget>(entity, false);
                    ecb.SetComponentEnabled<AICheckTargetValid>(entity, true);
                }
                
                ecb.Playback(state.EntityManager);
            }
        }
    }
}