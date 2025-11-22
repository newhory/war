using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;
using Unity.Mathematics;


namespace War.Dots.Component.ComponentSystem
{
    [UpdateInGroup(typeof(Group.AISystemGroup))]
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

                    EntityCommandBuffer.SetComponentEnabled<TroopStateMoveToTarget>(entity.Index, entity, true);
                }
                else
                {
                    EntityCommandBuffer.SetComponentEnabled<TroopAISearchTarget>(entity.Index, entity, true);
                    EntityCommandBuffer.SetComponentEnabled<TroopAICheckTargetValid>(entity.Index, entity, false);

                    EntityCommandBuffer.SetComponentEnabled<TroopStateMoveToTarget>(entity.Index, entity, false);
                }
            }
        }

        [BurstCompile]
        private partial struct CollectSoldiersByTroopJob : IJobEntity
        {
            public NativeParallelMultiHashMap<Entity, TroopSoldier>.ParallelWriter SoldiersByTroop;


            public void Execute(Entity soldierEntity, in SoldierAttachedTroop soldierAttachedTroop, in LocalTransform localTransform) =>
                SoldiersByTroop.Add(soldierAttachedTroop.TroopEntity, new TroopSoldier { Entity = soldierEntity, Position = localTransform.Position });
        }

        private readonly struct TroopSoldierComparer : IComparer<TroopSoldier>
        {
            private readonly float3 _position;


            public TroopSoldierComparer(float3 position)
            {
                _position = position;
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
        private partial struct SetSoldierStateByTroopJob : IJobEntity
        {
            [ReadOnly] public NativeParallelMultiHashMap<Entity, TroopSoldier>.ReadOnly SoldiersByTroop;
            [ReadOnly] public ComponentLookup<TroopTargetForAttack> TroopTargetForAttackLookup;

            public NativeParallelHashSet<int>.ParallelWriter AlreadyTargetedSoldierEntityIndices;
            public EntityCommandBuffer.ParallelWriter EntityCommandBuffer;


            public void Execute(Entity soldierEntity, ref SoldierTargetForAttack soldierTargetForAttack, ref SoldierAnimation soldierAnimation, in SoldierAttachedTroop soldierAttachedTroop, in LocalTransform localTransform)
            {
                if (!TroopTargetForAttackLookup.TryGetRefRO(soldierAttachedTroop.TroopEntity, out RefRO<TroopTargetForAttack> troopTargetForAttack) ||
                    troopTargetForAttack.ValueRO.TargetTroop == Entity.Null ||
                    !SoldiersByTroop.TryGetFirstValue(troopTargetForAttack.ValueRO.TargetTroop, out TroopSoldier targetCandidateSoldier, out NativeParallelMultiHashMapIterator<Entity> iterator))
                {
                    soldierAnimation.Next = SoldierAnimation.State.Default;
                    
                    soldierTargetForAttack.TargetSoldier = Entity.Null;

                    EntityCommandBuffer.SetComponentEnabled<SoldierStateMoveInFormation>(soldierEntity.Index, soldierEntity, true);
                    EntityCommandBuffer.SetComponentEnabled<SoldierStateMoveToTarget>(soldierEntity.Index, soldierEntity, false);
                    EntityCommandBuffer.SetComponentEnabled<SoldierStateAttackTarget>(soldierEntity.Index, soldierEntity, false);
                    
                    EntityCommandBuffer.SetComponentEnabled<Movable>(soldierEntity.Index, soldierEntity, true);
                    EntityCommandBuffer.SetComponentEnabled<Rotatable>(soldierEntity.Index, soldierEntity, true);

                    return;
                }

                if (soldierTargetForAttack.TargetSoldier != Entity.Null)
                {
                    return;
                }

                NativeList<TroopSoldier> targetCandidateSoldiers = new(Allocator.TempJob);
                do
                {
                    targetCandidateSoldiers.Add(targetCandidateSoldier);
                } while (SoldiersByTroop.TryGetNextValue(out targetCandidateSoldier, ref iterator));

                targetCandidateSoldiers.Sort(new TroopSoldierComparer(localTransform.Position));

                Entity targetEntity = Entity.Null;

                for (int i = 0, targetCandidateSoldierCount = targetCandidateSoldiers.Length; i < targetCandidateSoldierCount; i++)
                {
                    Entity candidateSoldierEntity = targetCandidateSoldiers[i].Entity;

                    if (AlreadyTargetedSoldierEntityIndices.Add(candidateSoldierEntity.Index))
                    {
                        targetEntity = candidateSoldierEntity;
                        break;
                    }
                }

                soldierTargetForAttack.TargetSoldier = targetEntity != Entity.Null ? targetEntity : targetCandidateSoldiers[0].Entity;

                targetCandidateSoldiers.Dispose();

                EntityCommandBuffer.SetComponentEnabled<SoldierStateMoveInFormation>(soldierEntity.Index, soldierEntity, false);
                EntityCommandBuffer.SetComponentEnabled<SoldierStateMoveToTarget>(soldierEntity.Index, soldierEntity, true);
            }
        }


        private EntityQuery _checkTargetValidQuery;
        private EntityQuery _aliveSoldierQuery;

        private ComponentLookup<LocalTransform> _localTransformLookup;
        private ComponentLookup<TroopTargetForAttack> _troopTargetForAttackLookup;


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
                    .WithAllRW<SoldierTargetForAttack, SoldierAnimation>()
                    .Build();

            _localTransformLookup = state.GetComponentLookup<LocalTransform>(true);
            _troopTargetForAttackLookup = state.GetComponentLookup<TroopTargetForAttack>(true);
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

            _troopTargetForAttackLookup.Update(ref state);

            int aliveSoldierCount = _aliveSoldierQuery.CalculateEntityCount();

            NativeParallelMultiHashMap<Entity, TroopSoldier> soldiersByTroop = new(aliveSoldierCount, Allocator.TempJob);
            NativeParallelHashSet<int> alreadyTargetedSoldierEntityIndices = new(aliveSoldierCount, Allocator.TempJob);

            JobHandle dependency = state.Dependency;

            dependency = new CollectSoldiersByTroopJob { SoldiersByTroop = soldiersByTroop.AsParallelWriter() }.ScheduleParallel(_aliveSoldierQuery, dependency);

            EntityCommandBuffer ecbSetSoldierStateByTroop = new(Allocator.TempJob);
            new SetSoldierStateByTroopJob
                {
                    SoldiersByTroop = soldiersByTroop.AsReadOnly(),
                    TroopTargetForAttackLookup = _troopTargetForAttackLookup,

                    AlreadyTargetedSoldierEntityIndices = alreadyTargetedSoldierEntityIndices.AsParallelWriter(),
                    EntityCommandBuffer = ecbSetSoldierStateByTroop.AsParallelWriter(),
                }
                .ScheduleParallel(_aliveSoldierQuery, dependency)
                .Complete();
            ecbSetSoldierStateByTroop.Playback(state.EntityManager);
            ecbSetSoldierStateByTroop.Dispose();

            alreadyTargetedSoldierEntityIndices.Dispose(dependency);
            soldiersByTroop.Dispose();
        }
    }
}