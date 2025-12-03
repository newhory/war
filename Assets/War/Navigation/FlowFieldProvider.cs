using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.AI;


namespace War.Navigation
{
    // 런타임 저장소 (서비스)
    public static class FlowFieldProvider
    {
        public static float CellSize { get; private set; }
        public static int2 GridSize { get; private set; }

        private static NativeArray<float2> directionField; // 방향 벡터(정규화)
        private static NativeArray<int> costField; // BFS 비용(셀 단계 수)
        private static NativeArray<byte> navMeshMask;
        private static NativeQueue<int> bfsQueue;


        public static float3 MinWorldPositionInGrid { get; private set; }

        public static NativeArray<float2>.ReadOnly DirectionField
        {
            get
            {
                if (!directionField.IsCreated)
                {
                    directionField = new NativeArray<float2>(1, Allocator.Persistent);
                }
                
                return directionField.AsReadOnly();
            }
        }

        public static NativeArray<byte>.ReadOnly NavMeshMask => navMeshMask.AsReadOnly();


        public static void Init(float newCellSize, int2 newGridSize, float3 minWorldPositionInGrid, Allocator alloc)
        {
            if (GridSize.x == newGridSize.x && GridSize.y == newGridSize.y &&
                mathf.Approximately(CellSize, newCellSize) &&
                mathf.Approximately(MinWorldPositionInGrid, minWorldPositionInGrid))
            {
                return;
            }

            if (GridSize.x != newGridSize.x || GridSize.y != newGridSize.y)
            {
                Dispose();

                int count = newGridSize.x * newGridSize.y;

                directionField = new NativeArray<float2>(count, alloc);
                costField = new NativeArray<int>(count, alloc);
                navMeshMask = new NativeArray<byte>(count, alloc);
                bfsQueue = new NativeQueue<int>(alloc);

                GridSize = newGridSize;
            }

            CellSize = newCellSize;
            MinWorldPositionInGrid = minWorldPositionInGrid;

            BuildNavMeshMask();
        }

        public static void BuildDirectionField(NativeArray<FlowFieldTarget>.ReadOnly targets, NativeArray<byte>.ReadOnly obstacleMask)
        {
            BuildBfs(targets, obstacleMask);

            // 방향 필드 구성(가장 낮은 비용 이웃으로 그라디언트 하강)
            for (int y = 0; y < GridSize.y; y++)
            {
                for (int x = 0; x < GridSize.x; x++)
                {
                    int idx = y * GridSize.x + x;
                    if (costField[idx] == int.MaxValue)
                    {
                        directionField[idx] = float2.zero;
                        continue;
                    }

                    int bestCost = costField[idx];
                    float2 best = float2.zero;

                    Consider(x + 1, y, new float2(1, 0));
                    Consider(x - 1, y, new float2(-1, 0));
                    Consider(x, y + 1, new float2(0, 1));
                    Consider(x, y - 1, new float2(0, -1));

                    directionField[idx] = math.normalizesafe(best);

                    continue;

                    void Consider(int nx, int ny, float2 offset)
                    {
                        if (nx < 0 || ny < 0 || nx >= GridSize.x || ny >= GridSize.y)
                        {
                            return;
                        }

                        int nIdx = ny * GridSize.x + nx;
                        if (costField[nIdx] < bestCost)
                        {
                            bestCost = costField[nIdx];
                            best = offset;
                        }
                    }
                }
            }
        }

        public static void Dispose()
        {
            if (directionField.IsCreated)
            {
                directionField.Dispose();
            }

            if (costField.IsCreated)
            {
                costField.Dispose();
            }

            if (navMeshMask.IsCreated)
            {
                navMeshMask.Dispose();
            }

            if (bfsQueue.IsCreated)
            {
                bfsQueue.Dispose();
            }
        }

        private static void BuildNavMeshMask()
        {
            float2 currentWorldPositionInGrid = MinWorldPositionInGrid.xz;

            for (int gridY = 0; gridY < GridSize.y; gridY++)
            {
                currentWorldPositionInGrid.x = MinWorldPositionInGrid.x;

                for (int gridX = 0; gridX < GridSize.x; gridX++)
                {
                    Vector3 center = new(currentWorldPositionInGrid.x + CellSize * 0.5f, 0, currentWorldPositionInGrid.y + CellSize * 0.5f);

                    if (!NavMesh.SamplePosition(center, out NavMeshHit _, CellSize, NavMesh.AllAreas))
                    {
                        int idx = gridX + gridY * GridSize.x;

                        navMeshMask[idx] = 1;
                    }

                    currentWorldPositionInGrid.x += CellSize;
                }

                currentWorldPositionInGrid.y += CellSize;
            }
        }

        private static void BuildBfs(NativeArray<FlowFieldTarget>.ReadOnly targets, NativeArray<byte>.ReadOnly obstacleMask)
        {
            // BFS 비용 초기화
            for (int i = 0; i < costField.Length; i++)
            {
                costField[i] = int.MaxValue;
            }

            // 다중 소스 BFS 큐 초기화(모든 목적지 셀을 소스에 삽입)
            bfsQueue.Clear();
            for (int k = 0; k < targets.Length; k++)
            {
                int index = FlowFieldQuery.WorldToIndex(targets[k].Position, GridSize, CellSize, MinWorldPositionInGrid);
                if (obstacleMask[index] == 0)
                {
                    costField[index] = 0;
                    bfsQueue.Enqueue(index);
                }
            }

            // 4-이웃 BFS
            while (bfsQueue.Count > 0)
            {
                int idx = bfsQueue.Dequeue();
                int2 c = new(idx % GridSize.x, idx / GridSize.x);
                int currCost = costField[idx];

                // 4방향
                TryRelax(c + new int2(1, 0), currCost, obstacleMask);
                TryRelax(c + new int2(-1, 0), currCost, obstacleMask);
                TryRelax(c + new int2(0, 1), currCost, obstacleMask);
                TryRelax(c + new int2(0, -1), currCost, obstacleMask);
            }
        }

        private static void TryRelax(int2 n, int currCost, NativeArray<byte>.ReadOnly mask)
        {
            if (n.x < 0 || n.y < 0 || n.x >= GridSize.x || n.y >= GridSize.y)
            {
                return;
            }

            int nIdx = n.y * GridSize.x + n.x;
            if (mask[nIdx] != 0) // 장애물 셀은 통과 불가
            {
                return;
            }

            int newCost = currCost + 1;
            if (newCost < costField[nIdx])
            {
                costField[nIdx] = newCost;
                bfsQueue.Enqueue(nIdx);
            }
        }
    }
}