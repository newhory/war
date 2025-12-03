using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;


namespace War.Navigation.Systems
{
    [UpdateInGroup(typeof(Group.NavigationSystemGroup))]
    [UpdateAfter(typeof(FlowFieldBuildSystem))]
    [BurstCompile]
    public partial struct UnitMovementSystem : ISystem
    {
        [BurstCompile]
        private partial struct MoveJob : IJobEntity
        {
            [ReadOnly] public float DeltaTime;

            /// <summary>
            /// 예측 시간 상수(Time horizon), 앞으로 τ(tau)초 동안 충돌을 예측.
            /// </summary>
            [ReadOnly] public float PredictionTimeHorizon;

            [ReadOnly] public NativeArray<float2>.ReadOnly DirectionField;
            [ReadOnly] public float3 MinWorldPositionInGrid;
            [ReadOnly] public int2 FlowFieldGridSize;
            [ReadOnly] public float FlowFieldCellSize;

            [ReadOnly] public NativeParallelMultiHashMap<int, NeighborRecord>.ReadOnly Hash;
            [ReadOnly] public float NeighborCellSize;
            [ReadOnly] public int MaxNeighborCount;


            private void Execute(
                ref UnitPosition unitPosition, ref UnitVelocity unitVelocity,
                in UnitDestination unitDestination, in UnitRadius unitRadius, in UnitMaxSpeed unitMaxSpeed, in StandingObstacle standingObstacle,
                ref UnitPreferredSide unitPreferredSide, ref BlockAhead blockAhead)
            {
                if (standingObstacle.Enabled == 1)
                {
                    return;
                }

                float3 position = unitPosition.Value;
                float3 toDestination = unitDestination.Value - position;
                float3 toDestDir = math.normalizesafe(new float3(toDestination.x, 0, toDestination.z));

                // 1) 플로우 필드 방향
                float3 desireVelocity =
                    FlowFieldQuery.TrySampleDir(unitDestination.FlowFieldId, DirectionField, position, FlowFieldCellSize, FlowFieldGridSize, MinWorldPositionInGrid, out float2 flow)
                        ? new float3(flow.x, 0, flow.y) * unitMaxSpeed.Value
                        : toDestDir * unitMaxSpeed.Value;

                // 2) 이웃 수집
                NativeArray<NeighborRecord> neighborRecordBuffer = new(MaxNeighborCount, Allocator.Temp);
                int neighborCount = NeighborQuery.Collect(position, toDestDir, MaxNeighborCount, NeighborCellSize, Hash, ref neighborRecordBuffer);

                // 3) 회피 (거리 + 예측 기반)
                float3 v = Steering.SteerAvoid(desireVelocity, position, neighborRecordBuffer.AsReadOnly(), neighborCount, unitRadius.Value, PredictionTimeHorizon, ref unitPreferredSide.Value);

                float3 vSep = Steering.ComputeSeparation(
                    position,
                    neighborRecordBuffer.AsReadOnly(),
                    neighborCount,
                    unitRadius.Value,
                    separationWeight: 0.8f // Separation 강도
                );

                v += vSep;

                // 4) 앞 차단 평가 (blockedCount 반영)
                float severity = Steering.EvaluateBlockAhead(position, toDestDir, Hash, NeighborCellSize, unitRadius.Value, out bool isLeftBetter);
                blockAhead.Severity = severity;

                if (severity > 0.6f)
                {
                    // 좌/우 선택
                    unitPreferredSide.Value = isLeftBetter ? -1 : 1;
                    float3 detour = Steering.ComputeDetourTarget(position, toDestDir, unitRadius.Value * 4f, isLeftBetter);
                    float3 detourDir = math.normalizesafe(detour - position);
                    v = math.lerp(v, detourDir * unitMaxSpeed.Value, 0.7f);
                }

                // 5) 속도 클램프 + 스무딩
                float speed = math.length(v);
                float maxV = unitMaxSpeed.Value;
                if (speed > maxV)
                {
                    v *= (maxV / speed);
                }

                float3 newVel = math.lerp(unitVelocity.Value, v, math.saturate(DeltaTime / 0.15f));

                // 6) 위치 적분
                unitPosition.Value += newVel * DeltaTime;
                unitVelocity.Value = newVel;

                neighborRecordBuffer.Dispose();
            }
        }


        public void OnCreate(ref SystemState state)
        {
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            state.Dependency =
                new MoveJob
                    {
                        DeltaTime = SystemAPI.Time.DeltaTime,
                        PredictionTimeHorizon = 1.2f,

                        DirectionField = FlowFieldProvider.DirectionField,
                        MinWorldPositionInGrid = FlowFieldProvider.MinWorldPositionInGrid,
                        FlowFieldGridSize = FlowFieldProvider.GridSize,
                        FlowFieldCellSize = FlowFieldProvider.CellSize,

                        Hash = NeighborService.Hash,
                        NeighborCellSize = NeighborService.CellSize,
                        MaxNeighborCount = NeighborService.MaxNeighborCount,
                    }
                    .ScheduleParallel(state.Dependency);
        }
    }
}