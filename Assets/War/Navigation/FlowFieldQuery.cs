using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;


namespace War.Navigation
{
    [BurstCompile]
    public static class FlowFieldQuery
    {
        private static readonly int2[] s_offsets = { new(-1, 0), new(1, 0), new(0, -1), new(0, 1), new(-1, 1), new(1, 1), new(-1, -1), new(1, -1) };


        public static int WorldToIndex(in float3 worldPos, in int2 gridSize, float cellSize, in float3 minWorldPositionInGrid) =>
            CellToIndex(WorldToCell(worldPos, gridSize, cellSize, minWorldPositionInGrid), gridSize);

        public static int2 WorldToCell(in float3 worldPos, in int2 gridSize, float cellSize, in float3 minWorldPositionInGrid)
        {
            float3 projectedPos = worldPos - minWorldPositionInGrid;
            float invCellSize = 1f / cellSize;

            int x = (int)math.clamp(math.floor(projectedPos.x * invCellSize), 0, gridSize.x - 1);
            int y = (int)math.clamp(math.floor(projectedPos.z * invCellSize), 0, gridSize.y - 1);

            return new int2(x, y);
        }

        public static int CellToIndex(in int2 cell, in int2 gridSize) => cell.y * gridSize.x + cell.x;
        public static int2 IndexToCell(int index, in int2 gridSize) => new(index % gridSize.x, index / gridSize.x);

        public static float3 IndexToWorldPosition(int index, in int2 gridSize, float cellSize, in float3 minWorldPositionInGrid)
        {
            int2 cell = IndexToCell(index, gridSize);
            return minWorldPositionInGrid + new float3(cell.x, 0, cell.y) * cellSize + new float3(cellSize * 0.5f, 0, cellSize * 0.5f);
        }

        public static bool IsWalkable(in float3 worldPos, in NativeArray<byte>.ReadOnly navMeshMask, in int2 gridSize, float cellSize, in float3 minWorldPositionInGrid) =>
            navMeshMask[WorldToIndex(worldPos, gridSize, cellSize, minWorldPositionInGrid)] == 0;

        public static bool TryFindNearestWalkableWorldPosition(in float3 worldPos, in NativeArray<byte>.ReadOnly navMeshMask, in int2 gridSize, float cellSize, in float3 minWorldPositionInGrid, out float3 worldPosition)
        {
            int index = FindNearestWalkableIndexInNavMeshMask(WorldToIndex(worldPos, gridSize, cellSize, minWorldPositionInGrid), navMeshMask, gridSize);

            if (navMeshMask[index] != 0)
            {
                worldPosition = worldPos;

                return false;
            }

            worldPosition = IndexToWorldPosition(index, gridSize, cellSize, minWorldPositionInGrid);

            return true;
        }

        public static int FindNearestWalkableIndexInNavMeshMask(int startIndex, in NativeArray<byte>.ReadOnly navMeshMask, in int2 gridSize)
        {
            NativeQueue<int> searchQueue = new(Allocator.Temp);
            NativeArray<bool> visited = new(gridSize.x * gridSize.y, Allocator.Temp);

            searchQueue.Enqueue(startIndex);
            visited[startIndex] = true;

            int findIndex = startIndex;

            while (searchQueue.TryDequeue(out int index))
            {
                if (navMeshMask[index] == 0)
                {
                    findIndex = index;
                    break;
                }

                int2 cell = IndexToCell(index, gridSize);

                foreach (int2 offset in s_offsets)
                {
                    int2 nextCell = cell + offset;
                    if (nextCell.x < 0 || nextCell.y < 0 || nextCell.x >= gridSize.x || nextCell.y >= gridSize.y)
                    {
                        continue;
                    }

                    int nextIndex = CellToIndex(nextCell, gridSize);
                    if (!visited[nextIndex])
                    {
                        visited[nextIndex] = true;
                        searchQueue.Enqueue(nextIndex);
                    }
                }
            }

            searchQueue.Dispose();
            visited.Dispose();

            return findIndex;
        }

        public static void GetNeighborCells(int2 cell, in int2 gridSize, ref NativeList<int2> neighborCells)
        {
            foreach (int2 offset in s_offsets)
            {
                int2 neighbor = cell + offset;

                // 그리드 범위 체크
                if (neighbor.x >= 0 && neighbor.x < gridSize.x &&
                    neighbor.y >= 0 && neighbor.y < gridSize.y)
                {
                    neighborCells.Add(neighbor);
                }
            }
        }
    }
}