using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;

namespace War.Game.Systems
{
    [UpdateInGroup(typeof(AfterPhysicsSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct ArrowSystem : ISystem
    {
        [BurstCompile]
        private partial struct SetForwardJob : IJobEntity
        {
            private static void Execute(ref Forward forward, in PhysicsVelocity velocity) => forward.Value = math.normalize(velocity.Linear);
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


            private void Execute([EntityIndexInQuery] int index, Entity entity, in Arrow arrow, in LocalTransform localTransform, in AttackPower attackPower)
            {
                if (CollisionEvents.IsEmpty ||
                    !CollisionEvents.TryGetFirstValue(entity, out Entity targetEntity, out NativeParallelMultiHashMapIterator<Entity> iterator))
                {
                    return;
                }

                do
                {
                    if (DamagedLookup.HasBuffer(targetEntity))
                    {
                        EntityCommandBuffer.AppendToBuffer(index, targetEntity, new Damaged { Hitter = arrow.Shooter, HitDamage = attackPower.Value });
                    }

                    if (SpawnHitEffectLookup.HasBuffer(targetEntity))
                    {
                        EntityCommandBuffer.AppendToBuffer(index, targetEntity, new SpawnHitEffect { Position = localTransform.Position });
                    }
                } while (CollisionEvents.TryGetNextValue(out targetEntity, ref iterator));

                EntityCommandBuffer.AddComponent(index, entity, new DestroyOn { DestroyTime = CurrentTime });
            }
        }


        private EntityQuery _arrowQuery;

        private ComponentLookup<Arrow> _arrowLookup;
        private BufferLookup<Damaged> _damagedLookup;
        private BufferLookup<SpawnHitEffect> _spawnHitEffectLookup;


        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimulationSingleton>();

            _arrowQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<Arrow, LocalTransform, AttackPower, PhysicsVelocity>()
                    .WithAllRW<Forward>()
                    .WithNone<DestroyOn>()
                    .Build();

            _arrowLookup = state.GetComponentLookup<Arrow>(true);
            _damagedLookup = state.GetBufferLookup<Damaged>(true);
            _spawnHitEffectLookup = state.GetBufferLookup<SpawnHitEffect>(true);
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            _arrowLookup.Update(ref state);
            _damagedLookup.Update(ref state);
            _spawnHitEffectLookup.Update(ref state);

            JobHandle dependency = state.Dependency;

            dependency = new SetForwardJob().ScheduleParallel(_arrowQuery, dependency);

            NativeParallelMultiHashMap<Entity, Entity> arrowCollisionEvents = new(1024, Allocator.TempJob);
            SimulationSingleton simulationSingleton = SystemAPI.GetSingleton<SimulationSingleton>();

            dependency =
                new CollisionEventJob
                    {
                        CollisionEvents = arrowCollisionEvents.AsParallelWriter(),

                        ArrowLookup = _arrowLookup,
                    }
                    .Schedule(simulationSingleton, dependency);

            EndSimulationEntityCommandBufferSystem ecbSystem = state.World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();

            EntityCommandBuffer ecb = ecbSystem.CreateCommandBuffer();
            dependency =
                new ProcessCollisionEventJob
                    {
                        EntityCommandBuffer = ecb.AsParallelWriter(),

                        CollisionEvents = arrowCollisionEvents.AsReadOnly(),

                        DamagedLookup = _damagedLookup,
                        SpawnHitEffectLookup = _spawnHitEffectLookup,

                        CurrentTime = SystemAPI.Time.ElapsedTime,
                    }
                    .ScheduleParallel(_arrowQuery, dependency);
            ecbSystem.AddJobHandleForProducer(dependency);

            dependency = arrowCollisionEvents.Dispose(dependency);

            state.Dependency = dependency;
        }
    }
}