using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;


namespace War.Dots.Component.ComponentSystem
{
    [UpdateInGroup(typeof(Group.SoldierStateSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct SoldierStateAttackTargetSystem : ISystem
    {
        [BurstCompile]
        private partial struct AttackJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter EntityCommandBuffer;

            [ReadOnly] public ComponentLookup<LocalTransform> LocalTransformLookup;
            [ReadOnly] public double CurrentTime;


            public void Execute(
                [EntityIndexInQuery] int entityIndex,
                Entity entity, DynamicBuffer<SpawnArrow> arrowSpawnDataBuffer,
                ref Attack attack, ref SoldierAnimation soldierAnimation, ref Forward forward, ref Destination moveToDestination,
                in Team team, in LocalTransform localTransform, in TargetForAttack targetForAttack,
                in SoldierWeapon soldierWeapon, in AttackData attackData, in AttackPower attackPower, in AttackRange attackRange)
            {
                float3 pos = localTransform.Position;

                if (targetForAttack.Target == Entity.Null)
                {
                    soldierAnimation.Next = SoldierAnimation.State.Default;

                    EntityCommandBuffer.SetComponentEnabled<StateMoveInFormation>(entityIndex, entity, true);
                    EntityCommandBuffer.SetComponentEnabled<StateAttackTarget>(entityIndex, entity, false);

                    return;
                }

                if (!LocalTransformLookup.TryGetRefRO(targetForAttack.Target, out RefRO<LocalTransform> targetLocalTransform))
                {
                    return;
                }

                float3 otherPos = targetLocalTransform.ValueRO.Position;

                switch (attack.AttackStep)
                {
                    case Attack.Step.NotYet:
                        EntityCommandBuffer.SetComponentEnabled<Movable>(entityIndex, entity, false);
                        EntityCommandBuffer.SetComponentEnabled<Rotatable>(entityIndex, entity, false);

                        soldierAnimation.Next = SoldierAnimation.State.Attack;

                        forward.Value = math.normalize(otherPos - pos);

                        attack.AttackTime = CurrentTime;
                        attack.AttackStep = Attack.Step.Attacked;
                        break;

                    case Attack.Step.Attacked:
                        if (CurrentTime >= attack.AttackTime + attackData.HitTime)
                        {
                            switch (soldierWeapon.Type)
                            {
                                case SoldierWeaponType.Melee:
                                    EntityCommandBuffer.AppendToBuffer(entityIndex, targetForAttack.Target, new Damaged { Hitter = entity, HitDamage = attackPower.Value });
                                    EntityCommandBuffer.AppendToBuffer(entityIndex, targetForAttack.Target, new SpawnHitEffect { Position = otherPos });

                                    break;

                                case SoldierWeaponType.Arrow:
                                    arrowSpawnDataBuffer.Add(
                                        new SpawnArrow
                                        {
                                            Shooter = entity,
                                            ShooterTeamColor = team.Color,
                                            Target = targetForAttack.Target,
                                            Damage = attackPower.Value,
                                            MinSpeed = 8,
                                            MaxPoiDeviation = 0.25f,
                                            StartPosition = new float3(pos.x, pos.y + 0.7f, pos.z),
                                            EndPosition = new float3(otherPos.x, otherPos.y + 0.7f, otherPos.z)
                                        });
                                    break;
                            }

                            attack.AttackStep = Attack.Step.End;
                        }

                        break;

                    case Attack.Step.End:
                        if (CurrentTime >= attack.AttackTime + attackData.Duration)
                        {
                            soldierAnimation.Next = SoldierAnimation.State.Default;

                            attack.AttackStep = Attack.Step.Delay;
                        }

                        break;

                    case Attack.Step.Delay:
                        if (CurrentTime >= attack.AttackTime + attackData.Duration + attackData.Delay)
                        {
                            EntityCommandBuffer.SetComponentEnabled<Movable>(entityIndex, entity, true);
                            EntityCommandBuffer.SetComponentEnabled<Rotatable>(entityIndex, entity, true);

                            moveToDestination.Position = pos;

                            EntityCommandBuffer.SetComponentEnabled<StateMoveToTarget>(entityIndex, entity, true);
                            EntityCommandBuffer.SetComponentEnabled<StateAttackTarget>(entityIndex, entity, false);

                            attack.AttackStep = Attack.Step.NotYet;
                        }

                        break;
                }
            }
        }


        private EntityQuery _stateAttackTargetQuery;
        private ComponentLookup<LocalTransform> _navMeshAgentDataLookup;
        private ComponentLookup<Health> _soldierHealthLookup;


        public void OnCreate(ref SystemState state)
        {
            _stateAttackTargetQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<Alive, Soldier, StateAttackTarget, LocalTransform>()
                    .WithAll<Team, NavMeshAgentData, TargetForAttack, SoldierWeapon, AttackData, AttackPower, AttackRange>()
                    .WithAllRW<Attack, SoldierAnimation>()
                    .WithAllRW<Forward, Destination>()
                    .WithAllRW<SpawnArrow>()
                    .Build();

            _navMeshAgentDataLookup = state.GetComponentLookup<LocalTransform>(true);
            _soldierHealthLookup = state.GetComponentLookup<Health>(true);
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            _navMeshAgentDataLookup.Update(ref state);
            _soldierHealthLookup.Update(ref state);

            using EntityCommandBuffer ecb = new(Allocator.TempJob);

            new AttackJob
                {
                    EntityCommandBuffer = ecb.AsParallelWriter(),

                    LocalTransformLookup = _navMeshAgentDataLookup,
                    CurrentTime = SystemAPI.Time.ElapsedTime,
                }
                .ScheduleParallel(_stateAttackTargetQuery, state.Dependency)
                .Complete();

            ecb.Playback(state.EntityManager);
        }
    }
}