using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;


namespace War.Dots.Component.ComponentSystem
{
    [UpdateInGroup(typeof(Group.InputUpdateGroup), OrderLast = true)]
    [RequireMatchingQueriesForUpdate]
    public partial struct TroopSystem : ISystem
    {
        [BurstCompile]
        private partial struct CollectSoldierPositionJob : IJobEntity
        {
            public NativeParallelMultiHashMap<Entity, TroopSoldier>.ParallelWriter TroopSoldierLookup;


            private void Execute(Entity soldierEntity, in SoldierAttachedTroop soldierAttachedTroop, in LocalTransform localTransform) =>
                TroopSoldierLookup.Add(soldierAttachedTroop.TroopEntity, new TroopSoldier { Entity = soldierEntity, Position = localTransform.Position });
        }

        [BurstCompile]
        private partial struct FillTroopSoldierBufferJob : IJobEntity
        {
            [ReadOnly] public NativeParallelMultiHashMap<Entity, TroopSoldier>.ReadOnly TroopSoldierLookup;


            private void Execute(Entity troopEntity, DynamicBuffer<TroopSoldier> troopSoldierBuffer)
            {
                troopSoldierBuffer.Clear();

                if (!TroopSoldierLookup.TryGetFirstValue(troopEntity, out TroopSoldier value, out NativeParallelMultiHashMapIterator<Entity> iterator))
                {
                    return;
                }

                do
                {
                    troopSoldierBuffer.Add(value);
                } while (TroopSoldierLookup.TryGetNextValue(out value, ref iterator));
            }
        }

        [BurstCompile]
        private partial struct RemoveDeadTroopJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter EntityCommandBuffer;

            [ReadOnly] public double CurrentTime;


            private void Execute([EntityIndexInQuery] int index, Entity troopEntity, DynamicBuffer<TroopSoldier> troopSoldierBuffer)
            {
                if (troopSoldierBuffer.IsEmpty)
                {
                    EntityCommandBuffer.SetComponentEnabled<Alive>(index, troopEntity, false);

                    EntityCommandBuffer.AddComponent(index, troopEntity, new DestroyOn { DestroyTime = CurrentTime });
                }
            }
        }


        private EntityQuery _troopQuery;
        private EntityQuery _soldierQuery;


        public void OnCreate(ref SystemState state)
        {
            _troopQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<Troop, Alive, TroopEntity, TroopSoldier>()
                    .Build();

            _soldierQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<Soldier, Troop, Alive, LocalTransform, SoldierAttachedTroop>()
                    .Build();
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_troopQuery.IsEmpty)
            {
                return;
            }

            JobHandle dependency = state.Dependency;

            NativeParallelMultiHashMap<Entity, TroopSoldier> troopSoldierLookup = new(_soldierQuery.CalculateEntityCount(), Allocator.TempJob);

            dependency =
                new CollectSoldierPositionJob
                    {
                        TroopSoldierLookup = troopSoldierLookup.AsParallelWriter()
                    }
                    .ScheduleParallel(_soldierQuery, dependency);

            dependency =
                new FillTroopSoldierBufferJob
                    {
                        TroopSoldierLookup = troopSoldierLookup.AsReadOnly()
                    }
                    .ScheduleParallel(_troopQuery, dependency);
            
            dependency = troopSoldierLookup.Dispose(dependency);

            EndSimulationEntityCommandBufferSystem ecbSystem = state.World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();

            EntityCommandBuffer ecb = ecbSystem.CreateCommandBuffer();
            dependency = new RemoveDeadTroopJob { EntityCommandBuffer = ecb.AsParallelWriter(), CurrentTime = SystemAPI.Time.ElapsedTime }.Schedule(_troopQuery, dependency);
            ecbSystem.AddJobHandleForProducer(dependency);

            state.Dependency = dependency;
        }
    }
}