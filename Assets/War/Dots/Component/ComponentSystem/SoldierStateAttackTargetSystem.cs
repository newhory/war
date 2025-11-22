using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
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


            public void Execute(
                Entity entity, DynamicBuffer<SpawnArrow> arrowSpawnDataBuffer,
                ref Attack attack, ref SoldierTargetForAttack targetForAttack, ref SoldierAnimation soldierAnimation, ref Forward forward, ref Destination moveToDestination,
                in Team team, in LocalTransform localTransform,
                in SoldierWeapon soldierWeapon, in AttackData attackData, in AttackPower attackPower, in AttackRange attackRange)
            {
                float3 pos = localTransform.Position;

                if (targetForAttack.TargetSoldier == Entity.Null ||
                    !LocalTransformLookup.HasComponent(targetForAttack.TargetSoldier))
                {
                    soldierAnimation.Next = SoldierAnimation.State.Default;

                    targetForAttack.TargetSoldier = Entity.Null;
                    
                    EntityCommandBuffer.SetComponentEnabled<SoldierStateMoveInFormation>(entity.Index, entity, true);
                    EntityCommandBuffer.SetComponentEnabled<SoldierStateAttackTarget>(entity.Index, entity, false);
                    
                    EntityCommandBuffer.SetComponentEnabled<Movable>(entity.Index, entity, true);
                    EntityCommandBuffer.SetComponentEnabled<Rotatable>(entity.Index, entity, true);

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
                                    EntityCommandBuffer.AppendToBuffer(entity.Index, targetForAttack.TargetSoldier, new Damaged { Hitter = entity, HitDamage = attackPower.Value });
                                    EntityCommandBuffer.AppendToBuffer(entity.Index, targetForAttack.TargetSoldier, new SpawnHitEffect { Position = otherPos });

                                    break;

                                case SoldierWeaponType.Arrow:
                                    arrowSpawnDataBuffer.Add(
                                        new SpawnArrow
                                        {
                                            Shooter = entity,
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
                            
                            moveToDestination.Position = pos;

                            EntityCommandBuffer.SetComponentEnabled<SoldierStateMoveToTarget>(entity.Index, entity, true);
                            EntityCommandBuffer.SetComponentEnabled<SoldierStateAttackTarget>(entity.Index, entity, false);
                            
                            EntityCommandBuffer.SetComponentEnabled<Movable>(entity.Index, entity, true);
                            EntityCommandBuffer.SetComponentEnabled<Rotatable>(entity.Index, entity, true);
                        }

                        break;
                }
            }
        }


        private EntityQuery _stateAttackTargetQuery;
        private ComponentLookup<LocalTransform> _localTransformLookup;


        public void OnCreate(ref SystemState state)
        {
            _stateAttackTargetQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<Alive, Soldier, SoldierStateAttackTarget, LocalTransform>()
                    .WithAll<Team, NavMeshAgentData, SoldierWeapon, AttackData, AttackPower, AttackRange>()
                    .WithAllRW<Attack, SoldierAnimation>()
                    .WithAllRW<Forward, Destination>()
                    .WithAllRW<SpawnArrow, SoldierTargetForAttack>()
                    .Build();

            _localTransformLookup = state.GetComponentLookup<LocalTransform>(true);
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            _localTransformLookup.Update(ref state);

            EntityCommandBuffer ecb = new(Allocator.TempJob);
            new AttackJob
                {
                    EntityCommandBuffer = ecb.AsParallelWriter(),

                    LocalTransformLookup = _localTransformLookup,
                    CurrentTime = SystemAPI.Time.ElapsedTime,
                }
                .ScheduleParallel(_stateAttackTargetQuery, state.Dependency)
                .Complete();
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}