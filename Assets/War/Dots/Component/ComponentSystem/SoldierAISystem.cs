using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;


namespace War.Dots.Component.ComponentSystem
{
    [UpdateInGroup(typeof(Group.SoldierAISystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct SoldierAISystem : ISystem
    {
        [BurstCompile]
        private partial struct SearchTargetJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter EntityCommandBuffer;


            public void Execute([EntityIndexInQuery] int entityIndex, Entity entity, in TargetForAttack targetForAttack)
            {
                if (targetForAttack.Target != Entity.Null)
                {
                    EntityCommandBuffer.SetComponentEnabled<AISearchTarget>(entityIndex, entity, false);
                    EntityCommandBuffer.SetComponentEnabled<AICheckTargetValid>(entityIndex, entity, true);
                }
            }
        }

        [BurstCompile]
        private partial struct CheckTargetValidJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter EntityCommandBuffer;

            [ReadOnly] public ComponentLookup<LocalTransform> SoldierPositionLookup;


            public void Execute([EntityIndexInQuery] int entityIndex, Entity entity, ref Destination moveToDestination, in AICheckTargetValid checkTargetValid, in LocalTransform localTransform, in TargetForAttack targetForAttack, in AttackRange attackRange)
            {
                if (targetForAttack.Target != Entity.Null)
                {
                    EntityCommandBuffer.SetComponentEnabled<StateMoveInFormation>(entityIndex, entity, false);

                    float3 otherPos = SoldierPositionLookup[targetForAttack.Target].Position;
                    float dist = math.distance(localTransform.Position, otherPos);
                    if (dist <= attackRange.Value)
                    {
                        moveToDestination.Position = localTransform.Position;
                        
                        EntityCommandBuffer.SetComponentEnabled<StateMoveToTarget>(entityIndex, entity, false);
                        EntityCommandBuffer.SetComponentEnabled<StateAttackTarget>(entityIndex, entity, true);

                        EntityCommandBuffer.SetComponent(entityIndex, entity, new Attack { AttackStep = Attack.Step.NotYet });
                    }
                    else
                    {
                        moveToDestination.Position = otherPos;
                        
                        EntityCommandBuffer.SetComponentEnabled<StateMoveToTarget>(entityIndex, entity, true);
                        EntityCommandBuffer.SetComponentEnabled<StateAttackTarget>(entityIndex, entity, false);

                        EntityCommandBuffer.SetComponentEnabled<Movable>(entityIndex, entity, true);
                        EntityCommandBuffer.SetComponentEnabled<Rotatable>(entityIndex, entity, true);
                    }
                }
                else
                {
                    EntityCommandBuffer.SetComponentEnabled<AISearchTarget>(entityIndex, entity, true);
                    EntityCommandBuffer.SetComponentEnabled<AICheckTargetValid>(entityIndex, entity, false);

                    EntityCommandBuffer.SetComponentEnabled<StateMoveInFormation>(entityIndex, entity, true);
                    EntityCommandBuffer.SetComponentEnabled<StateMoveToTarget>(entityIndex, entity, false);

                    EntityCommandBuffer.SetComponentEnabled<Movable>(entityIndex, entity, true);
                    EntityCommandBuffer.SetComponentEnabled<Rotatable>(entityIndex, entity, true);
                }
            }
        }

        [BurstCompile]
        private partial struct SetMaxSpeedJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter EntityCommandBuffer;

            [ReadOnly] public float MaxSpeed;


            public void Execute([EntityIndexInQuery] int entityIndex, Entity entity, ref MoveSpeed moveSpeed)
            {
                if (math.abs(moveSpeed.CurrentMax - MaxSpeed) > 0.00001f)
                {
                    moveSpeed.CurrentMax = MaxSpeed;

                    EntityCommandBuffer.SetComponentEnabled<UseDefaultMaxSpeed>(entityIndex, entity, false);
                }
            }
        }

        [BurstCompile]
        private partial struct RestoreMaxSpeedJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter EntityCommandBuffer;


            public void Execute([EntityIndexInQuery] int entityIndex, Entity entity, ref MoveSpeed moveSpeed)
            {
                moveSpeed.CurrentMax = moveSpeed.Max;

                EntityCommandBuffer.SetComponentEnabled<UseDefaultMaxSpeed>(entityIndex, entity, true);
            }
        }


