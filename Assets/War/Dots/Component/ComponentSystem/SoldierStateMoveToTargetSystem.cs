using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;


namespace War.Dots.Component.ComponentSystem
{
    [UpdateInGroup(typeof(Group.SoldierStateSystemGroup))]
    [UpdateAfter(typeof(SoldierStateMoveInFormationSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct SoldierStateMoveToTargetSystem : ISystem
    {
        [BurstCompile]
        private partial struct CheckTargetValidJob : IJobEntity
        {
            [ReadOnly] public BufferLookup<Damaged> DamagedLookup;
            [ReadOnly] public ComponentLookup<LocalTransform> SoldierTransformLookup;

            public EntityCommandBuffer.ParallelWriter EntityCommandBuffer;


            private void Execute([EntityIndexInQuery] int index, Entity entity, ref SoldierTargetForAttack targetForAttack, ref SoldierDestination soldierDestination, in LocalTransform localTransform, in AttackRange attackRange)
            {
                if (targetForAttack.TargetSoldier == Entity.Null ||
                    !DamagedLookup.HasBuffer(targetForAttack.TargetSoldier))
                {
                    targetForAttack.TargetSoldier = Entity.Null;

                    EntityCommandBuffer.SetComponentEnabled<SoldierStateMoveToTarget>(index, entity, false);
                    EntityCommandBuffer.SetComponentEnabled<SoldierStateMoveInFormation>(index, entity, true);

                    return;
                }

                float3 targetPos = SoldierTransformLookup[targetForAttack.TargetSoldier].Position;
                float dist = math.distance(localTransform.Position, targetPos);
                if (dist <= attackRange.Value)
                {
                    soldierDestination.Position = localTransform.Position;

                    EntityCommandBuffer.SetComponentEnabled<SoldierStateMoveToTarget>(index, entity, false);
                    EntityCommandBuffer.SetComponentEnabled<SoldierStateAttackTarget>(index, entity, true);

                    EntityCommandBuffer.SetComponentEnabled<Movable>(index, entity, false);
                    EntityCommandBuffer.SetComponentEnabled<Rotatable>(index, entity, false);
                    EntityCommandBuffer.SetComponent(index, entity, new Attack { AttackStep = Attack.Step.NotYet });
                }
                else
                {
                    soldierDestination.Position = targetPos;
                }
            }
        }


        private EntityQuery _checkTargetValidQuery;

        private BufferLookup<Damaged> _damagedLookup;
        private ComponentLookup<LocalTransform> _soldierTransformLookup;


        public void OnCreate(ref SystemState state)
        {
            _checkTargetValidQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<Soldier, Alive, SoldierStateMoveToTarget, LocalTransform, AttackRange>()
                    .WithAllRW<SoldierTargetForAttack, SoldierDestination>()
                    .Build();

            _damagedLookup = state.GetBufferLookup<Damaged>(true);
            _soldierTransformLookup = state.GetComponentLookup<LocalTransform>(true);
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            _damagedLookup.Update(ref state);
            _soldierTransformLookup.Update(ref state);

            JobHandle dependency = state.Dependency;

            EndSimulationEntityCommandBufferSystem ecbSystem = state.World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();

            EntityCommandBuffer ecb = ecbSystem.CreateCommandBuffer();
            dependency =
                new CheckTargetValidJob
                    {
                        DamagedLookup = _damagedLookup,
                        SoldierTransformLookup = _soldierTransformLookup,

                        EntityCommandBuffer = ecb.AsParallelWriter(),
                    }
                    .ScheduleParallel(_checkTargetValidQuery, dependency);
            ecbSystem.AddJobHandleForProducer(dependency);

            state.Dependency = dependency;
        }
    }
}