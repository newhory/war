using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;


namespace War.Dots.Component.ComponentSystem
{
    [UpdateInGroup(typeof(AfterPhysicsSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct ArrowSystem : ISystem
    {
        [BurstCompile]
        private partial struct SetForwardJob : IJobEntity
        {
            public void Execute(ref Forward forward, in PhysicsVelocity velocity) => forward.Value = math.normalize(velocity.Linear);
        }

        [BurstCompile]
        private struct CollisionEventJob : ICollisionEventsJob
        {
            [ReadOnly] public ComponentLookup<Arrow> ArrowLookup;

            public NativeParallelMultiHashMap<Entity, Entity>.ParallelWriter CollisionEvents;


            public void Execute(CollisionEvent collisionEvent)
            {
                Entity entityA = collisionEvent.EntityA;
                Entity entityB = collisionEvent.EntityB;

                if (ArrowLookup.HasComponent(entityA))
                {
                    CollisionEvents.Add(entityA, entityB);
                }
                else if (ArrowLookup.HasComponent(entityB))
                {
                    CollisionEvents.Add(entityB, entityA);
                }
            }
        }

        [BurstCompile]
        private partial struct ProcessCollisionEventJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter EntityCommandBuffer;

            [ReadOnly] public NativeParallelMultiHashMap<Entity, Entity>.ReadOnly CollisionEvents;

            [ReadOnly] public BufferLookup<Damaged> DamagedLookup;
            [ReadOnly] public BufferLookup<SpawnHitEffect> SpawnHitEffectLookup;

            [ReadOnly] public double CurrentTime;


            public void Execute(Entity entity, in Arrow arrow, in Team team, in LocalTransform localTransform, in AttackPower attackPower)
            {
                if (!CollisionEvents.TryGetFirstValue(entity, out Entity targetEntity, out NativeParallelMultiHashMapIterator<Entity> iterator))
                {
                    return;
                }

                do
                {
                    if (DamagedLookup.HasBuffer(targetEntity))
                    {
                        EntityCommandBuffer.AppendToBuffer(targetEntity.Index, targetEntity, new Damaged { Hitter = arrow.Shooter, HitDamage = attackPower.Value });
                    }

                    if (SpawnHitEffectLookup.HasBuffer(targetEntity))
                    {
                        EntityCommandBuffer.AppendToBuffer(targetEntity.Index, targetEntity, new SpawnHitEffect { Position = localTransform.Position });
                    }
                } while (CollisionEvents.TryGetNextValue(out targetEntity, ref iterator));

                EntityCommandBuffer.AddComponent(entity.Index, entity, new DestroyOn { DestroyTime = CurrentTime });
            }
        }


        private EntityQuery _arrowQuery;

        private ComponentLookup<Arrow> _arrowLookup;
        private BufferLookup<Damaged> _damagedLookup;
        private BufferLookup<SpawnHitEffect> _spawnHitEffectLookup;

        private NativeParallelMultiHashMap<Entity, Entity> _arrowCollisionEvents;


        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimulationSingleton>();

            _arrowQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<Arrow, Team, LocalTransform, AttackPower, PhysicsVelocity>()
                    .WithAllRW<Forward>()
                    .WithNone<DestroyOn>()
                    .Build();

            _arrowLookup = state.GetComponentLookup<Arrow>(true);
            _damagedLookup = state.GetBufferLookup<Damaged>(true);
            _spawnHitEffectLookup = state.GetBufferLookup<SpawnHitEffect>(true);

            _arrowCollisionEvents = new NativeParallelMultiHashMap<Entity, Entity>(1024, Allocator.Domain);
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            _arrowLookup.Update(ref state);
            _damagedLookup.Update(ref state);
            _spawnHitEffectLookup.Update(ref state);

            state.Dependency = new SetForwardJob().ScheduleParallel(_arrowQuery, state.Dependency);

            SimulationSingleton simulationSingleton = SystemAPI.GetSingleton<SimulationSingleton>();

            new CollisionEventJob
                {
                    CollisionEvents = _arrowCollisionEvents.AsParallelWriter(),

                    ArrowLookup = _arrowLookup,
                }
                .Schedule(simulationSingleton, state.Dependency)
                .Complete();

            if (!_arrowCollisionEvents.IsEmpty)
            {
                using EntityCommandBuffer ecb = new(Allocator.TempJob);
                new ProcessCollisionEventJob
                    {
                        EntityCommandBuffer = ecb.AsParallelWriter(),

                        CollisionEvents = _arrowCollisionEvents.AsReadOnly(),

                        DamagedLookup = _damagedLookup,
                        SpawnHitEffectLookup = _spawnHitEffectLookup,

                        CurrentTime = SystemAPI.Time.ElapsedTime,
                    }
                    .ScheduleParallel(_arrowQuery, state.Dependency)
                    .Complete();
                ecb.Playback(state.EntityManager);

                _arrowCollisionEvents.Clear();
            }
        }
    }
}