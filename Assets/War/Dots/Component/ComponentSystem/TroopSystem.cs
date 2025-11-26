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
            public NativeParallelMultiHashMap<Entity, float2>.ParallelWriter SoldierLocalTransformLookup;


            public void Execute(in SoldierAttachedTroop soldierAttachedTroop, in LocalTransform localTransform) => SoldierLocalTransformLookup.Add(soldierAttachedTroop.TroopEntity, localTransform.Position.xz);
        }

        [BurstCompile]
        private partial struct FillTroopSoldierPositionBufferJob : IJobEntity
        {
            [ReadOnly] public NativeParallelMultiHashMap<Entity, float2>.ReadOnly SoldierLocalTransformLookup;


            public void Execute(Entity troopEntity, DynamicBuffer<TroopSoldierPosition> troopSoldierPositionBuffer)
            {
                troopSoldierPositionBuffer.Clear();

                if (!SoldierLocalTransformLookup.TryGetFirstValue(troopEntity, out float2 value, out NativeParallelMultiHashMapIterator<Entity> iterator))
                {
                    return;
                }

                do
                {
                    troopSoldierPositionBuffer.Add(new TroopSoldierPosition { Position = value });
                } while (SoldierLocalTransformLookup.TryGetNextValue(out value, ref iterator));
            }
        }

        [BurstCompile]
        private partial struct FillTroopHullPointJob : IJobEntity
        {
            public void Execute(
                DynamicBuffer<TroopSoldierPosition> troopSoldierPositions,
                DynamicBuffer<TroopHullPoint> troopHullPointBuffer,
                DynamicBuffer<TroopSoldierIndexBuffer> troopSoldierIndexBuffer,
                DynamicBuffer<TroopLowerSoldierIndexBuffer> troopLowerSoldierIndexBuffer,
                DynamicBuffer<TroopUpperSoldierIndexBuffer> troopUpperSoldierIndexBuffer,
                ref TroopAABB troopAABB)
            {
                if (troopSoldierPositions.IsEmpty)
                {
                    return;
                }

                troopHullPointBuffer.Clear();

                int troopSoldierPositionCount = troopSoldierPositions.Length;

                // If only 1 or 2 points, copy as-is (no hull)
                if (troopSoldierPositionCount <= 2)
                {
                    switch (troopSoldierPositionCount)
                    {
                        case 1:
                            troopHullPointBuffer.Add(new TroopHullPoint { Position = troopSoldierPositions[0].Position });
                            break;

                        case 2:
                            troopHullPointBuffer.Add(new TroopHullPoint { Position = troopSoldierPositions[0].Position });
                            troopHullPointBuffer.Add(new TroopHullPoint { Position = troopSoldierPositions[1].Position });
                            break;
                    }
                }
                else
                {
                    // Compute convex hull via Monotone Chain
                    troopSoldierIndexBuffer.Clear();
                    for (int i = 0; i < troopSoldierPositionCount; ++i)
                    {
                        troopSoldierIndexBuffer.Add(new TroopSoldierIndexBuffer { Index = i });
                    }

                    // Sort indices by x then y
                    troopSoldierIndexBuffer.AsNativeArray().Sort(new TroopSoldierPositionComparer(troopSoldierPositions));

                    // Build lower and upper hulls (store indices)
                    troopLowerSoldierIndexBuffer.Clear();

                    for (int i = 0, count = troopSoldierIndexBuffer.Length; i < count; ++i)
                    {
                        int soldierPositionIndex = troopSoldierIndexBuffer[i].Index;
                        while (
                            troopLowerSoldierIndexBuffer.Length >= 2 &&
                            Cross(
                                troopSoldierPositions[troopLowerSoldierIndexBuffer[^2].Index].Position,
                                troopSoldierPositions[troopLowerSoldierIndexBuffer[^1].Index].Position,
                                troopSoldierPositions[soldierPositionIndex].Position) <= 0f)
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
                                troopSoldierPositions[troopUpperSoldierIndexBuffer[^2].Index].Position,
                                troopSoldierPositions[troopUpperSoldierIndexBuffer[^1].Index].Position,
                                troopSoldierPositions[soldierPositionIndex].Position) <= 0f)
                        {
                            troopUpperSoldierIndexBuffer.RemoveAtSwapBack(troopUpperSoldierIndexBuffer.Length - 1);
                        }

                        troopUpperSoldierIndexBuffer.Add(new TroopUpperSoldierIndexBuffer { Index = soldierPositionIndex });
                    }

                    // Concatenate lower and upper to get full hull (exclude the last element of each because it's repeated)
                    for (int i = 0, count = troopLowerSoldierIndexBuffer.Length - 1; i < count; ++i)
                    {
                        troopHullPointBuffer.Add(new TroopHullPoint { Position = troopSoldierPositions[troopLowerSoldierIndexBuffer[i].Index].Position });
                    }

                    for (int i = 0, count = troopUpperSoldierIndexBuffer.Length - 1; i < count; ++i)
                    {
                        troopHullPointBuffer.Add(new TroopHullPoint { Position = troopSoldierPositions[troopUpperSoldierIndexBuffer[i].Index].Position });
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

        private struct TroopSoldierPositionComparer : IComparer<TroopSoldierIndexBuffer>
        {
            private DynamicBuffer<TroopSoldierPosition> _buffer;


            public TroopSoldierPositionComparer(DynamicBuffer<TroopSoldierPosition> buffer) => _buffer = buffer;


            public int Compare(TroopSoldierIndexBuffer ia, TroopSoldierIndexBuffer ib)
            {
                float2 a = _buffer[ia.Index].Position;
                float2 b = _buffer[ib.Index].Position;

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
                    .WithAll<Troop, TroopEntity, TroopSoldierPosition, TroopHullPoint, TroopAABB>()
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

            JobHandle dependencies = state.Dependency;

            NativeParallelMultiHashMap<Entity, float2> soldierLocalTransformLookup = new(_soldierQuery.CalculateEntityCount(), Allocator.TempJob);

            dependencies =
                new CollectSoldierPositionJob
                    {
                        SoldierLocalTransformLookup = soldierLocalTransformLookup.AsParallelWriter()
                    }
                    .ScheduleParallel(_soldierQuery, dependencies);

            dependencies =
                new FillTroopSoldierPositionBufferJob
                    {
                        SoldierLocalTransformLookup = soldierLocalTransformLookup.AsReadOnly()
                    }
                    .ScheduleParallel(_troopQuery, dependencies);

            dependencies = new FillTroopHullPointJob().ScheduleParallel(_troopQuery, dependencies);

            state.Dependency = soldierLocalTransformLookup.Dispose(dependencies);
        }
    }
}