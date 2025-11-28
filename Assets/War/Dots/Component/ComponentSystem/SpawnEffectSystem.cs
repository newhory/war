using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;


namespace War.Dots.Component.ComponentSystem
{
    [UpdateInGroup(typeof(Group.SpawnSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct SpawnEffectSystem : ISystem
    {
        [BurstCompile]
        private partial struct SpawnEffectJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter EntityCommandBuffer;

            [ReadOnly] public Entity ProtoType;
            [ReadOnly] public float CurrentTime;
            [ReadOnly] public float Duration;


            private void Execute([EntityIndexInQuery] int index, DynamicBuffer<SpawnHitEffect> hitEffectDataBuffer)
            {
                if (hitEffectDataBuffer.IsEmpty)
                {
                    return;
                }

                foreach (SpawnHitEffect hitEffectData in hitEffectDataBuffer)
                {
                    Entity effectEntity = EntityCommandBuffer.Instantiate(index, ProtoType);

                    float3 effectPosition = hitEffectData.Position;

                    EntityCommandBuffer.AddComponent(index, effectEntity, new DestroyOn { DestroyTime = CurrentTime + Duration });
                    EntityCommandBuffer.AddComponent(index, effectEntity, new LocalTransform { Position = effectPosition, Scale = 1f });
                    EntityCommandBuffer.AddComponent(index, effectEntity, new Forward { Value = new float3(0, 0, 1) });
                }

                hitEffectDataBuffer.Clear();
            }
        }


        private EntityQuery _hitEffectDataQuery;


        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EffectSpawner>();

            _hitEffectDataQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<SpawnHitEffect>()
                    .Build();
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            EffectSpawner effectSpawner = SystemAPI.GetSingleton<EffectSpawner>();

            EndSimulationEntityCommandBufferSystem ecbSystem = state.World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();
            float currentTime = (float)SystemAPI.Time.ElapsedTime;

            SpawnEffect(
                ref state,
                _hitEffectDataQuery,
                ecbSystem,
                effectSpawner.HitEffectProtoType,
                currentTime,
                effectSpawner.HitEffectDuration);
        }

        private void SpawnEffect(ref SystemState state, EntityQuery effectDataQuery, EntityCommandBufferSystem ecbSystem, in Entity protoType, float currentTime, float duration)
        {
            if (effectDataQuery.IsEmpty)
            {
                return;
            }

            EntityCommandBuffer ecb = ecbSystem.CreateCommandBuffer();
            JobHandle dependency =
                new SpawnEffectJob
                    {
                        EntityCommandBuffer = ecb.AsParallelWriter(),

                        ProtoType = protoType,
                        CurrentTime = currentTime,
                        Duration = duration,
                    }
                    .ScheduleParallel(effectDataQuery, state.Dependency);
            ecbSystem.AddJobHandleForProducer(dependency);

            state.Dependency = dependency;
        }
    }
}