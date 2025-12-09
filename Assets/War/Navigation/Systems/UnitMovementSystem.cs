using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
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

            [ReadOnly] public float3 MinWorldPositionInGrid;
            [ReadOnly] public int2 FlowFieldGridSize;
            [ReadOnly] public float FlowFieldCellSize;

            [ReadOnly] public NativeParallelMultiHashMap<int, NeighborRecord>.ReadOnly Hash;
            [ReadOnly] public float NeighborCellSize;
            [ReadOnly] public int MaxNeighborCount;

            [ReadOnly] public NativeArray<byte>.ReadOnly NavMeshMask;


            private void Execute(
                ref UnitPosition unitPosition, ref UnitVelocity unitVelocity,
                in UnitDestination unitDestination, in UnitRadius unitRadius, in UnitMaxSpeed unitMaxSpeed, in StandingObstacle standingObstacle,
                ref UnitPreferredSide unitPreferredSide, ref BlockAhead blockAhead,
                in FlowFieldBlobReference flowFieldBlobReference)
            {
                if (standingObstacle.Enabled == 1)
                {
                    return;
                }

                float3 position = unitPosition.Value;
                float3 toDestination = unitDestination.Value - position;
                float3 toDestDir = math.normalizesafe(new float3(toDestination.x, 0, toDestination.z));
                float3 oldDir = math.normalizesafe(new float3(unitVelocity.Value.x, 0, unitVelocity.Value.z));

                int positionFlowId = -1;
                for (int i = 0, count = flowFieldBlobReference.Blob.Value.Targets.Length; i < count; ++i)
                {
                    if (flowFieldBlobReference.Blob.Value.Targets[i].AreaBounds.Contains(position.xz))
                    {
                        positionFlowId = flowFieldBlobReference.Blob.Value.Targets[i].FlowId;

                        break;
                    }
                }

                // 1) Robust Flow 샘플링
                if (positionFlowId < 0 || positionFlowId == unitDestination.FlowFieldId ||
                    !TrySampleDirection(flowFieldBlobReference, unitDestination.FlowFieldId, position, unitRadius.Value, oldDir.xz, out float2 flowDirection))
                {
                    flowDirection = toDestDir.xz;
                }

                float3 desireVelocity = new float3(flowDirection.x, 0, flowDirection.y) * unitMaxSpeed.Value;
                float3 velocityDirection = math.normalizesafe(desireVelocity);

                // 2) 이웃 수집
                NativeArray<NeighborRecord> neighborRecordBuffer = new(MaxNeighborCount, Allocator.Temp);
                int neighborCount = NeighborQuery.Collect(position, velocityDirection, MaxNeighborCount, NeighborCellSize, Hash, ref neighborRecordBuffer);

                // 3) 회피 (거리 + 예측 기반)
                float3 velocity = Steering.SteerAvoid(
                    desireVelocity,
                    position,
                    neighborRecordBuffer.AsReadOnly(),
                    neighborCount,
                    unitRadius.Value,
                    PredictionTimeHorizon,
                    ref unitPreferredSide.Value);

                // Separation
                velocity += Steering.ComputeSeparation(
                    position,
                    neighborRecordBuffer.AsReadOnly(),
                    neighborCount,
                    unitRadius.Value,
                    separationWeight: 0.8f);

                neighborRecordBuffer.Dispose();

                velocityDirection = math.normalizesafe(velocity);

                // 4) 앞 차단 평가 (blockedCount 반영)
                float severity = Steering.EvaluateBlockAhead(position, velocityDirection, Hash, NeighborCellSize, unitRadius.Value, out bool isLeftBetter);
                blockAhead.Severity = severity;

                if (severity > 0.6f)
                {
                    // 좌/우 선택
                    unitPreferredSide.Value = isLeftBetter ? -1 : 1;
                    float3 detour = Steering.ComputeDetourTarget(position, velocityDirection, unitRadius.Value * 4f, isLeftBetter);
                    float3 detourDirection = math.normalizesafe(detour - position);

                    // Flow와 Detour를 안정적으로 블렌딩
                    float3 blended = BlendFlowWithDetour(flowDirection, detourDirection, severity, oldDir);
                    velocity = math.lerp(velocity, blended * unitMaxSpeed.Value, 0.7f);
                }
                else
                {
                    // detour가 약할 때는 flow에 더 붙여서 튐 방지
                    float3 flow3 = new float3(flowDirection.x, 0, flowDirection.y) * unitMaxSpeed.Value;
                    velocity = math.lerp(velocity, flow3, 0.3f);
                }

                velocity.xz = AdjustForObstacle(position, velocity.xz, unitRadius.Value, FlowFieldCellSize);

                // 6) 속도 클램프 + 감속 + 스무딩
                float dist = math.length(toDestination);
                float arrivalRadius = unitRadius.Value * 2f;
                float slowdownFactor = math.saturate(dist / arrivalRadius);
                float maxV = unitMaxSpeed.Value * slowdownFactor;
                float speed = math.length(velocity.xz);

                if (speed > maxV)
                {
                    velocity.xz *= maxV / speed;
                }

                float3 newVelocity = math.lerp(unitVelocity.Value, velocity, math.saturate(DeltaTime / 0.15f));

                // 7) 위치 적분
                unitPosition.Value += newVelocity * DeltaTime;
                unitVelocity.Value = newVelocity;
            }

            private bool TrySampleDirection(in FlowFieldBlobReference flowFieldBlobReference, int flowId, in float3 worldPos, float unitRadius, in float2 oldDir, out float2 flow)
            {
                flow = float2.zero;

                if (flowId < 0)
                {
                    return false;
                }

                // 대상 타깃 찾기 + bilinear 기본 샘플
                int flowFieldTargetIndex = -1;
                float2 baseFlow = float2.zero;
                bool baseOk = false;

                for (int i = 0, count = flowFieldBlobReference.Blob.Value.Targets.Length; i < count; ++i)
                {
                    ref FlowFieldTargetBlob flowFieldTarget = ref flowFieldBlobReference.Blob.Value.Targets[i];
                    if (flowFieldTarget.FlowId != flowId)
                    {
                        continue;
                    }

                    flowFieldTargetIndex = i;

                    float2 uv = (worldPos.xz - MinWorldPositionInGrid.xz) / FlowFieldCellSize;
                    int ix = (int)math.floor(uv.x);
                    int iy = (int)math.floor(uv.y);
                    float fx = math.saturate(uv.x - ix);
                    float fy = math.saturate(uv.y - iy);

                    // 영역 밖이면 경계 스티키니스: 그리드로 clamp
                    ix = math.clamp(ix, 0, FlowFieldGridSize.x - 2);
                    iy = math.clamp(iy, 0, FlowFieldGridSize.y - 2);

                    float2 d00 = SafeGetDir(ref flowFieldTarget.DirectionField, ix, iy);
                    float2 d10 = SafeGetDir(ref flowFieldTarget.DirectionField, ix + 1, iy);
                    float2 d01 = SafeGetDir(ref flowFieldTarget.DirectionField, ix, iy + 1);
                    float2 d11 = SafeGetDir(ref flowFieldTarget.DirectionField, ix + 1, iy + 1);

                    // 유효 셀만 가중 평균 (거리 기반)
                    float w00 = (math.lengthsq(d00) > 0f ? 1f : 0f); // 현재 셀은 항상 강하게 반영
                    float w10 = (!IsWallInward(d10, d00) ? (fx) * (1f - fy) : 0f);
                    float w01 = (!IsWallInward(d01, d00) ? (1f - fx) * (fy) : 0f);
                    float w11 = (!IsWallInward(d11, d00) ? (fx * fy) : 0f);

                    float wSum = w00 + w10 + w01 + w11;

                    if (wSum > 0f)
                    {
                        baseFlow = math.normalizesafe((d00 * w00 + d10 * w10 + d01 * w01 + d11 * w11) / wSum);
                        baseOk = (math.lengthsq(baseFlow) > 0.0001f);
                    }

                    break;
                }

                if (flowFieldTargetIndex < 0)
                {
                    return false;
                }

                if (baseOk)
                {
                    flow = baseFlow;
                    return true;
                }

                // 현재 셀이 0벡터면: 반경 내 가장 가까운 유효 셀에서 방향 샘플링 (Nearest Valid Sampling)
                ref FlowFieldTargetBlob t = ref flowFieldBlobReference.Blob.Value.Targets[flowFieldTargetIndex];

                int2 currentCell = FlowFieldQuery.WorldToCell(worldPos, FlowFieldGridSize, FlowFieldCellSize, MinWorldPositionInGrid);

                // 반경은 유닛 반지름과 셀 크기 기반(최소 1 ~ 최대 3)
                int maxRadius = math.clamp((int)math.ceil(unitRadius / FlowFieldCellSize) + 1, 1, 3);

                float2 bestDirection = float2.zero;
                float bestDistance = float.PositiveInfinity;

                for (int r = 1; r <= maxRadius; r++)
                {
                    for (int dy = -r; dy <= r; dy++)
                    {
                        for (int dx = -r; dx <= r; dx++)
                        {
                            if (dy != -r && dy != r && dx != -r && dx != r)
                            {
                                continue;
                            }

                            int nx = currentCell.x + dx;
                            int ny = currentCell.y + dy;
                            if (nx < 0 || ny < 0 || nx >= FlowFieldGridSize.x || ny >= FlowFieldGridSize.y)
                            {
                                continue;
                            }

                            int nIdx = ny * FlowFieldGridSize.x + nx;
                            if (NavMeshMask[nIdx] != 0)
                            {
                                continue;
                            }

                            float2 ndir = t.DirectionField[nIdx];
                            if (math.lengthsq(ndir) < 0.0001f)
                            {
                                continue;
                            }

                            float dist = math.length(new float2(dx, dy));
                            if (dist < bestDistance)
                            {
                                bestDistance = dist;
                                bestDirection = ndir;
                            }
                        }
                    }

                    if (bestDistance < float.PositiveInfinity)
                    {
                        break;
                    }
                }

                if (bestDistance < float.PositiveInfinity)
                {
                    flow = math.normalizesafe(bestDirection);
                    return true;
                }

                // 최후: 이전 방향 유지 (목표 직진 금지)
                flow = oldDir;
                return math.lengthsq(flow) > 0.0001f;
            }

            // 벽 안쪽 성분 필터링 함수
            // posDir은 현재 셀 방향
            // 벽 평행 상태라면, posDir과 반대 성분은 제외
            private static bool IsWallInward(float2 dir, float2 posDir) => math.dot(dir, posDir) < 0f;

            private float2 SafeGetDir(ref BlobArray<float2> field, int x, int y)
            {
                int index = FlowFieldQuery.CellToIndex(new int2(x, y), FlowFieldGridSize);
                return NavMeshMask[index] != 0 ? float2.zero : field[index];
            }

            private static float3 BlendFlowWithDetour(float2 flowDir, float3 detourDir3, float severity, float3 oldDir3)
            {
                float3 flowDir3 = new(flowDir.x, 0, flowDir.y);
                float angle = math.degrees(math.acos(math.saturate(math.dot(math.normalizesafe(flowDir3), math.normalizesafe(detourDir3)))));
                float angleT = math.saturate(angle / 90f);

                float detourWeight = math.saturate(0.3f + 0.7f * severity);
                float followWeight = 1f - detourWeight;
                followWeight = math.lerp(followWeight, followWeight * 0.4f, angleT);

                float3 blended = math.normalizesafe(flowDir3 * followWeight + detourDir3 * detourWeight);

                float align = math.saturate(math.dot(blended, math.normalizesafe(oldDir3)));
                blended = math.normalizesafe(math.lerp(blended, oldDir3, math.saturate(0.2f * (1f - align))));

                return blended;
            }

            private float2 AdjustForObstacle(float3 worldPos, float2 dir, float unitRadius, float cellSize)
            {
                if (math.lengthsq(dir) < 0.0001f)
                {
                    return dir;
                }

                float3 step = new float3(dir.x, 0, dir.y) * math.max(cellSize, unitRadius);
                int2 nextCell = FlowFieldQuery.WorldToCell(worldPos + step, FlowFieldGridSize, FlowFieldCellSize, MinWorldPositionInGrid);
                if (nextCell.x >= 0 && nextCell.x < FlowFieldGridSize.x &&
                    nextCell.y >= 0 && nextCell.y < FlowFieldGridSize.y)
                {
                    int nextIndex = FlowFieldQuery.CellToIndex(nextCell, FlowFieldGridSize);

                    // 장애물 앞 → 목표 방향의 평행 성분만 남김
                    if (NavMeshMask[nextIndex] != 0)
                    {
                        return
                            math.abs(dir.x) > math.abs(dir.y)
                                ? new float2(dir.x, 0)
                                : new float2(0, dir.y);
                    }
                }

                return dir;
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
            JobHandle dependency = state.Dependency;

            dependency =
                new MoveJob
                    {
                        DeltaTime = SystemAPI.Time.DeltaTime,
                        PredictionTimeHorizon = 1.2f,

                        MinWorldPositionInGrid = FlowFieldProvider.MinWorldPositionInGrid,
                        FlowFieldGridSize = FlowFieldProvider.GridSize,
                        FlowFieldCellSize = FlowFieldProvider.CellSize,

                        Hash = NeighborService.Hash,
                        NeighborCellSize = NeighborService.CellSize,
                        MaxNeighborCount = NeighborService.MaxNeighborCount,

                        NavMeshMask = FlowFieldProvider.NavMeshMask
                    }
                    .ScheduleParallel(dependency);

            state.Dependency = dependency;
        }
    }
}