using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
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

        [BurstCompile]
        private partial struct FillTroopHullPointJob : IJobEntity
        {
            private static void Execute(
                DynamicBuffer<TroopSoldier> troopSoldierBuffer,
                DynamicBuffer<TroopHullPoint> troopHullPointBuffer,
                DynamicBuffer<TroopSoldierIndexBuffer> troopSoldierIndexBuffer,
                DynamicBuffer<TroopLowerSoldierIndexBuffer> troopLowerSoldierIndexBuffer,
                DynamicBuffer<TroopUpperSoldierIndexBuffer> troopUpperSoldierIndexBuffer,
                ref TroopAABB troopAABB)
            {
                if (troopSoldierBuffer.IsEmpty)
                {
                    return;
                }

                troopHullPointBuffer.Clear();

                int troopSoldierCount = troopSoldierBuffer.Length;

                // If only 1 or 2 points, copy as-is (no hull)
                if (troopSoldierCount <= 2)
                {
                    switch (troopSoldierCount)
                    {
                        case 1:
                        {
                            float2 pos0 = troopSoldierBuffer[0].Position.xz;
                            float padding = troopAABB.Padding + 0.25f;

                            troopHullPointBuffer.Add(new TroopHullPoint { Position = new float2(pos0.x - padding, pos0.y) });
                            troopHullPointBuffer.Add(new TroopHullPoint { Position = new float2(pos0.x, pos0.y + padding) });
                            troopHullPointBuffer.Add(new TroopHullPoint { Position = new float2(pos0.x + padding, pos0.y) });
                            troopHullPointBuffer.Add(new TroopHullPoint { Position = new float2(pos0.x, pos0.y - padding) });
                            break;
                        }

                        case 2:
                        {
                            float2 pos0 = troopSoldierBuffer[0].Position.xz;
                            float2 pos1 = troopSoldierBuffer[1].Position.xz;
                            float2 left, right, dir;

                            if (pos0.x < pos1.x)
                            {
                                (left, right) = (pos0, pos1);
                                dir = math.normalize(pos1 - pos0);
                            }
                            else
                            {
                                (left, right) = (pos1, pos0);
                                dir = math.normalize(pos0 - pos1);
                            }

                            float2 center = (pos0 + pos1) * 0.5f;
                            float2 up = new(dir.y, -dir.x);
                            float2 down = -up;
                            float padding = troopAABB.Padding + 0.25f;

                            troopHullPointBuffer.Add(new TroopHullPoint { Position = left - dir * padding });
                            troopHullPointBuffer.Add(new TroopHullPoint { Position = center + up * padding });
                            troopHullPointBuffer.Add(new TroopHullPoint { Position = right + dir * padding });
                            troopHullPointBuffer.Add(new TroopHullPoint { Position = center + down * padding });

                            break;
                        }
                    }
                }
                else
                {
                    // Compute convex hull via Monotone Chain
                    troopSoldierIndexBuffer.Clear();
                    for (int i = 0; i < troopSoldierCount; ++i)
                    {
                        troopSoldierIndexBuffer.Add(new TroopSoldierIndexBuffer { Index = i });
                    }

                    // Sort indices by x then y
                    troopSoldierIndexBuffer.AsNativeArray().Sort(new TroopSoldierComparer(troopSoldierBuffer));

                    // Build lower and upper hulls (store indices)
                    troopLowerSoldierIndexBuffer.Clear();

                    for (int i = 0, count = troopSoldierIndexBuffer.Length; i < count; ++i)
                    {
                        int soldierPositionIndex = troopSoldierIndexBuffer[i].Index;
                        while (
                            troopLowerSoldierIndexBuffer.Length >= 2 &&
                            Cross(
                                troopSoldierBuffer[troopLowerSoldierIndexBuffer[^2].Index].Position.xz,
                                troopSoldierBuffer[troopLowerSoldierIndexBuffer[^1].Index].Position.xz,
                                troopSoldierBuffer[soldierPositionIndex].Position.xz) <= 0f)
                        {
                            troopLowerSoldierIndexBuffer.RemoveAtSwapBack(troopLowerSoldierIndexBuffer.Length - 1);
                        }

                        troopLowerSoldierIndexBuffer.Add(new TroopLowerSoldierIndexBuffer { Index = soldierPositionIndex });
                    }

                    troopUpperSoldierIndexBuffer.Clear();

                    for (int i = troopSoldierIndexBuffer.Length - 1; i >= 0; --i)
                    {
                        int soldierPositionIndex = troopSoldierIndexBuffer[i].Index;
                        while (
                            troopUpperSoldierIndexBuffer.Length >= 2 &&
                            Cross(
                                troopSoldierBuffer[troopUpperSoldierIndexBuffer[^2].Index].Position.xz,
                                troopSoldierBuffer[troopUpperSoldierIndexBuffer[^1].Index].Position.xz,
                                troopSoldierBuffer[soldierPositionIndex].Position.xz) <= 0f)
                        {
                            troopUpperSoldierIndexBuffer.RemoveAtSwapBack(troopUpperSoldierIndexBuffer.Length - 1);
                        }

                        troopUpperSoldierIndexBuffer.Add(new TroopUpperSoldierIndexBuffer { Index = soldierPositionIndex });
                    }

                    // Concatenate lower and upper to get full hull (exclude the last element of each because it's repeated)
                    for (int i = 0, count = troopLowerSoldierIndexBuffer.Length - 1; i < count; ++i)
                    {
                        troopHullPointBuffer.Add(new TroopHullPoint { Position = troopSoldierBuffer[troopLowerSoldierIndexBuffer[i].Index].Position.xz });
                    }

                    for (int i = 0, count = troopUpperSoldierIndexBuffer.Length - 1; i < count; ++i)
                    {
                        troopHullPointBuffer.Add(new TroopHullPoint { Position = troopSoldierBuffer[troopUpperSoldierIndexBuffer[i].Index].Position.xz });
                    }
                }

                float minX = float.MaxValue, minZ = float.MaxValue;
                float maxX = float.MinValue, maxZ = float.MinValue;

                for (int i = 0, count = troopHullPointBuffer.Length; i < count; ++i)
                {
                    float2 p = troopHullPointBuffer[i].Position;
                    if (p.x < minX) minX = p.x;
                    if (p.y < minZ) minZ = p.y;
                    if (p.x > maxX) maxX = p.x;
                    if (p.y > maxZ) maxZ = p.y;
                }

                troopAABB.Min = new float2(minX - troopAABB.Padding, minZ - troopAABB.Padding);
                troopAABB.Max = new float2(maxX + troopAABB.Padding, maxZ + troopAABB.Padding);
                troopAABB.Center = (troopAABB.Min + troopAABB.Max) * 0.5f;
            }
        }

        private struct TroopSoldierComparer : IComparer<TroopSoldierIndexBuffer>
        {
            private DynamicBuffer<TroopSoldier> _buffer;


            public TroopSoldierComparer(DynamicBuffer<TroopSoldier> buffer) => _buffer = buffer;


            public int Compare(TroopSoldierIndexBuffer ia, TroopSoldierIndexBuffer ib)
            {
                float2 a = _buffer[ia.Index].Position.xz;
                float2 b = _buffer[ib.Index].Position.xz;

                if (a.x < b.x) return -1;
                if (a.x > b.x) return 1;
                if (a.y < b.y) return -1;
                if (a.y > b.y) return 1;

                return 0;
            }
        }


        // Cross product z-component of (b - a) x (c - a) in 2D (XZ)
        private static float Cross(float2 a, float2 b, float2 c) => (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);


        private EntityQuery _troopQuery;
        private EntityQuery _soldierQuery;


        public void OnCreate(ref SystemState state)
        {
            _troopQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<Troop, Alive, TroopEntity, TroopSoldier, TroopHullPoint, TroopAABB>()
                    .WithAll<TroopSoldierIndexBuffer, TroopLowerSoldierIndexBuffer, TroopUpperSoldierIndexBuffer>()
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

            EndSimulationEntityCommandBufferSystem ecbSystem = state.World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();

            EntityCommandBuffer ecb = ecbSystem.CreateCommandBuffer();
            dependency = new RemoveDeadTroopJob { EntityCommandBuffer = ecb.AsParallelWriter(), CurrentTime = SystemAPI.Time.ElapsedTime }.Schedule(_troopQuery, dependency);
            ecbSystem.AddJobHandleForProducer(dependency);

            dependency = new FillTroopHullPointJob().ScheduleParallel(_troopQuery, dependency);

            state.Dependency = troopSoldierLookup.Dispose(dependency);
        }
    }
}