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
                ref UnitPosition unitPosition, ref UnitVelocity unitVelocity, ref UnitPreferredSide unitPreferredSide, ref BlockAhead blockAhead,
                in UnitDestination unitDestination, in UnitRadius unitRadius, in UnitMaxSpeed unitMaxSpeed, in StandingObstacle standingObstacle,
                in FlowFieldBlobReference flowFieldBlobReference)
            {
                if (standingObstacle.Enabled == 1)
                {
                    return;
                }

                float3 position = unitPosition.Value;
                float3 toDestination = unitDestination.Value - position;
                float3 toDestDirection = math.normalizesafe(new float3(toDestination.x, 0, toDestination.z));
                float3 oldVelocityDirection = math.normalizesafe(new float3(unitVelocity.Value.x, 0, unitVelocity.Value.z));

                float2 flowDirection;

                if (CanMoveDirect(position, unitDestination.Value, unitRadius.Value))
                {
                    flowDirection = toDestDirection.xz;
                }
                else
                {
                    int positionFlowId = -1;
                    for (int i = 0, count = flowFieldBlobReference.BlobAssetReference.Value.FlowFieldTargets.Length; i < count; ++i)
                    {
                        if (flowFieldBlobReference.BlobAssetReference.Value.FlowFieldTargets[i].AreaBounds.Contains(position.xz))
                        {
                            positionFlowId = flowFieldBlobReference.BlobAssetReference.Value.FlowFieldTargets[i].FlowId;

                            break;
                        }
                    }

                    // 1) Robust Flow 샘플링
                    if (positionFlowId < 0 || positionFlowId == unitDestination.FlowFieldId ||
                        !TrySampleDirection(flowFieldBlobReference, unitDestination.FlowFieldId, position, unitRadius.Value, oldVelocityDirection.xz, out flowDirection))
                    {
                        flowDirection = toDestDirection.xz;
                    }
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
                    float3 blended = BlendFlowWithDetour(flowDirection, detourDirection, severity, oldVelocityDirection);
                    velocity = math.lerp(velocity, blended * unitMaxSpeed.Value, 0.7f);
                }
                else
                {
                    // detour가 약할 때는 flow에 더 붙여서 튐 방지
                    float3 flow3 = new float3(flowDirection.x, 0, flowDirection.y) * unitMaxSpeed.Value;
                    velocity = math.lerp(velocity, flow3, 0.3f);
                }

                // 5) 진행 방향에 장애물 처리
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

                // 7) 속도/위치 갱신
                float3 newVelocity = math.lerp(unitVelocity.Value, velocity, math.saturate(DeltaTime / 0.15f));

                unitVelocity.Value = newVelocity;
                unitPosition.Value += newVelocity * DeltaTime;
            }

            private int2 WorldToCell(float3 worldPos) => FlowFieldQuery.WorldToCell(worldPos, FlowFieldGridSize, FlowFieldCellSize, MinWorldPositionInGrid);

            private bool CanMoveDirect(float3 startPos, float3 endPos, float unitRadius)
            {
                int2 startCell = WorldToCell(startPos);
                int2 endCell = WorldToCell(endPos);

                // Bresenham 직선 알고리즘
                int dx = math.abs(endCell.x - startCell.x);
                int dy = math.abs(endCell.y - startCell.y);
                int sx = startCell.x < endCell.x ? 1 : -1;
                int sy = startCell.y < endCell.y ? 1 : -1;
                int err = dx - dy;

                int2 cell = startCell;
                int radiusCells = math.max(1, (int)math.ceil(unitRadius / FlowFieldCellSize));

                while (true)
                {
                    // 반경 내 셀 검사
                    for (int ry = -radiusCells; ry <= radiusCells; ry++)
                    {
                        for (int rx = -radiusCells; rx <= radiusCells; rx++)
                        {
                            int nx = cell.x + rx;
                            int ny = cell.y + ry;
                            if (nx < 0 || ny < 0 || nx >= FlowFieldGridSize.x || ny >= FlowFieldGridSize.y)
                            {
                                return false;
                            }

                            int nIdx = ny * FlowFieldGridSize.x + nx;
                            if (NavMeshMask[nIdx] != 0) // 장애물 있음
                            {
                                return false;
                            }
                        }
                    }

                    if (cell.x == endCell.x && cell.y == endCell.y)
                    {
                        break;
                    }

                    int e2 = 2 * err;
                    if (e2 > -dy)
                    {
                        err -= dy;
                        cell.x += sx;
                    }

                    if (e2 < dx)
                    {
                        err += dx;
                        cell.y += sy;
                    }
                }

                return true; // 직선 경로가 모두 유효
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

                for (int i = 0, count = flowFieldBlobReference.BlobAssetReference.Value.FlowFieldTargets.Length; i < count; ++i)
                {
                    ref FlowFieldTarget flowFieldTarget = ref flowFieldBlobReference.BlobAssetReference.Value.FlowFieldTargets[i];
                    if (flowFieldTarget.FlowId != flowId)
                    {
                        continue;
                    }

                    flowFieldTargetIndex = i;

                    float2 uv = (worldPos.xz - MinWorldPositionInGrid.xz) / FlowFieldCellSize;
                    int ix = (int)math.floor(uv.x);
                    int iy = (int)math.floor(uv.y);
                    float fx = uv.x - ix;
                    float fy = uv.y - iy;

                    // 영역 밖이면 경계 스티키니스: 그리드로 clamp
                    ix = math.clamp(ix, 0, FlowFieldGridSize.x - 2);
                    iy = math.clamp(iy, 0, FlowFieldGridSize.y - 2);

                    float2 d00 = GetDirectionSafe(ref flowFieldTarget.DirectionField, ix, iy);
                    float2 d10 = GetDirectionSafe(ref flowFieldTarget.DirectionField, ix + 1, iy);
                    float2 d01 = GetDirectionSafe(ref flowFieldTarget.DirectionField, ix, iy + 1);
                    float2 d11 = GetDirectionSafe(ref flowFieldTarget.DirectionField, ix + 1, iy + 1);

                    // 유효 셀만 가중 평균 (거리 기반)
                    float w00 = math.lengthsq(d00) > 0f ? 1f : 0f; // 현재 셀은 항상 강하게 반영
                    float w10 = !IsWallInward(d10, d00) ? fx * (1f - fy) : 0f;
                    float w01 = !IsWallInward(d01, d00) ? (1f - fx) * fy : 0f;
                    float w11 = !IsWallInward(d11, d00) ? fx * fy : 0f;

                    float wSum = w00 + w10 + w01 + w11;

                    if (wSum > 0f)
                    {
                        baseFlow = math.normalizesafe((d00 * w00 + d10 * w10 + d01 * w01 + d11 * w11) / wSum);
                        baseOk = math.lengthsq(baseFlow) > 0.0001f;
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
                ref FlowFieldTarget currentFlowFieldTarget = ref flowFieldBlobReference.BlobAssetReference.Value.FlowFieldTargets[flowFieldTargetIndex];

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

                            int nextX = currentCell.x + dx;
                            int nextY = currentCell.y + dy;
                            if (nextX < 0 || nextY < 0 || nextX >= FlowFieldGridSize.x || nextY >= FlowFieldGridSize.y)
                            {
                                continue;
                            }

                            int nextIndex = nextY * FlowFieldGridSize.x + nextX;
                            if (NavMeshMask[nextIndex] != 0)
                            {
                                continue;
                            }

                            float2 nextDirection = currentFlowFieldTarget.DirectionField[nextIndex];
                            if (math.lengthsq(nextDirection) < 0.0001f)
                            {
                                continue;
                            }

                            float dist = math.length(new float2(dx, dy));
                            if (dist < bestDistance)
                            {
                                bestDistance = dist;
                                bestDirection = nextDirection;
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

            private float2 GetDirectionSafe(ref BlobArray<float2> field, int x, int y)
            {
                int index = FlowFieldQuery.CellToIndex(new int2(x, y), FlowFieldGridSize);

                return NavMeshMask[index] != 0 ? float2.zero : field[index];
            }

            private static float3 BlendFlowWithDetour(float2 flowDirection, float3 detourDirection, float severity, float3 oldVelocityDirection)
            {
                float3 flowDir3 = new(flowDirection.x, 0, flowDirection.y);
                float angle = math.degrees(math.acos(math.saturate(math.dot(math.normalizesafe(flowDir3), math.normalizesafe(detourDirection)))));
                float angleT = math.saturate(angle / 90f);

                float detourWeight = math.saturate(0.3f + 0.7f * severity);
                float followWeight = 1f - detourWeight;
                followWeight = math.lerp(followWeight, followWeight * 0.4f, angleT);

                float3 blended = math.normalizesafe(flowDir3 * followWeight + detourDirection * detourWeight);

                float align = math.saturate(math.dot(blended, math.normalizesafe(oldVelocityDirection)));
                blended = math.normalizesafe(math.lerp(blended, oldVelocityDirection, math.saturate(0.2f * (1f - align))));

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