using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;


namespace War.Dots.Component.ComponentSystem
{
    [UpdateInGroup(typeof(Group.SoldierStateSystemGroup))]
    [UpdateAfter(typeof(SoldierStateMoveToTargetSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct SoldierStateAttackTargetSystem : ISystem
    {
        [BurstCompile]
        private partial struct AttackJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter EntityCommandBuffer;

            [ReadOnly] public ComponentLookup<LocalTransform> LocalTransformLookup;
            [ReadOnly] public double CurrentTime;
            
            [ReadOnly] public BufferLookup<Damaged> DamagedLookup;
            [ReadOnly] public BufferLookup<SpawnHitEffect> SpawnHitEffectLookup;


            private void Execute(
                [EntityIndexInQuery] int index, Entity soldierEntity,
                ref Attack attack, ref SoldierTargetForAttack targetForAttack, ref SoldierAnimation soldierAnimation, ref Forward forward, ref SoldierDestination soldierDestination,
                in Team team, in LocalTransform localTransform,
                in SoldierWeapon soldierWeapon, in AttackData attackData, in AttackPower attackPower, in AttackRange attackRange)
            {
                float3 pos = localTransform.Position;

                if (targetForAttack.TargetSoldier == Entity.Null ||
                    !LocalTransformLookup.HasComponent(targetForAttack.TargetSoldier))
                {
                    soldierAnimation.Next = SoldierAnimation.State.Default;

                    targetForAttack.TargetSoldier = Entity.Null;

                    EntityCommandBuffer.SetComponentEnabled<SoldierStateMoveInFormation>(index, soldierEntity, true);
                    EntityCommandBuffer.SetComponentEnabled<SoldierStateAttackTarget>(index, soldierEntity, false);

                    EntityCommandBuffer.SetComponentEnabled<Movable>(index, soldierEntity, true);
                    EntityCommandBuffer.SetComponentEnabled<Rotatable>(index, soldierEntity, true);

                    return;
                }

                if (!LocalTransformLookup.TryGetRefRO(targetForAttack.TargetSoldier, out RefRO<LocalTransform> targetLocalTransform))
                {
                    return;
                }

                float3 otherPos = targetLocalTransform.ValueRO.Position;

                switch (attack.AttackStep)
                {
                    case Attack.Step.NotYet:
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
                                    if (DamagedLookup.HasBuffer(targetForAttack.TargetSoldier))
                                    {
                                        EntityCommandBuffer.AppendToBuffer(index, targetForAttack.TargetSoldier, new Damaged { Hitter = soldierEntity, HitDamage = attackPower.Value });
                                    }

                                    if (SpawnHitEffectLookup.HasBuffer(targetForAttack.TargetSoldier))
                                    {
                                        EntityCommandBuffer.AppendToBuffer(index, targetForAttack.TargetSoldier, new SpawnHitEffect { Position = otherPos });
                                    }

                                    break;

                                case SoldierWeaponType.Arrow:
                                    EntityCommandBuffer.AppendToBuffer(index, soldierEntity, new SpawnArrow
                                    {
                                        Shooter = soldierEntity,
                                        ShooterTeamColor = team.Color,
                                        Target = targetForAttack.TargetSoldier,
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
                            attack.AttackStep = Attack.Step.NotYet;

                            soldierDestination.Position = pos;

                            EntityCommandBuffer.SetComponentEnabled<SoldierStateMoveToTarget>(index, soldierEntity, true);
                            EntityCommandBuffer.SetComponentEnabled<SoldierStateAttackTarget>(index, soldierEntity, false);

                            EntityCommandBuffer.SetComponentEnabled<Movable>(index, soldierEntity, true);
                            EntityCommandBuffer.SetComponentEnabled<Rotatable>(index, soldierEntity, true);
                        }

                        break;
                }
            }
        }


        private EntityQuery _stateAttackTargetQuery;
        private ComponentLookup<LocalTransform> _localTransformLookup;
        private BufferLookup<Damaged> _damagedLookup;
        private BufferLookup<SpawnHitEffect> _spawnHitEffectLookup;


        public void OnCreate(ref SystemState state)
        {
            _stateAttackTargetQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<Alive, Soldier, SoldierStateAttackTarget, LocalTransform>()
                    .WithAll<Team, NavMeshAgentData, SoldierWeapon, AttackData, AttackPower, AttackRange>()
                    .WithAllRW<Attack, SoldierAnimation>()
                    .WithAllRW<Forward, SoldierDestination>()
                    .WithAllRW<SoldierTargetForAttack>()
                    .Build();

            _localTransformLookup = state.GetComponentLookup<LocalTransform>(true);
            _damagedLookup = state.GetBufferLookup<Damaged>(true);
            _spawnHitEffectLookup = state.GetBufferLookup<SpawnHitEffect>(true);
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            _localTransformLookup.Update(ref state);
            _damagedLookup.Update(ref state);
            _spawnHitEffectLookup.Update(ref state);

            JobHandle dependency = state.Dependency;

            EndSimulationEntityCommandBufferSystem ecbSystem = state.World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();

            EntityCommandBuffer ecb = ecbSystem.CreateCommandBuffer();
            dependency =
                new AttackJob
                    {
                        EntityCommandBuffer = ecb.AsParallelWriter(),

                        LocalTransformLookup = _localTransformLookup,
                        CurrentTime = SystemAPI.Time.ElapsedTime,
                        
                        DamagedLookup = _damagedLookup,
                        SpawnHitEffectLookup = _spawnHitEffectLookup,
                    }
                    .ScheduleParallel(_stateAttackTargetQuery, dependency);
            ecbSystem.AddJobHandleForProducer(dependency);

            state.Dependency = dependency;
        }
    }
}