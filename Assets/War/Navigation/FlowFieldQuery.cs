using Unity.Collections;
using Unity.Mathematics;


namespace War.Navigation
{
    public static class FlowFieldQuery
    {
        public static int WorldToIndex(in float3 worldPos, in int2 gridSize, float cellSize, in float3 minWorldPositionInGrid) =>
            CellToIndex(WorldToCell(worldPos, gridSize, cellSize, minWorldPositionInGrid), gridSize);

        public static bool TrySampleDir(int flowId, NativeArray<float2>.ReadOnly directionField, float3 worldPos, float cellSize, int2 gridSize, float3 minWorldPositionInGrid, out float2 flow)
        {
            flow = float2.zero;

            if (flowId < 0 || !directionField.IsCreated || directionField.Length <= 1)
            {
                return false;
            }

            int index = WorldToIndex(worldPos, gridSize, cellSize, minWorldPositionInGrid);

            float2 direction = directionField[index];
            if (math.lengthsq(direction) > 1e-6f)
            {
                flow = direction;
            }

            return true;
        }

        private static int2 WorldToCell(in float3 worldPos, in int2 gridSize, float cellSize, in float3 minWorldPositionInGrid)
        {
            float3 projectedPos = worldPos - minWorldPositionInGrid;
            float invCellSize = 1f / cellSize;

            int x = (int)math.clamp(math.floor(projectedPos.x * invCellSize), 0, gridSize.x - 1);
            int y = (int)math.clamp(math.floor(projectedPos.z * invCellSize), 0, gridSize.y - 1);

            return new int2(x, y);
        }

        private static int CellToIndex(in int2 cell, in int2 gridSize) => cell.y * gridSize.x + cell.x;
    }
}