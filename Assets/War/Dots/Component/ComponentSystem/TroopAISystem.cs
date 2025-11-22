using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;
using Unity.Mathematics;


namespace War.Dots.Component.ComponentSystem
{
    [UpdateInGroup(typeof(Group.TroopAISystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct TroopAISystem : ISystem
    {
        [BurstCompile]
        private partial struct CheckTargetValidJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter EntityCommandBuffer;

            [ReadOnly] public ComponentLookup<LocalTransform> LocalTransformLookup;


            public void Execute(Entity entity, ref Destination moveToDestination, in TroopTargetForAttack targetForAttack)
            {
                if (targetForAttack.TargetTroop != Entity.Null)
                {
                    float3 otherPos = LocalTransformLookup[targetForAttack.TargetTroop].Position;

                    moveToDestination.Position = otherPos;

                    EntityCommandBuffer.SetComponentEnabled<TroopAISearchTarget>(entity.Index, entity, false);
                    EntityCommandBuffer.SetComponentEnabled<TroopAICheckTargetValid>(entity.Index, entity, true);

                    EntityCommandBuffer.SetComponentEnabled<StateMoveToTarget>(entity.Index, entity, true);
                }
                else
                {
                    EntityCommandBuffer.SetComponentEnabled<TroopAISearchTarget>(entity.Index, entity, true);
                    EntityCommandBuffer.SetComponentEnabled<TroopAICheckTargetValid>(entity.Index, entity, false);

                    EntityCommandBuffer.SetComponentEnabled<StateMoveToTarget>(entity.Index, entity, false);
                }
            }
        }

        [BurstCompile]
        private partial struct CollectTroopSoldierEntityJob : IJobEntity
        {
            public NativeParallelMultiHashMap<Entity, TroopSoldier>.ParallelWriter SoldiersByTroop;


            public void Execute(Entity soldierEntity, in SoldierAttachedTroop soldierAttachedTroop, in LocalTransform localTransform) =>
                SoldiersByTroop.Add(soldierAttachedTroop.TroopEntity, new TroopSoldier { Entity = soldierEntity, Position = localTransform.Position });
        }

        [BurstCompile]
        private partial struct FillTroopSoldierEntityBufferJob : IJobEntity
        {
            [ReadOnly] public NativeParallelMultiHashMap<Entity, TroopSoldier>.ReadOnly SoldiersByTroop;


            public void Execute(Entity troopEntity, DynamicBuffer<TroopSoldierEntity> troopSoldierEntities)
            {
                troopSoldierEntities.Clear();

                if (!SoldiersByTroop.TryGetFirstValue(troopEntity, out TroopSoldier soldierEntity, out NativeParallelMultiHashMapIterator<Entity> iterator))
                {
                    return;
                }

                do
                {
                    troopSoldierEntities.Add(new TroopSoldierEntity { Soldier = soldierEntity });
                } while (SoldiersByTroop.TryGetNextValue(out soldierEntity, ref iterator));
            }
        }

        private readonly struct TroopSoldierDistanceComparer : IComparer<TroopSoldier>
        {
            private readonly float3 _position;
            private readonly NativeList<TroopSoldier> _troopSoldiers;


            public TroopSoldierDistanceComparer(float3 position, NativeList<TroopSoldier> troopSoldiers)
            {
                _position = position;
                _troopSoldiers = troopSoldiers;
            }


            public int Compare(TroopSoldier ia, TroopSoldier ib)
            {
                float3 a = ia.Position;
                float3 b = ib.Position;

                float distanceToA = math.distance(a, _position);
                float distanceToB = math.distance(b, _position);

                if (distanceToA < distanceToB) return -1;
                if (distanceToA > distanceToB) return 1;

                return 0;
            }
        }

        [BurstCompile]
        private partial struct ChangeTroopSoldiersAggressiveJob : IJobEntity
        {
            [ReadOnly] public NativeParallelMultiHashMap<Entity, TroopSoldier>.ReadOnly SoldiersByTroop;
            [ReadOnly] public ComponentLookup<SoldierTargetForAttack> SoldierTargetForAttackLookup;

            public EntityCommandBuffer.ParallelWriter EntityCommandBuffer;
            public NativeParallelHashSet<int>.ParallelWriter AlreadyTargetedSoldierEntityIndices;


            public void Execute(DynamicBuffer<TroopSoldierEntity> troopSoldierEntities, in TroopTargetForAttack targetForAttack)
            {
                if (targetForAttack.TargetTroop == Entity.Null ||
                    !SoldiersByTroop.TryGetFirstValue(targetForAttack.TargetTroop, out TroopSoldier targetCandidateSoldier, out NativeParallelMultiHashMapIterator<Entity> iterator))
                {
                    return;
                }

                NativeList<TroopSoldier> targetCandidateSoldiers = new(Allocator.TempJob);
                do
                {
                    targetCandidateSoldiers.Add(targetCandidateSoldier);
                } while (SoldiersByTroop.TryGetNextValue(out targetCandidateSoldier, ref iterator));

                for (int i = 0, count = troopSoldierEntities.Length; i < count; ++i)
                {
                    TroopSoldier troopSoldier = troopSoldierEntities[i].Soldier;

                    Entity soldierEntity = troopSoldier.Entity;
                    SoldierTargetForAttack soldierTargetForAttack = SoldierTargetForAttackLookup[soldierEntity];

                    if (soldierTargetForAttack.TargetSoldier != Entity.Null)
                    {
                        continue;
                    }

                    Entity targetEntity = Entity.Null;

                    targetCandidateSoldiers.Sort(new TroopSoldierDistanceComparer(troopSoldier.Position, targetCandidateSoldiers));

                    for (int j = 0, targetCandidateSoldierCount = targetCandidateSoldiers.Length; j < targetCandidateSoldierCount; j++)
                    {
                        Entity candidateSoldierEntity = targetCandidateSoldiers[j].Entity;

                        if (AlreadyTargetedSoldierEntityIndices.Add(candidateSoldierEntity.Index))
                        {
                            targetEntity = candidateSoldierEntity;

                            break;
                        }
                    }

                    if (targetEntity == Entity.Null)
                    {
                        targetEntity = targetCandidateSoldiers[0].Entity;
                    }

                    soldierTargetForAttack.TargetSoldier = targetEntity;
                    EntityCommandBuffer.SetComponent(soldierEntity.Index, soldierEntity, soldierTargetForAttack);

                    EntityCommandBuffer.SetComponentEnabled<SoldierAISearchTarget>(soldierEntity.Index, soldierEntity, false);
                    EntityCommandBuffer.SetComponentEnabled<SoldierAICheckTargetValid>(soldierEntity.Index, soldierEntity, true);
                }

                targetCandidateSoldiers.Dispose();
            }
        }


        private EntityQuery _checkTargetValidQuery;
        private EntityQuery _aliveSoldierQuery;
        private EntityQuery _allTroopQuery;
        private EntityQuery _aggressiveTroopQuery;

        private ComponentLookup<LocalTransform> _localTransformLookup;
        private ComponentLookup<SoldierTargetForAttack> _soldierTargetForAttackLookup;

        public void OnCreate(ref SystemState state)
        {
            _checkTargetValidQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<Troop, TroopEntity, TroopTargetForAttack, TroopSoldierEntity>()
                    .WithAny<TroopAICheckTargetValid, TroopAISearchTarget>()
                    .WithAllRW<Destination>()
                    .Build();

            _aliveSoldierQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<Soldier, Troop, Alive, NavMeshAgentData, LocalTransform, SoldierAttachedTroop>()
                    .Build();

            _allTroopQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<Troop, TroopEntity, TroopSoldierEntity, TroopTargetForAttack>()
                    .Build();

            _aggressiveTroopQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<Troop, TroopEntity, TroopSoldierEntity, TroopTargetForAttack>()
                    .WithAll<TroopAICheckTargetValid>()
                    .Build();

            _localTransformLookup = state.GetComponentLookup<LocalTransform>(true);
            _soldierTargetForAttackLookup = state.GetComponentLookup<SoldierTargetForAttack>(true);
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
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

            _soldierTargetForAttackLookup.Update(ref state);

            NativeParallelMultiHashMap<Entity, TroopSoldier> soldiersByTroop = new(_aliveSoldierQuery.CalculateEntityCount(), Allocator.TempJob);
            NativeParallelHashSet<int> alreadyTargetedSoldierEntityIndices = new(_aliveSoldierQuery.CalculateEntityCount(), Allocator.TempJob);

            JobHandle dependency = state.Dependency;
            dependency = new CollectTroopSoldierEntityJob { SoldiersByTroop = soldiersByTroop.AsParallelWriter() }.ScheduleParallel(_aliveSoldierQuery, dependency);
            dependency = new FillTroopSoldierEntityBufferJob { SoldiersByTroop = soldiersByTroop.AsReadOnly() }.ScheduleParallel(_allTroopQuery, dependency);

            EntityCommandBuffer ecbChangeTroopSoldiersAggressive = new(Allocator.TempJob);
            new ChangeTroopSoldiersAggressiveJob
                {
                    SoldiersByTroop = soldiersByTroop.AsReadOnly(),
                    SoldierTargetForAttackLookup = _soldierTargetForAttackLookup,

                    EntityCommandBuffer = ecbChangeTroopSoldiersAggressive.AsParallelWriter(),
                    AlreadyTargetedSoldierEntityIndices = alreadyTargetedSoldierEntityIndices.AsParallelWriter(),
                }
                .ScheduleParallel(_aggressiveTroopQuery, dependency)
                .Complete();
            ecbChangeTroopSoldiersAggressive.Playback(state.EntityManager);
            ecbChangeTroopSoldiersAggressive.Dispose();

            alreadyTargetedSoldierEntityIndices.Dispose();
            soldiersByTroop.Dispose();
        }
    }
}