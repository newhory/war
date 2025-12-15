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

                if (CanMoveDirect(position, unitDestination.Value))
                {
                    flowDirection = toDestDirection.xz;
                }
                else
                {
                    // 1) Robust Flow 샘플링
                    if (!TrySampleDirection(flowFieldBlobReference, unitDestination.FlowFieldId, position, unitRadius.Value, oldVelocityDirection.xz, out flowDirection))
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
                velocity.xz = AdjustForObstacle(position, velocity.xz, desireVelocity.xz);

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

            private bool CanMoveDirect(float3 startPos, float3 endPos)
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

                while (true)
                {
                    if (cell.x == endCell.x && cell.y == endCell.y)
                    {
                        break;
                    }

                    if (cell.x < 0 || cell.y < 0 || cell.x >= FlowFieldGridSize.x || cell.y >= FlowFieldGridSize.y)
                    {
                        return false;
                    }

                    int index = FlowFieldQuery.CellToIndex(cell, FlowFieldGridSize);
                    if (NavMeshMask[index] != 0) // 장애물 있음
                    {
                        return false;
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
                    float w00 = math.lengthsq(d00) > 0f ? (1f - fx) * (1f - fy) : 0f;
                    float w10 = math.lengthsq(d10) > 0f ? fx * (1f - fy) : 0f;
                    float w01 = math.lengthsq(d01) > 0f ? (1f - fx) * fy : 0f;
                    float w11 = math.lengthsq(d11) > 0f ? fx * fy : 0f;

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

            private float2 GetDirectionSafe(ref BlobArray<float2> field, int x, int y)
            {
                int index = FlowFieldQuery.CellToIndex(new int2(x, y), FlowFieldGridSize);

                return NavMeshMask[index] == 0 ? field[index] : float2.zero;
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

            private float2 AdjustForObstacle(float3 worldPos, float2 velocity, float2 desireVelocity)
            {
                if (math.lengthsq(velocity) < 0.0001f || IsDirectionFree(worldPos, velocity))
                {
                    return velocity;
                }

                float2 candidateX = new(velocity.x, 0);
                float2 candidateY = new(0, velocity.y);

                // 후보 방향을 worldPos 기준으로 검사
                bool xFree = IsDirectionFree(worldPos, candidateX);
                bool yFree = IsDirectionFree(worldPos, candidateY);

                if (xFree && yFree)
                {
                    // 둘 다 가능 → 원래 dir과 더 가까운 쪽 선택
                    return (math.abs(velocity.x) > math.abs(velocity.y) ? candidateX : candidateY);
                }

                if (xFree)
                {
                    return candidateX;
                }

                if (yFree)
                {
                    return candidateY;
                }

                // 둘 다 막힘 → FlowField 방향으로 fallback
                return desireVelocity;
            }

            // 보조 함수: 특정 방향으로 unitRadius만큼 이동했을 때 뚫려 있는지 검사
            private bool IsDirectionFree(float3 worldPos, float2 velocity)
            {
                if (math.lengthsq(velocity) < 0.0001f)
                {
                    return false;
                }

                float3 direction = math.normalize(new float3(velocity.x, 0, velocity.y));
                float3 step = direction * FlowFieldCellSize;
                int2 nextCell = FlowFieldQuery.WorldToCell(worldPos + step, FlowFieldGridSize, FlowFieldCellSize, MinWorldPositionInGrid);
                if (nextCell.x < 0 || nextCell.x >= FlowFieldGridSize.x || nextCell.y < 0 || nextCell.y >= FlowFieldGridSize.y)
                {
                    return false;
                }

                int nextIndex = FlowFieldQuery.CellToIndex(nextCell, FlowFieldGridSize);

                return NavMeshMask[nextIndex] == 0;
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
                        PredictionTimeHorizon = 0.8f,

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