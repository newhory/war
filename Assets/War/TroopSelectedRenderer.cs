using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Splines;
using ZLinq;


namespace War
{
    using Dots.Component;


    public class TroopSelectedRenderer : MonoBehaviour
    {
        [Header(nameof(LineRenderer))]
        [SerializeField] private LineRenderer lineRendererPrefab;
        [SerializeField] private float widthMultiplier = 0.05f;
        [SerializeField] private int numCapVertices = 8;
        [SerializeField] private int numCornerVertices = 8;

        [Header("Outline Padding")]
        [SerializeField] private float padding = 0.7f; // 병사들을 감싸는 여유 거리 (월드 단위)
        [SerializeField] private float miterLimit = 4f; // 너무 긴 miter(모서리 확장)를 제한

        [SerializeField] private int samplesPerUnit = 8; // 샘플 밀도 조절
        [SerializeField] private float height = 0.05f; // 지면 Y offset


        private EntityManager _entityManager;


        private ObjectPool<LineRenderer> _lineRendererPool;
        private ObjectPool<SplineContainer> _splineContainerPool;
        private Dictionary<Entity, (SplineContainer, LineRenderer)> _activeSelectedTroops;


        private void Awake()
        {
            _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

            _lineRendererPool =
                new ObjectPool<LineRenderer>(
                    createFunc: () =>
                    {
                        LineRenderer lineRenderer = Instantiate(lineRendererPrefab);

                        lineRenderer.widthCurve = AnimationCurve.Constant(0f, 1f, 1f);
                        lineRenderer.widthMultiplier = widthMultiplier; // 원하는 굵기
                        lineRenderer.numCapVertices = numCapVertices; // 끝을 라운드 처리
                        lineRenderer.numCornerVertices = numCornerVertices; // 커브가 있을 때도 부드러움

                        return lineRenderer;
                    },
                    actionOnGet: lineRenderer => lineRenderer.gameObject.SetActive(true),
                    actionOnRelease: lineRenderer => lineRenderer.gameObject.SetActive(false),
                    actionOnDestroy: lineRenderer => Destroy(lineRenderer.gameObject),
                    collectionCheck: true, // An Editor-only check that determines if an instance is returned back to the pool. Throws an exception if the instance is already in the pool.
                    defaultCapacity: 1);

            _splineContainerPool =
                new ObjectPool<SplineContainer>(
                    createFunc: () => new GameObject().AddComponent<SplineContainer>(),
                    actionOnGet: splineContainer => splineContainer.gameObject.SetActive(true),
                    actionOnRelease: splineContainer => splineContainer.gameObject.SetActive(false),
                    actionOnDestroy: splineContainer => Destroy(splineContainer.gameObject),
                    collectionCheck: true, // An Editor-only check that determines if an instance is returned back to the pool. Throws an exception if the instance is already in the pool.
                    defaultCapacity: 1);

            _activeSelectedTroops = new Dictionary<Entity, (SplineContainer, LineRenderer)>();
        }

