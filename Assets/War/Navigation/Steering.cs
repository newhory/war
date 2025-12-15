using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;


namespace War.Navigation
{
    [BurstCompile]
    public static class Steering
    {
        public static float3 SteerAvoid(in float3 desireVelocity, in float3 position, in NativeArray<NeighborRecord>.ReadOnly neighborRecordBuffer, int neighborCount, float selfRadius, float tau, ref int sidePref) // UnitPreferredSide.Value 참조
        {
            float3 velocity = desireVelocity;
            float3 desireDirection = math.normalizesafe(desireVelocity);
            float leftAccum = 0f, rightAccum = 0f;

            for (int i = 0; i < neighborCount; i++)
            {
                NeighborRecord neighborRecord = neighborRecordBuffer[i];
                float3 d = neighborRecord.Position - position;
                float dist = math.length(d);
                float sumRadius = selfRadius + neighborRecord.Radius;

                // --- 1) 거리 기반 회피 ---
                if (dist < sumRadius * 1.2f)
                {
                    float3 dir = math.normalizesafe(new float3(d.x, 0, d.z));
                    float3 side = new(-dir.z, 0, dir.x); // 좌측 방향
                    float push = math.saturate((sumRadius * 1.2f - dist) / (sumRadius * 1.2f));
                    velocity += side * push * 0.6f;

                    // 좌/우 누적
                    float dot = math.dot(side, desireDirection);
                    if (dot > 0)
                    {
                        leftAccum += push;
                    }
                    else
                    {
                        rightAccum += push;
                    }
                }

                // --- 2) 속도 기반 예측 회피 ---
                float3 relativeVelocity = velocity - neighborRecord.Velocity;
                float closing = math.dot(relativeVelocity, math.normalizesafe(d));
                if (closing > 0f)
                {
                    float timeToClose = dist / (closing + 1e-3f);
                    if (timeToClose < tau)
                    {
                        // τ초 안에 충돌 예상 → 강한 측면 회피
                        float weight = math.saturate((tau - timeToClose) / tau);
                        float3 side = new float3(-d.z, 0, d.x) * (0.5f * weight);
                        velocity += side;

                        // 좌/우 누적 반영
                        float dot = math.dot(side, desireDirection);
                        if (dot > 0)
                        {
                            leftAccum += weight;
                        }
                        else
                        {
                            rightAccum += weight;
                        }
                    }
                }
            }

            // --- 3) 좌/우 선호 방향 갱신 ---
            if (leftAccum > rightAccum * 1.1f)
            {
                sidePref = -1; // 좌측 선호
            }
            else if (rightAccum > leftAccum * 1.1f)
            {
                sidePref = 1; // 우측 선호
            }
            else
            {
                sidePref = 0;
            }

            // --- 4) 선호 방향을 약간 반영해 흔들림 완화 ---
            if (sidePref != 0)
            {
                float3 sideDir = sidePref < 0
                    ? new float3(-velocity.z, 0, velocity.x) // 좌측
                    : new float3(velocity.z, 0, -velocity.x); // 우측

                velocity += sideDir * 0.1f;
            }

            return velocity;
        }

        public static float3 ComputeSeparation(
            in float3 myPos,
            in NativeArray<NeighborRecord>.ReadOnly neighbors,
            int count,
            float myRadius,
            float separationWeight)
        {
            float3 separation = float3.zero;

            for (int i = 0; i < count; i++)
            {
                NeighborRecord neighborRecord = neighbors[i];
                float3 toNeighbor = myPos - neighborRecord.Position;
                float dist = math.length(toNeighbor);
                float desiredSpacing = myRadius + neighborRecord.Radius;

                if (dist > 0 && dist < desiredSpacing)
                {
                    // 가까운 병사일수록 강하게 밀어내기
                    separation += math.normalizesafe(toNeighbor) * (desiredSpacing - dist);
                }
            }

            return separation * separationWeight;
        }


        public static float EvaluateBlockAhead(in float3 position, in float3 flowDir, in NativeParallelMultiHashMap<int, NeighborRecord>.ReadOnly hash, float cellSize, float selfRadius, out bool isLeftBetter)
        {
            const float inv3 = 1.0f / 3.0f;

            float severity = 0f;
            int blockedCount = 0;

            // 앞으로 3스텝 검사
            float3 step = math.normalizesafe(flowDir) * cellSize;
            float3 pos = position;

            for (int s = 1; s <= 3; s++)
            {
                pos += step;

                int key = NeighborQuery.HashKey(pos, cellSize);
                int obsCount = 0;
                if (hash.TryGetFirstValue(key, out NeighborRecord neighborRecord, out NativeParallelMultiHashMapIterator<int> iterator))
                {
                    do
                    {
                        float3 d = neighborRecord.Position - pos;
                        float dist = math.length(d);
                        if (neighborRecord.IsObstacle != 0 && dist < (selfRadius + neighborRecord.Radius) * 1.1f)
                        {
                            obsCount++;
                        }
                    } while (hash.TryGetNextValue(out neighborRecord, ref iterator));
                }

                if (obsCount > 0)
                {
                    blockedCount++; // 연속 차단 셀 카운트
                }

                severity += math.saturate(obsCount * inv3);
            }

            // 좌/우 주변 셀 비교
            float3 leftStep = new float3(-flowDir.z, 0, flowDir.x) * cellSize;
            float3 rightStep = new float3(flowDir.z, 0, -flowDir.x) * cellSize;

            int freeLeft = CountFreeCells(position + leftStep, hash, cellSize);
            int freeRight = CountFreeCells(position + rightStep, hash, cellSize);

            isLeftBetter = freeLeft > freeRight;

            // --- blockedCount를 severity에 반영 ---
            if (blockedCount >= 3)
            {
                // 앞쪽 3셀 모두 막힘 → 완전 차단
                severity = 1f;
            }
            else
            {
                // 부분 차단 → severity 보정
                severity = math.saturate(severity + blockedCount * 0.25f);
            }

            return severity;
        }

        private static int CountFreeCells(in float3 position, in NativeParallelMultiHashMap<int, NeighborRecord>.ReadOnly hash, float cellSize)
        {
            int key = NeighborQuery.HashKey(position, cellSize);
            int free = 0;
            if (hash.TryGetFirstValue(key, out NeighborRecord neighborRecord, out NativeParallelMultiHashMapIterator<int> iterator))
            {
                do
                {
                    if (neighborRecord.IsObstacle == 0)
                    {
                        free++;
                    }
                } while (hash.TryGetNextValue(out neighborRecord, ref iterator));
            }

            return free;
        }

        public static float3 ComputeDetourTarget(float3 position, float3 forward, float offset, bool isLeftBetter)
        {
            float3 side = isLeftBetter
                ? new float3(-forward.z, 0, forward.x)
                : new float3(forward.z, 0, -forward.x);

            return position + forward * offset * 2f + side * offset;
        }
    }
}