        private EntityQuery _searchTargetQuery;
        private EntityQuery _checkTargetValidQuery;

        private EntityQuery _moveInFormationSoldierQuery;
        private EntityQuery _notMoveInFormationSoldierQuery;
        
        private ComponentLookup<LocalTransform> _soldierPositionLookup;


        public void OnCreate(ref SystemState state)
        {
            _searchTargetQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<Soldier, Alive, AISearchTarget, TargetForAttack>()
                    .WithDisabled<AICheckTargetValid>()
                    .Build();

            _checkTargetValidQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<Soldier, Alive, AICheckTargetValid, NavMeshAgentData, TargetForAttack, AttackRange, LocalTransform>()
                    .WithDisabled<AISearchTarget, StateAttackTarget>()
                    .WithAllRW<Destination>()
                    .Build();

            _moveInFormationSoldierQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<Soldier, Alive, Formation, StateMoveInFormation, UseDefaultMaxSpeed>()
                    .WithAllRW<MoveSpeed>()
                    .Build();

            _notMoveInFormationSoldierQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<Soldier, Alive>()
                    .WithDisabled<StateMoveInFormation, UseDefaultMaxSpeed>()
                    .WithAllRW<MoveSpeed>()
                    .Build();

            _soldierPositionLookup = state.GetComponentLookup<LocalTransform>(true);
        }
        
        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            using EntityCommandBuffer searchTargetEcb = new(Allocator.TempJob);
            new SearchTargetJob
                {
                    EntityCommandBuffer = searchTargetEcb.AsParallelWriter()
                }
                .ScheduleParallel(_searchTargetQuery, state.Dependency)
                .Complete();
            searchTargetEcb.Playback(state.EntityManager);

            _soldierPositionLookup.Update(ref state);

            using EntityCommandBuffer checkTargetValidEcb = new(Allocator.TempJob);
            new CheckTargetValidJob
                {
                    EntityCommandBuffer = checkTargetValidEcb.AsParallelWriter(),

                    SoldierPositionLookup = _soldierPositionLookup
                }
                .ScheduleParallel(_checkTargetValidQuery, state.Dependency)
                .Complete();
            checkTargetValidEcb.Playback(state.EntityManager);

            if (!_moveInFormationSoldierQuery.IsEmpty)
            {
                state.EntityManager.GetAllUniqueSharedComponents(out NativeList<Formation> formations, Allocator.Temp);

                foreach (Formation formation in formations)
                {
                    _moveInFormationSoldierQuery.SetSharedComponentFilter(formation);
                    if (_moveInFormationSoldierQuery.IsEmpty)
                    {
                        continue;
                    }

                    float minMaxSpeed = float.MaxValue;

                    NativeArray<MoveSpeed> moveSpeeds = _moveInFormationSoldierQuery.ToComponentDataArray<MoveSpeed>(Allocator.Temp);

                    foreach (MoveSpeed moveSpeed in moveSpeeds)
                    {
                        if (minMaxSpeed > moveSpeed.Max)
                        {
                            minMaxSpeed = moveSpeed.Max;
                        }
                    }

                    using EntityCommandBuffer setMaxSpeedJobEcb = new(Allocator.TempJob);
                    new SetMaxSpeedJob
                        {
                            EntityCommandBuffer = setMaxSpeedJobEcb.AsParallelWriter(),
                            MaxSpeed = minMaxSpeed
                        }
                        .ScheduleParallel(_moveInFormationSoldierQuery, state.Dependency)
                        .Complete();
                    setMaxSpeedJobEcb.Playback(state.EntityManager);
                }

                _moveInFormationSoldierQuery.ResetFilter();
            }

            using EntityCommandBuffer restoreMaxSpeedJobEcb = new(Allocator.TempJob);
            new RestoreMaxSpeedJob
                {
                    EntityCommandBuffer = restoreMaxSpeedJobEcb.AsParallelWriter(),
                }
                .ScheduleParallel(_notMoveInFormationSoldierQuery, state.Dependency)
                .Complete();
            restoreMaxSpeedJobEcb.Playback(state.EntityManager);
        }
    }
}