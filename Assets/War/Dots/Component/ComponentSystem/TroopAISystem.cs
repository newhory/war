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


            private void Execute([EntityIndexInQuery] int index, Entity entity, ref Destination moveToDestination, in TroopTargetForAttack targetForAttack)
            {
                if (targetForAttack.TargetTroop != Entity.Null)
                {
                    float3 otherPos = LocalTransformLookup[targetForAttack.TargetTroop].Position;

                    moveToDestination.Position = otherPos;

                    EntityCommandBuffer.SetComponentEnabled<TroopAISearchTarget>(index, entity, false);
                    EntityCommandBuffer.SetComponentEnabled<TroopAICheckTargetValid>(index, entity, true);

                    EntityCommandBuffer.SetComponentEnabled<TroopStateMoveToTarget>(index, entity, true);
                    EntityCommandBuffer.SetComponentEnabled<TroopStateMoveToDestination>(index, entity, false);
                }
                else
                {
                    EntityCommandBuffer.SetComponentEnabled<TroopAISearchTarget>(index, entity, true);
                    EntityCommandBuffer.SetComponentEnabled<TroopAICheckTargetValid>(index, entity, false);

                    EntityCommandBuffer.SetComponentEnabled<TroopStateMoveToTarget>(index, entity, false);
                    EntityCommandBuffer.SetComponentEnabled<TroopStateMoveToDestination>(index, entity, true);
                }
            }
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
            [ReadOnly] public ComponentLookup<TroopTargetForAttack> TroopTargetForAttackLookup;
            [ReadOnly] public BufferLookup<TroopSoldier> TroopSoldierLookup;

            public NativeParallelHashSet<int>.ParallelWriter AlreadyTargetedSoldierEntityIndices;
            public EntityCommandBuffer.ParallelWriter EntityCommandBuffer;


            private void Execute([EntityIndexInQuery] int index, Entity soldierEntity, ref SoldierTargetForAttack soldierTargetForAttack, ref SoldierAnimation soldierAnimation, in SoldierAttachedTroop soldierAttachedTroop, in LocalTransform localTransform)
            {
                if (!TroopTargetForAttackLookup.TryGetRefRO(soldierAttachedTroop.TroopEntity, out RefRO<TroopTargetForAttack> troopTargetForAttack) ||
                    troopTargetForAttack.ValueRO.TargetTroop == Entity.Null ||
                    !TroopSoldierLookup.TryGetBuffer(troopTargetForAttack.ValueRO.TargetTroop, out DynamicBuffer<TroopSoldier> targetCandidateTroopSoldiers) ||
                    targetCandidateTroopSoldiers.IsEmpty)
                {
                    soldierAnimation.Next = SoldierAnimation.State.Default;

                    soldierTargetForAttack.TargetSoldier = Entity.Null;

                    EntityCommandBuffer.SetComponentEnabled<SoldierStateMoveInFormation>(index, soldierEntity, true);
                    EntityCommandBuffer.SetComponentEnabled<SoldierStateMoveToTarget>(index, soldierEntity, false);
                    EntityCommandBuffer.SetComponentEnabled<SoldierStateAttackTarget>(index, soldierEntity, false);

                    EntityCommandBuffer.SetComponentEnabled<Movable>(index, soldierEntity, true);
                    EntityCommandBuffer.SetComponentEnabled<Rotatable>(index, soldierEntity, true);

                    return;
                }

                if (soldierTargetForAttack.TargetSoldier != Entity.Null)
                {
                    return;
                }

                NativeArray<TroopSoldier> targetCandidateSoldiers = new(targetCandidateTroopSoldiers.Length, Allocator.Temp);
                targetCandidateSoldiers.CopyFrom(targetCandidateTroopSoldiers.AsNativeArray());

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

                EntityCommandBuffer.SetComponentEnabled<SoldierStateMoveInFormation>(index, soldierEntity, false);
                EntityCommandBuffer.SetComponentEnabled<SoldierStateMoveToTarget>(index, soldierEntity, true);
            }
        }


        private EntityQuery _checkTargetValidQuery;
        private EntityQuery _aliveSoldierQuery;

        private ComponentLookup<LocalTransform> _localTransformLookup;
        private ComponentLookup<TroopTargetForAttack> _troopTargetForAttackLookup;
        private BufferLookup<TroopSoldier> _troopSoldierLookup;


        public void OnCreate(ref SystemState state)
        {
            _checkTargetValidQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<Troop, Alive, TroopEntity, TroopTargetForAttack>()
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
            _troopSoldierLookup = state.GetBufferLookup<TroopSoldier>(true);
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            _localTransformLookup.Update(ref state);

            JobHandle dependency = state.Dependency;

            EndSimulationEntityCommandBufferSystem ecbSystem = state.World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();

            EntityCommandBuffer checkTargetValidEcb = ecbSystem.CreateCommandBuffer();
            dependency =
                new CheckTargetValidJob
                    {
                        EntityCommandBuffer = checkTargetValidEcb.AsParallelWriter(),

                        LocalTransformLookup = _localTransformLookup
                    }
                    .ScheduleParallel(_checkTargetValidQuery, dependency);
            ecbSystem.AddJobHandleForProducer(dependency);

            _troopTargetForAttackLookup.Update(ref state);
            _troopSoldierLookup.Update(ref state);

            NativeParallelHashSet<int> alreadyTargetedSoldierEntityIndices = new(_aliveSoldierQuery.CalculateEntityCount(), Allocator.TempJob);

            EntityCommandBuffer ecb = ecbSystem.CreateCommandBuffer();
            dependency =
                new SetSoldierStateByTroopJob
                    {
                        TroopTargetForAttackLookup = _troopTargetForAttackLookup,
                        TroopSoldierLookup = _troopSoldierLookup,

                        AlreadyTargetedSoldierEntityIndices = alreadyTargetedSoldierEntityIndices.AsParallelWriter(),
                        EntityCommandBuffer = ecb.AsParallelWriter(),
                    }
                    .ScheduleParallel(_aliveSoldierQuery, dependency);
            ecbSystem.AddJobHandleForProducer(dependency);

            dependency = alreadyTargetedSoldierEntityIndices.Dispose(dependency);

            state.Dependency = dependency;
        }
    }
}