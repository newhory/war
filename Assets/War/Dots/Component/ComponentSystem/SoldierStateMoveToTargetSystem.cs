using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
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


            public void Execute([EntityIndexInQuery] int entityIndex, Entity entity, ref SoldierTargetForAttack targetForAttack, ref Destination moveToDestination, in LocalTransform localTransform, in AttackRange attackRange)
            {
                if (targetForAttack.TargetSoldier == Entity.Null ||
                    !DamagedLookup.HasBuffer(targetForAttack.TargetSoldier))
                {
                    targetForAttack.TargetSoldier = Entity.Null;

                    EntityCommandBuffer.SetComponentEnabled<SoldierStateMoveToTarget>(entityIndex, entity, false);
                    EntityCommandBuffer.SetComponentEnabled<SoldierStateMoveInFormation>(entityIndex, entity, true);

                    return;
                }

                float3 targetPos = SoldierTransformLookup[targetForAttack.TargetSoldier].Position;
                float dist = math.distance(localTransform.Position, targetPos);
                if (dist <= attackRange.Value)
                {
                    moveToDestination.Position = localTransform.Position;

                    EntityCommandBuffer.SetComponentEnabled<SoldierStateMoveToTarget>(entityIndex, entity, false);
                    EntityCommandBuffer.SetComponentEnabled<SoldierStateAttackTarget>(entityIndex, entity, true);

                    EntityCommandBuffer.SetComponentEnabled<Movable>(entityIndex, entity, false);
                    EntityCommandBuffer.SetComponentEnabled<Rotatable>(entityIndex, entity, false);
                    EntityCommandBuffer.SetComponent(entityIndex, entity, new Attack { AttackStep = Attack.Step.NotYet });
                }
                else
                {
                    moveToDestination.Position = targetPos;
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
                    .WithAllRW<SoldierTargetForAttack, Destination>()
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

            EntityCommandBuffer ecb = new(Allocator.TempJob);
            new CheckTargetValidJob
                {
                    DamagedLookup = _damagedLookup,
                    SoldierTransformLookup = _soldierTransformLookup,

                    EntityCommandBuffer = ecb.AsParallelWriter(),
                }
                .ScheduleParallel(_checkTargetValidQuery, state.Dependency)
                .Complete();
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}