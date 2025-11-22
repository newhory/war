using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;


namespace War.Dots.Component.ComponentSystem
{
    [UpdateInGroup(typeof(Group.DamagedSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct SoldierDamagedSystem : ISystem
    {
        [BurstCompile]
        private partial struct SoldierHitDamageJob : IJobEntity
        {
            public void Execute(DynamicBuffer<Damaged> damagedBuffer, ref SoldierAnimation soldierAnimation)
            {
                if (soldierAnimation.Current == SoldierAnimation.State.Hit)
                {
                    soldierAnimation.Next = SoldierAnimation.State.Default;
                }

                foreach (Damaged damaged in damagedBuffer)
                {
                    if (damaged.HitDamage > 0)
                    {
                        if (soldierAnimation.Current == SoldierAnimation.State.Default)
                        {
                            soldierAnimation.Next = SoldierAnimation.State.Hit;
                        }

                        break;
                    }
                }
            }
        }

        [BurstCompile]
        private partial struct SoldierSetTargetJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<LocalTransform> LocalTransformLookup;


            public void Execute(DynamicBuffer<Damaged> damagedBuffer, ref SoldierTargetForAttack targetForAttack, in LocalTransform localTransform)
            {
                float distanceToTarget = GetDistance(localTransform, targetForAttack.TargetSoldier);
                Entity target = targetForAttack.TargetSoldier;

                foreach (Damaged damaged in damagedBuffer)
                {
                    if (damaged.HitDamage <= 0)
                    {
                        continue;
                    }

                    float distanceToHitter = GetDistance(localTransform, damaged.Hitter);
                    if (distanceToHitter < distanceToTarget)
                    {
                        distanceToTarget = distanceToHitter;
                        target = damaged.Hitter;
                    }
                }

                targetForAttack.TargetSoldier = target;
            }

            private float GetDistance(in LocalTransform localTransform, Entity target) =>
                target != Entity.Null && LocalTransformLookup.TryGetRefRO(target, out RefRO<LocalTransform> targetTransform)
                    ? math.distance(localTransform.Position, targetTransform.ValueRO.Position)
                    : float.MaxValue;
        }


        private EntityQuery _soldierHitDamageQuery;
        private EntityQuery _soldierHitDamageNotAttackingQuery;

        private ComponentLookup<LocalTransform> _localTransformLookup;


        public void OnCreate(ref SystemState state)
        {
            _soldierHitDamageQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<Alive, Soldier, Damaged>()
                    .WithAllRW<SoldierAnimation>()
                    .Build();

            _soldierHitDamageNotAttackingQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<Alive, Soldier, Damaged, LocalTransform>()
                    .WithAllRW<SoldierTargetForAttack>()
                    .WithDisabled<StateAttackTarget>()
                    .Build();

            _localTransformLookup = state.GetComponentLookup<LocalTransform>(true);
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            _localTransformLookup.Update(ref state);
            
            JobHandle dependency = state.Dependency;

            dependency = new SoldierHitDamageJob().ScheduleParallel(_soldierHitDamageQuery, dependency);
            dependency = new SoldierSetTargetJob { LocalTransformLookup = _localTransformLookup }.ScheduleParallel(_soldierHitDamageNotAttackingQuery, dependency);
            
            state.Dependency = dependency;
        }
    }
}