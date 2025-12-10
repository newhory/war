using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.AI;


namespace War.Navigation
{
    [BurstCompile]
    public static class FlowFieldProvider
    {
        private static readonly int2[] s_offsets = { new(-1, 0), new(1, 0), new(0, -1), new(0, 1) };


        private struct QuadtreeNode
        {
            public int2 Min;
            public int2 Max;


            public int MinX
            {
                set => Min.x = value;
            }

            public int MinY
            {
                set => Min.y = value;
            }

            public int MaxX
            {
                set => Max.x = value;
            }

            public int MaxY
            {
                set => Max.y = value;
            }

            public int Width => Max.x - Min.x + 1;
            public int Height => Max.y - Min.y + 1;
        }

        private struct BoundForQuadtree
        {
            public SimpleBounds Bounds;
            public readonly float WalkableRatio;


            public float3 Min => Bounds.Min;
            public float3 Max => Bounds.Max;

            public float3 Center => Bounds.Center;
            public float3 Size => Bounds.Size;


            public BoundForQuadtree(SimpleBounds bounds, float walkableRatio)
                => (Bounds, WalkableRatio) = (bounds, walkableRatio);

            public BoundForQuadtree(float3 min, float3 max, float walkableRatio)
                : this(new SimpleBounds(min, max), walkableRatio)
            {
            }
        }

        private struct FlowFieldTargetForBuffer
        {
            public int FlowId;
            public SimpleBounds AreaBounds;
            public NativeArray<float2> DirectionField;

            public float3 Position => AreaBounds.Center;
        }

        [BurstCompile]
        private struct QuadtreeJob : IJob
        {
            [ReadOnly] public NativeArray<byte>.ReadOnly NavMeshMaskInJob; // 0 = walkable, 1 = blocked
            [ReadOnly] public int2 NavMeshGridSize;
            [ReadOnly] public float NavMeshCellSize;
            [ReadOnly] public float3 NavMeshMinWorldPosition;

            [ReadOnly] public int MinSize; // 최소 노드 크기 (예: 8)
            [ReadOnly] public float Tolerance; // 분할 기준 walkable 비율 (예: 0.9f)

            public NativeList<BoundForQuadtree> Bounds;


            public void Execute()
            {
                NativeQueue<QuadtreeNode> nodes = new(Allocator.Temp);
                nodes.Enqueue(new QuadtreeNode { MinX = 0, MinY = 0, MaxX = NavMeshGridSize.x - 1, MaxY = NavMeshGridSize.y - 1 });

                while (nodes.TryDequeue(out QuadtreeNode node))
                {
                    if (ShouldSplit(node, out float walkableRatio))
                    {
                        Split(node, nodes);
                    }
                    else
                    {
                        float3 minPos = new(NavMeshMinWorldPosition.x + node.Min.x * NavMeshCellSize, 0, NavMeshMinWorldPosition.z + node.Min.y * NavMeshCellSize);
                        float3 maxPos = new(NavMeshMinWorldPosition.x + (node.Max.x + 1) * NavMeshCellSize, 0, NavMeshMinWorldPosition.z + (node.Max.y + 1) * NavMeshCellSize);

                        Bounds.Add(new BoundForQuadtree(minPos, maxPos, walkableRatio));
                    }
                }

                nodes.Dispose();
            }

            private bool ShouldSplit(in QuadtreeNode node, out float walkableRatio)
            {
                int total = node.Width * node.Height;
                int walkable = 0;

                for (int y = node.Min.y; y <= node.Max.y; y++)
                {
                    int rowBase = y * NavMeshGridSize.x;
                    for (int x = node.Min.x; x <= node.Max.x; x++)
                    {
                        int index = rowBase + x;
                        if (NavMeshMaskInJob[index] == 0)
                        {
                            walkable++;
                        }
                    }
                }

                walkableRatio = (float)walkable / total;

                return node.Width > MinSize && node.Height > MinSize && walkableRatio < Tolerance;
            }

            private static void Split(in QuadtreeNode node, NativeQueue<QuadtreeNode> nodes)
            {
                int midX = (node.Min.x + node.Max.x) >> 1;
                int midY = (node.Min.y + node.Max.y) >> 1;

                nodes.Enqueue(new QuadtreeNode { MinX = node.Min.x, MinY = node.Min.y, MaxX = midX, MaxY = midY });
                nodes.Enqueue(new QuadtreeNode { MinX = midX + 1, MinY = node.Min.y, MaxX = node.Max.x, MaxY = midY });
                nodes.Enqueue(new QuadtreeNode { MinX = node.Min.x, MinY = midY + 1, MaxX = midX, MaxY = node.Max.y });
                nodes.Enqueue(new QuadtreeNode { MinX = midX + 1, MinY = midY + 1, MaxX = node.Max.x, MaxY = node.Max.y });
            }
        }

