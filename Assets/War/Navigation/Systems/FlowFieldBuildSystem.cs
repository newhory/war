using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;


namespace War.Navigation.Systems
{
    [UpdateInGroup(typeof(Group.NavigationSystemGroup))]
    [UpdateAfter(typeof(StandingToggleSystem))]
    [RequireMatchingQueriesForUpdate]
    [BurstCompile]
    public partial struct FlowFieldBuildSystem : ISystem, ISystemStartStop
    {
        [BurstCompile]
        private partial struct InjectObstacleJob : IJobEntity
        {
            [ReadOnly] public float3 MinWorldPositionInGrid;
            [ReadOnly] public int2 GridSize;
            [ReadOnly] public float CellSize;

            public NativeParallelHashSet<int>.ParallelWriter ObstacleIndicesWriter;


            private void Execute(in UnitPosition unitPosition, in UnitRadius unitRadius, in StandingObstacle standingObstacle)
            {
                if (standingObstacle.Enabled == 0)
                {
                    return;
                }

                int index = FlowFieldQuery.WorldToIndex(unitPosition.Value, GridSize, CellSize, MinWorldPositionInGrid);
                if (index < 0)
                {
                    return;
                }

                ObstacleIndicesWriter.Add(index);
            }
        }

        [BurstCompile]
        private struct InjectNavMeshMask : IJobParallelFor
        {
            [ReadOnly] public NativeArray<byte>.ReadOnly NavMeshMask;

            public NativeParallelHashSet<int>.ParallelWriter ObstacleIndicesWriter;


            public void Execute(int index)
            {
                if (NavMeshMask[index] == 0)
                {
                    return;
                }

                ObstacleIndicesWriter.Add(index);
            }
        }

        [BurstCompile]
        private struct BuildObstacleMaskJob : IJobParallelFor
        {
            [ReadOnly] public NativeParallelHashSet<int>.ReadOnly ObstacleIndices;

            public NativeArray<byte> ObstacleMask;


            public void Execute(int index)
            {
                if (ObstacleIndices.Contains(index))
                {
                    ObstacleMask[index] = 1;
                }
            }
        }


        private EntityQuery _targets;

        private uint _frameCount;
        private uint _throttleFrames; // 갱신 주기


        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NavigationGrid>();

            _targets =
                SystemAPI.QueryBuilder()
                    .WithAll<FlowFieldTarget>()
                    .Build();

            _throttleFrames = 10; // 10프레임마다 재계산(대규모 변화 시에만 더 자주)
        }

        public void OnDestroy(ref SystemState state) => FlowFieldProvider.Dispose();

        public void OnUpdate(ref SystemState state)
        {
            // 스로틀: 지정한 프레임 간격으로만 업데이트
            if (_frameCount++ % _throttleFrames != 0)
            {
                return;
            }

            if (_targets.IsEmpty)
            {
                return;
            }

            NavigationGrid navigationGrid = SystemAPI.GetSingleton<NavigationGrid>();

            int2 gridSize = navigationGrid.FlowFieldGridSize;
            float cellSize = navigationGrid.FlowFieldCellSize;
            float3 minWorldPositionInGrid = navigationGrid.Min;

            FlowFieldProvider.Init(cellSize, gridSize, minWorldPositionInGrid, Allocator.Persistent);

            JobHandle dependency = state.Dependency;

            NativeParallelHashSet<int> maskBuffer = new(gridSize.x * gridSize.y, Allocator.TempJob);

            dependency =
                new InjectObstacleJob
                    {
                        MinWorldPositionInGrid = minWorldPositionInGrid,
                        GridSize = gridSize,
                        CellSize = cellSize,

                        ObstacleIndicesWriter = maskBuffer.AsParallelWriter()
                    }
                    .ScheduleParallel(dependency);

            dependency =
                new InjectNavMeshMask
                    {
                        ObstacleIndicesWriter = maskBuffer.AsParallelWriter(),

                        NavMeshMask = FlowFieldProvider.NavMeshMask
                    }
                    .Schedule(FlowFieldProvider.NavMeshMask.Length, 64, dependency);

            // 정지 장애물 마스크 생성(셀당 차단 여부)
            NativeArray<byte> obstacleMask = new(gridSize.x * gridSize.y, Allocator.TempJob);

            dependency =
                new BuildObstacleMaskJob
                    {
                        ObstacleIndices = maskBuffer.AsReadOnly(),

                        ObstacleMask = obstacleMask
                    }
                    .Schedule(obstacleMask.Length, 64, dependency);

            dependency.Complete();

            // 목적지 수집
            NativeArray<FlowFieldTarget> targets = _targets.ToComponentDataArray<FlowFieldTarget>(Allocator.Temp);

            FlowFieldProvider.BuildDirectionField(targets.AsReadOnly(), obstacleMask.AsReadOnly());

            targets.Dispose();
            obstacleMask.Dispose();
            maskBuffer.Dispose();
        }

        public void OnStartRunning(ref SystemState state) => _frameCount = 0;

        public void OnStopRunning(ref SystemState state)
        {
        }
    }
}