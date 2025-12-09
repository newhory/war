using Unity.Mathematics;


namespace War.Navigation
{
    public static class FlowFieldQuery
    {
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
    }
}