        private void Update()
        {
            // 쿼리: Selected Troop with hull buffer
            EntityQuery selectedTroopQuery = _entityManager.CreateEntityQuery(typeof(TroopSelected), typeof(TroopHullPoint));

            using NativeArray<Entity> selectedTroopEntities = selectedTroopQuery.ToEntityArray(Allocator.Temp);
            using NativeList<Entity> unselectedTroopEntities = new(Allocator.Temp);

            foreach (Entity currentSelectedEntity in _activeSelectedTroops.AsValueEnumerable().Select(kvp => kvp.Key))
            {
                if (!selectedTroopEntities.AsValueEnumerable().Any(entity => entity == currentSelectedEntity))
                {
                    unselectedTroopEntities.Add(currentSelectedEntity);
                }
            }

            foreach (Entity unselectedEntity in unselectedTroopEntities)
            {
                RestoreTroopSelected(unselectedEntity);
            }

            if (selectedTroopEntities.Length == 0)
            {
                return;
            }

            foreach (Entity selectedTroopEntity in selectedTroopEntities)
            {
                DynamicBuffer<TroopHullPoint> troopHullPoints = _entityManager.GetBuffer<TroopHullPoint>(selectedTroopEntity);

                if (troopHullPoints.Length < 2)
                {
                    RestoreTroopSelected(selectedTroopEntity);

                    continue;
                }

                // 1) hull 점들을 float2 리스트로 수집 (XZ)
                int n = troopHullPoints.Length;
                NativeArray<float2> hull = new NativeArray<float2>(n, Allocator.Temp);
                for (int i = 0; i < n; ++i)
                {
                    float3 p = troopHullPoints[i].Position;
                    hull[i] = new float2(p.x, p.z);
                }

                // 2) centroid 계산 (노멀 방향 판정용)
                float2 centroid = float2.zero;
                for (int i = 0; i < n; ++i)
                {
                    centroid += hull[i];
                }

                centroid /= n;

                // 3) 각 정점에 대해 vertex normal 계산 -> outward 보정 -> padding 적용
                float3[] knotArray = new float3[n];
                for (int i = 0; i < n; ++i)
                {
                    float2 prev = hull[(i - 1 + n) % n];
                    float2 curr = hull[i];
                    float2 next = hull[(i + 1) % n];

                    float2 dir1 = math.normalize(curr - prev);
                    float2 dir2 = math.normalize(next - curr);

                    // 각 에지의 외측(perpendicular). 회전방식은 ( -y, x ) 또는 ( y, -x ) 중 하나.
                    // 두 perpendicular을 합쳐 vertex normal을 구함
                    float2 n1 = new float2(-dir1.y, dir1.x);
                    float2 n2 = new float2(-dir2.y, dir2.x);

                    float2 vnormal = n1 + n2;
                    float vlen = math.length(vnormal);

                    if (vlen < 1e-4f)
                    {
                        // 거의 평행(혹은 수치 문제)일 땐 현재 엣지의 perpendicular 사용
                        vnormal = new float2(-dir1.y, dir1.x);
                        vlen = math.length(vnormal);
                        if (vlen < 1e-4f)
                        {
                            vnormal = new float2(-dir2.y, dir2.x);
                            vlen = math.length(vnormal);
                        }
                    }

                    vnormal /= vlen; // 정규화

                    // 보정: centroid 방향과 같은 쪽인지 확인. 아니라면 뒤집음(바깥쪽을 향하게)
                    float2 fromCentroid = curr - centroid;
                    if (math.dot(vnormal, fromCentroid) < 0f)
                    {
                        vnormal = -vnormal;
                    }

                    // miter limit: 지나치게 길어지는 경우 clamp (optional 안전장치)
                    float appliedOffset = padding;
                    // 간단한 miter limit: (padding * miterLimit) 를 초과하면 clamp
                    // (여기선 vertex normal이 극단적이면 큰 이동 발생 가능 -> 제한)
                    float maxOffset = padding * miterLimit;
                    if (math.abs(appliedOffset) > maxOffset) appliedOffset = math.sign(appliedOffset) * maxOffset;

                    float2 padded = curr + vnormal * appliedOffset;

                    knotArray[i] = new float3(padded.x, height, padded.y);
                }

                hull.Dispose();

                // get or create SplineContainer
                if (!_activeSelectedTroops.TryGetValue(selectedTroopEntity, out (SplineContainer splineContainer, LineRenderer lineRenderer) container))
                {
                    container.lineRenderer = _lineRendererPool.Get();
                    container.splineContainer = _splineContainerPool.Get();

                    _activeSelectedTroops[selectedTroopEntity] = container;
                }

                // build spline (closed loop)
                Spline spline = new(knotArray.Length, closed: true);
                spline.AddRange(knotArray); // API supports AddRange(float3[])
                container.splineContainer.Spline = spline;

                // Optional: tweak tangents/tension for Catmull-Rom feel
                // SplineUtility provides helpers (you can set tangent modes or call smoothing ops if needed)

                // compute approximate spline length (or rely on points count), then sample positions
                float approxLength = EstimateApproxLength(knotArray);
                int totalSamples = math.max(4, (int)(approxLength * samplesPerUnit));

                Vector3[] positions = new Vector3[totalSamples + 1];
                for (int i = 0; i <= totalSamples; ++i)
                {
                    float t = i / (float)totalSamples;

                    // EvaluatePosition returns local position; container.TransformPoint -> world
                    float3 localPos = container.splineContainer.Spline.EvaluatePosition(t);
                    Vector3 worldPos = container.splineContainer.transform.TransformPoint(new Vector3(localPos.x, localPos.y, localPos.z));

                    positions[i] = worldPos;
                }

                // assign to LineRenderer
                container.lineRenderer.positionCount = positions.Length;
                container.lineRenderer.SetPositions(positions);
            }
        }

        private void RestoreTroopSelected(Entity troopEntity)
        {
            if (!_activeSelectedTroops.TryGetValue(troopEntity, out (SplineContainer, LineRenderer) container))
            {
                return;
            }

            (SplineContainer splineContainer, LineRenderer lineRenderer) = container;

            _lineRendererPool.Release(lineRenderer);
            _splineContainerPool.Release(splineContainer);

            _activeSelectedTroops.Remove(troopEntity);
        }

        private static float EstimateApproxLength(float3[] pts)
        {
            float s = 0;
            for (int i = 0; i < pts.Length; ++i)
            {
                float3 a = pts[i];
                float3 b = pts[(i + 1) % pts.Length];
                s += math.distance(a, b);
            }

            return s;
        }
    }
}