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
    using Dots.Component.ComponentSystem;


    public class TroopSelectedRenderer : MonoBehaviour
    {
        [Header(nameof(LineRenderer))] [SerializeField]
        private LineRenderer lineRendererPrefab;

        [SerializeField] private float widthMultiplier = 0.05f;
        [SerializeField] private int numCapVertices = 8;
        [SerializeField] private int numCornerVertices = 8;
        [SerializeField] private Color redTeamColor = Color.red;
        [SerializeField] private Color blueTeamColor = Color.blue;

        [Header("Outline Padding")] [SerializeField]
        private float padding = 0.7f; // 병사들을 감싸는 여유 거리 (월드 단위)

        [SerializeField] private float miterLimit = 4f; // 너무 긴 miter(모서리 확장)를 제한

        [SerializeField] private int samplesPerUnit = 8; // 샘플 밀도 조절
        [SerializeField] private float height = 0.05f; // 지면 Y offset

        [Header("Drag Line")] [SerializeField] private GameObject dragLinePrefab;
        [SerializeField] private float tilingFactor = 0.25f;
        [SerializeField] private Material dragHeadMaterial;
        [SerializeField] private float dragHeadSideLength = 1f;


        private class ActiveTroopVisual
        {
            public SplineContainer SplineContainer;
            public LineRenderer LineRenderer;
            public float2[] SampledPositions;
            public Vector3 TroopPosition;
        }

        private EntityManager _entityManager;

        private ObjectPool<LineRenderer> _lineRendererPool;
        private ObjectPool<SplineContainer> _splineContainerPool;
        private Dictionary<Entity, ActiveTroopVisual> _activeSelectedTroops;

        private GameObject _dragLine;
        private Material _dragLineMaterial;
        private GameObject _dragHead;
        private Material _dragHeadMaterial;


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

            _activeSelectedTroops = new Dictionary<Entity, ActiveTroopVisual>();

            _dragLine = Instantiate(dragLinePrefab);
            _dragLineMaterial = _dragLine.GetComponentInChildren<Renderer>().material;
            _dragLine.SetActive(false);

            _dragHead = new GameObject("TroopDragHead");

            GameObject dragHeadMeshObject = new("TroopDragHeadMesh");
            Transform dragHeadMeshTransform = dragHeadMeshObject.transform;
            dragHeadMeshTransform.SetParent(_dragHead.transform);
            dragHeadMeshTransform.localRotation = Quaternion.Euler(90f, 0, 0);

            MeshFilter dragHeadMeshFilter = dragHeadMeshObject.AddComponent<MeshFilter>();
            MeshRenderer dragHeadMeshRenderer = dragHeadMeshObject.AddComponent<MeshRenderer>();

            Mesh mesh = new();

            // 정삼각형의 높이 (피타고라스)
            float headHeight = Mathf.Sqrt(3f) * 0.5f * dragHeadSideLength;

            // 정삼각형 정점 좌표 (2D 평면에 배치)
            mesh.vertices = new Vector3[]
            {
                new(-dragHeadSideLength * 0.5f, 0, 0), // 왼쪽 아래
                new(dragHeadSideLength * 0.5f, 0, 0), // 오른쪽 아래
                new(0, headHeight, 0) // 위쪽 꼭짓점
            };

            // 삼각형 인덱스
            mesh.triangles = new[] { 0, 1, 2 };

            // UV 좌표 (텍스처 매핑용)
            mesh.uv = new Vector2[]
            {
                new(0, 0),
                new(1, 0),
                new(0.5f, 1)
            };

            mesh.RecalculateNormals();

            dragHeadMeshFilter.mesh = mesh;
            dragHeadMeshRenderer.sharedMaterial = dragHeadMaterial;
            dragHeadMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            _dragHeadMaterial = dragHeadMeshRenderer.material;

            _dragHead.SetActive(false);
        }

        private void Update()
        {
            // 쿼리: Selected Troop with hull buffer
            EntityQuery selectedTroopQuery =
                _entityManager.CreateEntityQuery(
                    typeof(TroopSelected),
                    typeof(TroopHullPoint),
                    typeof(Team),
                    typeof(TroopAABB));

            using NativeList<Entity> unselectedTroopEntities = new(Allocator.Temp);

            if (!selectedTroopQuery.IsEmpty)
            {
                using NativeArray<Entity> selectedTroopEntities = selectedTroopQuery.ToEntityArray(Allocator.Temp);
                using NativeArray<TroopAABB> selectedTroopAABB = selectedTroopQuery.ToComponentDataArray<TroopAABB>(Allocator.Temp);

                foreach (Entity currentSelectedEntity in _activeSelectedTroops.AsValueEnumerable().Select(kvp => kvp.Key))
                {
                    if (!selectedTroopEntities.AsValueEnumerable().Any(entity => entity == currentSelectedEntity))
                    {
                        unselectedTroopEntities.Add(currentSelectedEntity);
                    }
                }

                using NativeArray<Team> selectedTroopTeams = selectedTroopQuery.ToComponentDataArray<Team>(Allocator.Temp);

                for (int i = 0, count = selectedTroopEntities.Length; i < count; ++i)
                {
                    Entity selectedTroopEntity = selectedTroopEntities[i];
                    Team selectedTroopTeam = selectedTroopTeams[i];

                    DynamicBuffer<TroopHullPoint> troopHullPoints = _entityManager.GetBuffer<TroopHullPoint>(selectedTroopEntity);

                    if (troopHullPoints.Length < 2)
                    {
                        RestoreTroopSelected(selectedTroopEntity);

                        continue;
                    }

                    // 1) hull 점들을 float2 리스트로 수집 (XZ)
                    int n = troopHullPoints.Length;
                    NativeArray<float2> hull = new(n, Allocator.Temp);
                    for (int j = 0; j < n; ++j)
                    {
                        hull[j] = troopHullPoints[j].Position;
                    }

                    // 2) centroid 계산 (노멀 방향 판정용)
                    float2 centroid = float2.zero;
                    for (int j = 0; j < n; ++j)
                    {
                        centroid += hull[j];
                    }

                    centroid /= n;

                    // 3) 각 정점에 대해 vertex normal 계산 -> outward 보정 -> padding 적용
                    float3[] knotArray = new float3[n];
                    float2[] sampledPositions = new float2[n];
                    for (int j = 0; j < n; ++j)
                    {
                        float2 prev = hull[(j - 1 + n) % n];
                        float2 curr = hull[j];
                        float2 next = hull[(j + 1) % n];

                        float2 dir1 = math.normalize(curr - prev);
                        float2 dir2 = math.normalize(next - curr);

                        // 각 에지의 외측(perpendicular). 회전방식은 ( -y, x ) 또는 ( y, -x ) 중 하나.
                        // 두 perpendicular을 합쳐 vertex normal을 구함
                        float2 n1 = new(-dir1.y, dir1.x);
                        float2 n2 = new(-dir2.y, dir2.x);

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
                        if (math.abs(appliedOffset) > maxOffset)
                        {
                            appliedOffset = math.sign(appliedOffset) * maxOffset;
                        }

                        float2 padded = curr + vnormal * appliedOffset;

                        knotArray[j] = new float3(padded.x, height, padded.y);
                        sampledPositions[j] = padded;
                    }

                    hull.Dispose();

                    // get or create SplineContainer
                    if (!_activeSelectedTroops.TryGetValue(selectedTroopEntity, out ActiveTroopVisual activeTroopVisual))
                    {
                        activeTroopVisual = new ActiveTroopVisual();

                        _activeSelectedTroops[selectedTroopEntity] = activeTroopVisual;
                    }

                    activeTroopVisual.LineRenderer ??= _lineRendererPool.Get();
                    activeTroopVisual.SplineContainer ??= _splineContainerPool.Get();
                    activeTroopVisual.TroopPosition = selectedTroopAABB[i].Center.xxy;
                    activeTroopVisual.TroopPosition.y = height;
                    activeTroopVisual.SampledPositions = sampledPositions;

                    // build spline (closed loop)
                    Spline spline = new(knotArray.Length, closed: true);
                    spline.AddRange(knotArray); // API supports AddRange(float3[])
                    activeTroopVisual.SplineContainer.Spline = spline;

                    // Optional: tweak tangents/tension for Catmull-Rom feel
                    // SplineUtility provides helpers (you can set tangent modes or call smoothing ops if needed)

                    // compute approximate spline length (or rely on points count), then sample positions
                    float approxLength = EstimateApproxLength(knotArray);
                    int totalSamples = math.max(4, (int)(approxLength * samplesPerUnit));

                    Vector3[] positions = new Vector3[totalSamples + 1];
                    for (int j = 0; j <= totalSamples; ++j)
                    {
                        float t = j / (float)totalSamples;

                        // EvaluatePosition returns a local position; container.TransformPoint -> world
                        float3 localPos = activeTroopVisual.SplineContainer.Spline.EvaluatePosition(t);
                        Vector3 worldPos = activeTroopVisual.SplineContainer.transform.TransformPoint(new Vector3(localPos.x, localPos.y, localPos.z));

                        positions[j] = worldPos;
                    }

                    // assign to LineRenderer
                    activeTroopVisual.LineRenderer.positionCount = positions.Length;
                    activeTroopVisual.LineRenderer.SetPositions(positions);

                    Color teamColor = selectedTroopTeam.Color == TeamColor.Red ? redTeamColor : blueTeamColor;

                    activeTroopVisual.LineRenderer.startColor = teamColor;
                    activeTroopVisual.LineRenderer.endColor = teamColor;
                }
            }
            else
            {
                foreach (Entity currentSelectedEntity in _activeSelectedTroops.AsValueEnumerable().Select(kvp => kvp.Key))
                {
                    unselectedTroopEntities.Add(currentSelectedEntity);
                }
            }

            foreach (Entity unselectedEntity in unselectedTroopEntities)
            {
                RestoreTroopSelected(unselectedEntity);
            }

            if (BattleInputSystem.CurrentSelectedEntity != Entity.Null &&
                _activeSelectedTroops.TryGetValue(BattleInputSystem.CurrentSelectedEntity, out ActiveTroopVisual currentActiveTroopVisual) &&
                _entityManager.IsComponentEnabled<DragStartWorldPosition>(BattleInputSystem.PointInput) &&
                _entityManager.IsComponentEnabled<DraggingWorldPosition>(BattleInputSystem.PointInput))
            {
                Vector3 dragStartPosition = currentActiveTroopVisual.TroopPosition;
                dragStartPosition.y = height;

                Vector3 draggingPosition = _entityManager.GetComponentData<DraggingWorldPosition>(BattleInputSystem.PointInput).Position;
                draggingPosition.y = height;


                float2 draggingPosition2D = new(draggingPosition.x, draggingPosition.z);
                if (IsPointInPolygon(draggingPosition2D, currentActiveTroopVisual.SampledPositions))
                {
                    _dragLine.SetActive(false);
                    _dragHead.SetActive(false);
                }
                else
                {
                    float2 dragStartPosition2D = new(dragStartPosition.x, dragStartPosition.z);

                #region calc dragStartPosition2D for cull drag line

                    bool isFound = false;
                    float minRateOnDragLine = float.MaxValue;
                    float2 bestIntersectionPoint = default;

                    int vertexCount = currentActiveTroopVisual.LineRenderer.positionCount;
                    using NativeArray<Vector3> vertices = new(vertexCount, Allocator.Temp);
                    currentActiveTroopVisual.LineRenderer.GetPositions(vertices);

                    for (int i = 0; i < vertexCount; ++i)
                    {
                        Vector3 vertex13d = vertices[i];
                        Vector3 vertex23d = vertices[(i + 1) % vertexCount];

                        float2 vertex1 = new(vertex13d.x, vertex13d.z);
                        float2 vertex2 = new(vertex23d.x, vertex23d.z);

                        if (IntersectionSegments(dragStartPosition2D, draggingPosition2D, vertex1, vertex2, out float2 inter, out float rateOnDragLine))
                        {
                            // choose the earliest intersection along A->B (smallest t)
                            if (rateOnDragLine is >= 0f and <= 1f && rateOnDragLine < minRateOnDragLine)
                            {
                                isFound = true;

                                minRateOnDragLine = rateOnDragLine;
                                bestIntersectionPoint = inter;
                            }
                        }
                    }

                    if (isFound)
                    {
                        dragStartPosition = new Vector3(bestIntersectionPoint.x, dragStartPosition.y, bestIntersectionPoint.y);
                    }

                #endregion

                    Transform dragLineTransform = _dragLine.transform;

                    dragLineTransform.position = dragStartPosition;

                    Vector3 dragLineScale = dragLineTransform.localScale;
                    float dragHeadHeight = Mathf.Sqrt(3f) * 0.5f * dragHeadSideLength;
                    dragLineScale.z = Vector3.Distance(dragStartPosition, draggingPosition) - dragHeadHeight * 0.9f;
                    dragLineTransform.localScale = dragLineScale;

                    Vector3 dragDirection = Vector3.Normalize(draggingPosition - dragStartPosition);
                    dragLineTransform.forward = dragDirection;

                    _dragLineMaterial.color = currentActiveTroopVisual.LineRenderer.startColor;
                    _dragLineMaterial.mainTextureScale = new Vector2(dragLineScale.z * tilingFactor, 1f);

                    _dragLine.SetActive(true);

                    Transform dragHeadTransform = _dragHead.transform;
                    dragHeadTransform.position = draggingPosition - dragDirection * dragHeadHeight;
                    dragHeadTransform.forward = dragDirection;

                    _dragHeadMaterial.color = currentActiveTroopVisual.LineRenderer.startColor;

                    _dragHead.SetActive(true);
                }
            }
            else
            {
                _dragLine.SetActive(false);
                _dragHead.SetActive(false);
            }
        }

        private void RestoreTroopSelected(Entity troopEntity)
        {
            if (!_activeSelectedTroops.TryGetValue(troopEntity, out ActiveTroopVisual activeTroopVisual))
            {
                return;
            }

            if (activeTroopVisual.LineRenderer)
            {
                _lineRendererPool.Release(activeTroopVisual.LineRenderer);
            }

            if (activeTroopVisual.SplineContainer)
            {
                _splineContainerPool.Release(activeTroopVisual.SplineContainer);
            }

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

        /// <summary>
        /// segment (pointA->pointB) vs segment (pointC->pointD) intersection on 2D XZ
        /// </summary>
        /// <param name="pointA"></param>
        /// <param name="pointB"></param>
        /// <param name="pointC"></param>
        /// <param name="pointD"></param>
        /// <param name="intersectionPoint">intersection point on segment (pointA->pointB)</param>
        /// <param name="rateIntersectionSegmentAtoB">rate of intersection point along a segment (pointA->pointB) (0..1)</param>
        /// <returns>if segment (pointA->pointB) vs segment (pointC->pointD) is cross, true else false</returns>
        private static bool IntersectionSegments(float2 pointA, float2 pointB, float2 pointC, float2 pointD, out float2 intersectionPoint, out float rateIntersectionSegmentAtoB)
        {
            intersectionPoint = default;
            rateIntersectionSegmentAtoB = 0f;

            float2 segmentAtoB = pointB - pointA;
            float2 segmentCtoD = pointD - pointC;

            float cross = segmentAtoB.x * segmentCtoD.y - segmentAtoB.y * segmentCtoD.x;
            if (math.abs(cross) < 1e-8f) // parallel or nearly so 
            {
                return false;
            }

            float2 segmentAtoC = pointC - pointA;

            rateIntersectionSegmentAtoB = (segmentAtoC.x * segmentCtoD.y - segmentAtoC.y * segmentCtoD.x) / cross;
            float rateIntersectionSegmentCtoD = (segmentAtoC.x * segmentAtoB.y - segmentAtoC.y * segmentAtoB.x) / cross;

            if (rateIntersectionSegmentAtoB is >= 0f and <= 1f &&
                rateIntersectionSegmentCtoD is >= 0f and <= 1f)
            {
                intersectionPoint = new float2(pointA.x + rateIntersectionSegmentAtoB * segmentAtoB.x, pointA.y + rateIntersectionSegmentAtoB * segmentAtoB.y);

                return true;
            }

            return false;
        }

        // Winding/odd-even test: check if 2D point is inside polygon (polygon in world XZ given)
        private static bool IsPointInPolygon(float2 point, float2[] polygonInWorld)
        {
            int vertexCount = polygonInWorld.Length;

            bool isInside = false;
            for (int i = 0, j = vertexCount - 1; i < vertexCount; j = i++)
            {
                float2 a = polygonInWorld[i];
                float2 b = polygonInWorld[j];

                // check if the point is exactly on edge - treat as inside
                if (IsPointOnSegment(point, a, b))
                {
                    return true;
                }

                bool intersect =
                    a.y > point.y != b.y > point.y &&
                    point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y + 0f) + a.x;

                if (intersect)
                {
                    isInside = !isInside;
                }
            }

            return isInside;
        }

        private static bool IsPointOnSegment(float2 p, float2 a, float2 b, float eps = 1e-6f)
        {
            float2 ap = p - a;
            float2 ab = b - a;

            float cross = ap.x * ab.y - ap.y * ab.x;

            if (math.abs(cross) > eps)
            {
                return false;
            }

            float dot = math.dot(ap, ab);
            if (dot < -eps)
            {
                return false;
            }

            return !(dot > math.dot(ab, ab) + eps);
        }
    }
}