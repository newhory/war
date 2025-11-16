using Unity.Burst;
using Unity.Entities;


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
            public void Execute(DynamicBuffer<Damaged> damagedBuffer, ref TargetForAttack targetForAttack)
            {
                foreach (Damaged damaged in damagedBuffer)
                {
                    if (damaged.HitDamage > 0)
                    {
                        targetForAttack.Target = damaged.Hitter;

                        break;
                    }
                }
            }
        }


        private EntityQuery _soldierHitDamageQuery;
        private EntityQuery _soldierHitDamageNotAttackingQuery;


        public void OnCreate(ref SystemState state)
        {
            _soldierHitDamageQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<Alive, Soldier, Damaged>()
                    .WithAllRW<SoldierAnimation>()
                    .Build();

            _soldierHitDamageNotAttackingQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<Alive, Soldier, Damaged>()
                    .WithAllRW<TargetForAttack>()
                    .WithDisabled<StateAttackTarget>()
                    .Build();
        }
        
        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new SoldierHitDamageJob().ScheduleParallel(_soldierHitDamageQuery, state.Dependency);
            state.Dependency = new SoldierSetTargetJob().ScheduleParallel(_soldierHitDamageNotAttackingQuery, state.Dependency);
        }
    }
}