        [BurstCompile]
        private struct MergeBoundsJob : IJob
        {
            public NativeList<BoundForQuadtree> Bounds;

            [ReadOnly] public float ValidWalkableRatio;


            public void Execute()
            {
                for (int index = Bounds.Length - 1; index >= 0; index--)
                {
                    // blocked 40%이상 영역 제거
                    if (!HasWalkable(Bounds[index], ValidWalkableRatio))
                    {
                        Bounds.RemoveAtSwapBack(index);
                    }
                }

                bool isChanged;
                int safety = 0;
                do
                {
                    isChanged = false;

                    for (int i = Bounds.Length - 1; i >= 0; i--)
                    {
                        BoundForQuadtree current = Bounds[i];

                        int j = Bounds.Length - 1;
                        while (j >= 0)
                        {
                            if (j == i)
                            {
                                j--;
                                continue;
                            }

                            BoundForQuadtree other = Bounds[j];

                            if (CanMergeHorizontal(current.Bounds, other.Bounds))
                            {
                                isChanged = true;

                                current.Bounds = MergeHorizontal(current.Bounds, other.Bounds);
                                Bounds[i] = current;
                                Bounds.RemoveAtSwapBack(j);

                                i = Bounds.Length; // SwapBack 후 Bounds.Length가 감소됐으므로 처음부터 다시 검사
                                break;
                            }

                            if (CanMergeVertical(current.Bounds, other.Bounds))
                            {
                                isChanged = true;

                                current.Bounds = MergeVertical(current.Bounds, other.Bounds);
                                Bounds[i] = current;
                                Bounds.RemoveAtSwapBack(j);

                                i = Bounds.Length;
                                break;
                            }

                            j--;
                        }
                    }

                    safety++;
                } while (isChanged && safety < 1024);
            }

            private static bool HasWalkable(in BoundForQuadtree b, float tolerance) => b.WalkableRatio > tolerance;

            private static bool CanMergeHorizontal(in SimpleBounds a, in SimpleBounds b) =>
                mathf.Approximately(a.Min.z, b.Min.z) &&
                mathf.Approximately(a.Max.z, b.Max.z) &&
                mathf.Approximately(a.Max.x, b.Min.x);

            private static bool CanMergeVertical(in SimpleBounds a, in SimpleBounds b) =>
                mathf.Approximately(a.Min.x, b.Min.x) &&
                mathf.Approximately(a.Max.x, b.Max.x) &&
                mathf.Approximately(a.Max.z, b.Min.z);

            private static SimpleBounds MergeHorizontal(in SimpleBounds a, in SimpleBounds b) =>
                new(
                    new float3(math.min(a.Min.x, b.Min.x), 0, a.Min.z),
                    new float3(math.max(a.Max.x, b.Max.x), 0, a.Max.z)
                );

            private static SimpleBounds MergeVertical(in SimpleBounds a, in SimpleBounds b) =>
                new(
                    new float3(a.Min.x, 0, math.min(a.Min.z, b.Min.z)),
                    new float3(a.Max.x, 0, math.max(a.Max.z, b.Max.z))
                );
        }

        [BurstCompile]
        private struct ExpandBoundsJob : IJobParallelFor
        {
            public NativeArray<BoundForQuadtree> Bounds;

            [ReadOnly] public int2 NavMeshGridSize;
            [ReadOnly] public float NavMeshCellSize;
            [ReadOnly] public float3 NavMeshMinWorldPosition;


            public void Execute(int i)
            {
                BoundForQuadtree boundForQuadtree = Bounds[i];
                SimpleBounds bound = boundForQuadtree.Bounds;

                bound.Min.x = math.max(NavMeshMinWorldPosition.x, bound.Min.x - NavMeshCellSize);
                bound.Min.z = math.max(NavMeshMinWorldPosition.z, bound.Min.z - NavMeshCellSize);
                bound.Max.x = math.min((NavMeshGridSize.x - 1) * NavMeshCellSize, bound.Max.x + NavMeshCellSize);
                bound.Max.z = math.min((NavMeshGridSize.y - 1) * NavMeshCellSize, bound.Max.z + NavMeshCellSize);

                boundForQuadtree.Bounds = bound;

                Bounds[i] = boundForQuadtree;
            }
        }

