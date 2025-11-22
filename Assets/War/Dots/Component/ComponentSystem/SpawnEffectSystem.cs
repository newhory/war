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


            public void Execute([EntityIndexInQuery] int entityIndex, DynamicBuffer<SpawnHitEffect> hitEffectDataBuffer)
            {
                foreach (SpawnHitEffect hitEffectData in hitEffectDataBuffer)
                {
                    Entity effectEntity = EntityCommandBuffer.Instantiate(entityIndex, ProtoType);

                    float3 effectPosition = hitEffectData.Position;

                    EntityCommandBuffer.AddComponent(entityIndex, effectEntity, new DestroyOn { DestroyTime = CurrentTime + Duration });
                    EntityCommandBuffer.AddComponent(entityIndex, effectEntity, new LocalTransform { Position = effectPosition, Scale = 1f });
                    EntityCommandBuffer.AddComponent(entityIndex, effectEntity, new Forward { Value = new float3(0, 0, 1) });
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

            float currentTime = (float)SystemAPI.Time.ElapsedTime;

            SpawnEffect(
                _hitEffectDataQuery,
                ref state,
                ref effectSpawner.HitEffectProtoType,
                currentTime,
                effectSpawner.HitEffectDuration);
        }

        private void SpawnEffect(EntityQuery effectDataQuery, ref SystemState state, ref Entity protoType, float currentTime, float duration)
        {
            if (effectDataQuery.IsEmpty)
            {
                return;
            }

            EndSimulationEntityCommandBufferSystem ecbSystem = state.World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();

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