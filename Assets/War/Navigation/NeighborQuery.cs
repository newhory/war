using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;


namespace War.Navigation
{
    [BurstCompile]
    public static class NeighborQuery
    {
        public static int Collect(
            in float3 position, in float3 forward, int maxCount, float cellSize,
            NativeParallelMultiHashMap<int, NeighborRecord>.ReadOnly hash,
            ref NativeArray<NeighborRecord> neighborRecordBuffer)
        {
            int count = 0;
            int2 baseCell = CellOf(position, cellSize);
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int2 neighborCell = new(baseCell.x + dx, baseCell.y + dy);
                    if (hash.TryGetFirstValue(HashKey(neighborCell), out NeighborRecord neighborRecord, out NativeParallelMultiHashMapIterator<int> iterator))
                    {
                        do
                        {
                            float3 d = neighborRecord.Position - position;
                            float dist = math.length(d);
                            if (dist < 5f)
                            {
                                // 3~5 radii; conservative constant in open field
                                float3 dir = math.normalizesafe(new float3(d.x, 0, d.z));
                                float dot = math.dot(dir, forward);
                                if (dot > math.cos(math.radians(60f)))
                                {
                                    neighborRecordBuffer[count] = neighborRecord;
                                    count++;
                                }
                            }
                        } while (hash.TryGetNextValue(out neighborRecord, ref iterator) && count < maxCount);
                    }

                    if (count >= maxCount)
                    {
                        break;
                    }
                }

                if (count >= maxCount)
                {
                    break;
                }
            }

            return count;
        }

        public static int2 CellOf(float3 pos, float cellSize)
        {
            float invCellSize = 1f / cellSize;

            return new int2(
                (int)math.floor(pos.x * invCellSize),
                (int)math.floor(pos.z * invCellSize)
            );
        }

        public static int HashKey(int2 c) => (c.x * 73856093) ^ (c.y * 19349663);
        public static int HashKey(float3 pos, float cellSize) => HashKey(CellOf(pos, cellSize));
    }
}