        [BurstCompile]
        private struct BuildCostFieldJob : IJob
        {
            public NativeArray<float> CostField;

            [ReadOnly] public float3 FlowFieldTargetPosition;
            [ReadOnly] public NativeArray<byte>.ReadOnly NavMeshMaskInJob;
            [ReadOnly] public int2 NavMeshGridSize;
            [ReadOnly] public float NavMeshCellSize;
            [ReadOnly] public float3 NavMeshMinWorldPosition;


            public void Execute()
            {
                NativeQueue<int> bfsQueue = new(Allocator.Temp);

                for (int i = 0; i < CostField.Length; i++)
                {
                    CostField[i] = float.PositiveInfinity;
                }

                // 1. 타깃 셀 초기화
                int2 targetCell = FlowFieldQuery.WorldToCell(FlowFieldTargetPosition, NavMeshGridSize, NavMeshCellSize, NavMeshMinWorldPosition);
                int targetIndex = FlowFieldQuery.CellToIndex(targetCell, NavMeshGridSize);

                if (NavMeshMaskInJob[targetIndex] != 0)
                {
                    targetIndex = FindNearestWalkable(targetIndex);
                }

                CostField[targetIndex] = 0f;
                bfsQueue.Enqueue(targetIndex);

                // 2. Dijkstra 확산
                while (bfsQueue.TryDequeue(out int index))
                {
                    int2 cell = FlowFieldQuery.IndexToCell(index, NavMeshGridSize);
                    float currentCost = CostField[index];

                    Relax(cell, new int2(-1, 1), currentCost, bfsQueue);
                    Relax(cell, new int2(-1, 0), currentCost, bfsQueue);
                    Relax(cell, new int2(-1, -1), currentCost, bfsQueue);
                    Relax(cell, new int2(0, 1), currentCost, bfsQueue);
                    Relax(cell, new int2(0, -1), currentCost, bfsQueue);
                    Relax(cell, new int2(1, 1), currentCost, bfsQueue);
                    Relax(cell, new int2(1, 0), currentCost, bfsQueue);
                    Relax(cell, new int2(1, -1), currentCost, bfsQueue);
                }

                bfsQueue.Dispose();
            }

            private int FindNearestWalkable(int startIndex)
            {
                NativeQueue<int> searchQueue = new(Allocator.Temp);
                NativeArray<bool> visited = new(NavMeshGridSize.x * NavMeshGridSize.y, Allocator.Temp);

                searchQueue.Enqueue(startIndex);
                visited[startIndex] = true;

                int findIndex = startIndex;

                while (searchQueue.TryDequeue(out int index))
                {
                    int2 cell = FlowFieldQuery.IndexToCell(index, NavMeshGridSize);

                    if (NavMeshMaskInJob[index] == 0)
                    {
                        findIndex = index;

                        break;
                    }

                    foreach (int2 off in s_offsets)
                    {
                        int2 nextCell = cell + off;
                        if (nextCell.x < 0 || nextCell.y < 0 || nextCell.x >= NavMeshGridSize.x || nextCell.y >= NavMeshGridSize.y)
                        {
                            continue;
                        }

                        int nextIndex = FlowFieldQuery.CellToIndex(nextCell, NavMeshGridSize);
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

            private void Relax(int2 from, int2 offset, float currentCost, NativeQueue<int> bfsQueue)
            {
                int2 cell = from + offset;
                if (cell.x < 0 || cell.y < 0 || cell.x >= NavMeshGridSize.x || cell.y >= NavMeshGridSize.y)
                {
                    return;
                }

                int index = cell.y * NavMeshGridSize.x + cell.x;
                if (NavMeshMaskInJob[index] != 0)
                {
                    return;
                }

                bool diagonal = offset.x != 0 && offset.y != 0;
                if (diagonal)
                {
                    int2 adj1 = new(from.x + offset.x, from.y);
                    int2 adj2 = new(from.x, from.y + offset.y);
                    if (NavMeshMaskInJob[adj1.y * NavMeshGridSize.x + adj1.x] != 0 ||
                        NavMeshMaskInJob[adj2.y * NavMeshGridSize.x + adj2.y] != 0)
                    {
                        return;
                    }
                }

                float step = diagonal ? 1.41421356f : 1f;
                float newCost = currentCost + step * NavMeshCellSize;
                if (newCost < CostField[index])
                {
                    CostField[index] = newCost;
                    bfsQueue.Enqueue(index);
                }
            }
        }

        [BurstCompile]
        private struct BuildDirectionFieldJob : IJobParallelFor
        {
            public NativeArray<float2> DirectionField;

            [ReadOnly] public NativeArray<float>.ReadOnly CostField;
            [ReadOnly] public float3 FlowFieldTargetPosition;
            [ReadOnly] public NativeArray<byte>.ReadOnly NavMeshMaskInJob;
            [ReadOnly] public int2 NavMeshGridSize;
            [ReadOnly] public float NavMeshCellSize;
            [ReadOnly] public float3 NavMeshMinWorldPosition;


            public void Execute(int index)
            {
                int x = index % NavMeshGridSize.x;
                int y = index / NavMeshGridSize.x;

                if (NavMeshMaskInJob[index] == 0)
                {
                    DirectionField[index] = ComputeDirection(x, y, index);
                }
                else
                {
                    float2 bestDir = float2.zero;
                    float bestDist = float.PositiveInfinity;

                    // 주변 1~2셀 탐색
                    for (int dy = -2; dy <= 2; dy++)
                    {
                        for (int dx = -2; dx <= 2; dx++)
                        {
                            int nx = x + dx;
                            int ny = y + dy;
                            if (nx < 0 || ny < 0 || nx >= NavMeshGridSize.x || ny >= NavMeshGridSize.y)
                            {
                                continue;
                            }

                            int nIdx = ny * NavMeshGridSize.x + nx;
                            if (NavMeshMaskInJob[nIdx] != 0) // 유효 셀만
                            {
                                continue;
                            }

                            float dist = math.length(new float2(dx, dy));
                            if (dist < bestDist)
                            {
                                bestDist = dist;
                                bestDir = new float2(dx, dy);
                            }
                        }
                    }

                    DirectionField[index] = math.normalizesafe(bestDir);
                }
            }

            private float2 ComputeDirection(int x, int y, int index)
            {
                float currentCost = CostField[index];
                if (float.IsPositiveInfinity(currentCost))
                {
                    return float2.zero;
                }

                float dx = 0f, dy = 0f;
                float invCellSize = 1f / NavMeshCellSize;
                float invDoubleCellSize = invCellSize * 0.5f;

                // 좌/우 차분
                bool leftBlocked = false, rightBlocked = false;
                if (x > 0 && x < NavMeshGridSize.x - 1)
                {
                    int leftIndex = index - 1;
                    int rightIndex = index + 1;
                    leftBlocked = NavMeshMaskInJob[leftIndex] != 0;
                    rightBlocked = NavMeshMaskInJob[rightIndex] != 0;

                    if (!leftBlocked && !float.IsPositiveInfinity(CostField[leftIndex]) &&
                        !rightBlocked && !float.IsPositiveInfinity(CostField[rightIndex]))
                    {
                        dx = (CostField[rightIndex] - CostField[leftIndex]) * invDoubleCellSize;
                    }
                    else if (!rightBlocked && !float.IsPositiveInfinity(CostField[rightIndex]))
                    {
                        dx = (CostField[rightIndex] - currentCost) * invCellSize;
                    }
                    else if (!leftBlocked && !float.IsPositiveInfinity(CostField[leftIndex]))
                    {
                        dx = (currentCost - CostField[leftIndex]) * invCellSize;
                    }
                }

                // 상/하 차분
                bool downBlocked = false, upBlocked = false;
                if (y > 0 && y < NavMeshGridSize.y - 1)
                {
                    int downIndex = index - NavMeshGridSize.x;
                    int upIndex = index + NavMeshGridSize.x;
                    downBlocked = NavMeshMaskInJob[downIndex] != 0;
                    upBlocked = NavMeshMaskInJob[upIndex] != 0;

                    if (!downBlocked && !float.IsPositiveInfinity(CostField[downIndex]) &&
                        !upBlocked && !float.IsPositiveInfinity(CostField[upIndex]))
                    {
                        dy = (CostField[upIndex] - CostField[downIndex]) * invDoubleCellSize;
                    }
                    else if (!upBlocked && !float.IsPositiveInfinity(CostField[upIndex]))
                    {
                        dy = (CostField[upIndex] - currentCost) * invCellSize;
                    }
                    else if (!downBlocked && !float.IsPositiveInfinity(CostField[downIndex]))
                    {
                        dy = (currentCost - CostField[downIndex]) * invCellSize;
                    }
                }

                // gradient → 비용 감소 방향
                float2 direction = math.normalizesafe(-new float2(dx, dy));

                // 장애물 평행 보정
                if ((leftBlocked && direction.x < 0f) || (rightBlocked && direction.x > 0f))
                {
                    direction = new float2(0, direction.y);
                }

                if ((upBlocked && direction.y > 0f) || (downBlocked && direction.y < 0f))
                {
                    direction = new float2(direction.x, 0);
                }

                // Fallback: 코너 셀에서 방향이 완전히 사라질 경우
                if (math.lengthsq(direction) < 0.0001f)
                {
                    // 열린 축을 따라가거나 목표 방향 사용
                    if (!leftBlocked) direction = new float2(-1, 0);
                    else if (!rightBlocked) direction = new float2(1, 0);
                    else if (!upBlocked) direction = new float2(0, 1);
                    else if (!downBlocked) direction = new float2(0, -1);
                    else
                    {
                        float3 cellWorldPos = NavMeshMinWorldPosition + new float3(x * NavMeshCellSize, 0, y * NavMeshCellSize);
                        direction = math.normalizesafe((FlowFieldTargetPosition - cellWorldPos).xz);
                    }
                }

                return direction;
            }
        }


        private static NativeArray<byte> s_navMeshMask;


        public static float CellSize { get; private set; }
        public static int2 GridSize { get; private set; }
        public static float3 MinWorldPositionInGrid { get; private set; }

        public static BlobAssetReference<FlowFieldBlobRoot> FlowFieldFlowBlobAssetReference { get; private set; }
        public static NativeArray<byte>.ReadOnly NavMeshMask => s_navMeshMask.AsReadOnly();


        public static void Init(in NavigationGrid navigationGrid)
        {
            int2 newGridSize = navigationGrid.FlowFieldGridSize;
            float newCellSize = navigationGrid.FlowFieldCellSize;
            float3 minWorldPositionInGrid = navigationGrid.Min;

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


                s_navMeshMask = new NativeArray<byte>(count, Allocator.Persistent);

                GridSize = newGridSize;
            }

            CellSize = newCellSize;
            MinWorldPositionInGrid = minWorldPositionInGrid;

            BuildNavMeshMask();

            BuildFlowFieldFlowBlobAsset(navigationGrid);
        }

        public static void Dispose()
        {
            if (s_navMeshMask.IsCreated)
            {
                s_navMeshMask.Dispose();
            }
        }

        private static void BuildNavMeshMask()
        {
            float2 currentWorldPositionInGrid = MinWorldPositionInGrid.xz;

            for (int y = 0; y < GridSize.y; y++)
            {
                currentWorldPositionInGrid.x = MinWorldPositionInGrid.x;

                for (int x = 0; x < GridSize.x; x++)
                {
                    Vector3 center = new(currentWorldPositionInGrid.x + CellSize * 0.5f, 0, currentWorldPositionInGrid.y + CellSize * 0.5f);

                    if (!NavMesh.SamplePosition(center, out NavMeshHit _, CellSize, NavMesh.AllAreas))
                    {
                        int index = x + y * GridSize.x;

                        s_navMeshMask[index] = 1;
                    }

                    currentWorldPositionInGrid.x += CellSize;
                }

                currentWorldPositionInGrid.y += CellSize;
            }
        }

        public static void BuildFlowFieldFlowBlobAsset(in NavigationGrid navigationGrid)
        {
            NativeList<BoundForQuadtree> results = new(Allocator.TempJob);

            new QuadtreeJob
                {
                    NavMeshMaskInJob = s_navMeshMask.AsReadOnly(),
                    NavMeshGridSize = GridSize,
                    NavMeshCellSize = CellSize,
                    NavMeshMinWorldPosition = MinWorldPositionInGrid,

                    MinSize = navigationGrid.FlowFieldMinGridCellCount,
                    Tolerance = navigationGrid.FlowFieldWalkableToleranceForDivide,

                    Bounds = results
                }
                .Run();

            if (results.Length == 0)
            {
                results.Dispose();

                return;
            }

            new MergeBoundsJob
                {
                    Bounds = results,

                    ValidWalkableRatio = navigationGrid.FlowFieldValidWalkableRatio,
                }
                .Run();

            new ExpandBoundsJob
                {
                    Bounds = results.AsArray(),

                    NavMeshGridSize = GridSize,
                    NavMeshCellSize = CellSize,
                    NavMeshMinWorldPosition = MinWorldPositionInGrid,
                }
                .Schedule(results.Length, 64)
                .Complete();

            NativeArray<FlowFieldTargetForBuffer> flowFieldTargetBuffer = new(results.Length, Allocator.TempJob);

            for (int i = 0; i < results.Length; i++)
            {
                SimpleBounds bounds = results[i].Bounds;

                flowFieldTargetBuffer[i] = new FlowFieldTargetForBuffer
                {
                    FlowId = i,
                    AreaBounds = bounds,
                    DirectionField = new NativeArray<float2>(GridSize.x * GridSize.y, Allocator.TempJob)
                };
            }

            results.Dispose();

            BuildDirectionField(flowFieldTargetBuffer);

            for (int i = 0; i < flowFieldTargetBuffer.Length; i++)
            {
                flowFieldTargetBuffer[i].DirectionField.Dispose();
            }

            flowFieldTargetBuffer.Dispose();
        }

        private static void BuildDirectionField(in NativeArray<FlowFieldTargetForBuffer> flowFieldTargetBuffer)
        {
            JobHandle rootDependency = default;
            JobHandle dependency = rootDependency;

            for (int i = 0, count = flowFieldTargetBuffer.Length; i < count; i++)
            {
                NativeArray<float> costField = new(GridSize.x * GridSize.y, Allocator.TempJob);

                JobHandle buildCostFieldJobHandle =
                    new BuildCostFieldJob
                        {
                            CostField = costField,

                            FlowFieldTargetPosition = flowFieldTargetBuffer[i].Position,
                            NavMeshMaskInJob = s_navMeshMask.AsReadOnly(),
                            NavMeshGridSize = GridSize,
                            NavMeshCellSize = CellSize,
                            NavMeshMinWorldPosition = MinWorldPositionInGrid
                        }
                        .Schedule(rootDependency);

                JobHandle buildDirectionFieldJobHandle =
                    new BuildDirectionFieldJob
                        {
                            DirectionField = flowFieldTargetBuffer[i].DirectionField,

                            CostField = costField.AsReadOnly(),
                            FlowFieldTargetPosition = flowFieldTargetBuffer[i].Position,
                            NavMeshMaskInJob = s_navMeshMask.AsReadOnly(),
                            NavMeshGridSize = GridSize,
                            NavMeshCellSize = CellSize,
                            NavMeshMinWorldPosition = MinWorldPositionInGrid
                        }
                        .Schedule(costField.Length, 64, buildCostFieldJobHandle);

                JobHandle buildJobHandle = costField.Dispose(buildDirectionFieldJobHandle);

                dependency = JobHandle.CombineDependencies(dependency, buildJobHandle);
            }

            dependency.Complete();

            FlowFieldFlowBlobAssetReference = Build(flowFieldTargetBuffer);
        }

        private static BlobAssetReference<FlowFieldBlobRoot> Build(NativeArray<FlowFieldTargetForBuffer> sourceTargets)
        {
            BlobBuilder builder = new(Allocator.Temp);
            ref FlowFieldBlobRoot root = ref builder.ConstructRoot<FlowFieldBlobRoot>();

            // FlowFieldTarget 배열 크기만큼 BlobArray 할당
            BlobBuilderArray<FlowFieldTarget> targets = builder.Allocate(ref root.FlowFieldTargets, sourceTargets.Length);

            for (int i = 0; i < sourceTargets.Length; i++)
            {
                FlowFieldTargetForBuffer src = sourceTargets[i];
                ref FlowFieldTarget dst = ref targets[i];

                dst.FlowId = src.FlowId;
                dst.AreaBounds = src.AreaBounds;

                // DirectionField 복사
                BlobBuilderArray<float2> dirArray = builder.Allocate(ref dst.DirectionField, src.DirectionField.Length);
                for (int j = 0; j < src.DirectionField.Length; j++)
                {
                    dirArray[j] = src.DirectionField[j];
                }
            }

            BlobAssetReference<FlowFieldBlobRoot> blobAsset = builder.CreateBlobAssetReference<FlowFieldBlobRoot>(Allocator.Persistent);

            builder.Dispose();

            return blobAsset;
        }
    }
}