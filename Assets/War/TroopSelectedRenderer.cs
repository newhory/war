using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
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
        [SerializeField] private Color selectedColor = Color.yellowNice;
        [SerializeField] private Color redTeamColor = Color.red;
        [SerializeField] private Color blueTeamColor = Color.blue;

        [Header("Outline Padding")] [SerializeField]
        private float padding = 0.7f; // 병사들을 감싸는 여유 거리 (월드 단위)

        [SerializeField] private float miterLimit = 4f; // 너무 긴 miter(모서리 확장)를 제한
        [SerializeField] private int samplesPerUnit = 8; // 샘플 밀도 조절
        [SerializeField] private float height = 0.05f; // 지면 Y offset

        [Header("Line")] [SerializeField] private float lineTilingFactor = 0.25f;
        [SerializeField] private Material lineHeadMaterial;
        [SerializeField] private float lineHeadSideLength = 1f;

        [Header("Move Line")] [SerializeField] private GameObject moveLinePrefab;

        [Header("Drag Line")] [SerializeField] private GameObject dragLinePrefab;


        private class TroopLine : IDisposable
        {
            private readonly GameObject _dragLine;
            private readonly Material _dragLineMaterial;
            private readonly Transform _dragLineTransform;

            private readonly GameObject _dragHead;
            private readonly Material _dragHeadMaterial;
            private readonly Transform _dragHeadTransform;
            private readonly float _dragHeadSideLength;


            public TroopLine(GameObject dragLinePrefab, float dragHeadSideLength, Material dragHeadMaterial)
            {
                _dragLine = Instantiate(dragLinePrefab);
                _dragLineMaterial = _dragLine.GetComponentInChildren<Renderer>().material;
                _dragLineTransform = _dragLine.transform;
                _dragLine.SetActive(false);

                _dragHead = new GameObject("TroopDragHead");
                _dragHeadTransform = _dragHead.transform;

                _dragHeadSideLength = dragHeadSideLength;

                GameObject dragHeadMeshObject = new("TroopDragHeadMesh");
                Transform dragHeadMeshTransform = dragHeadMeshObject.transform;
                dragHeadMeshTransform.SetParent(_dragHead.transform);
                dragHeadMeshTransform.localRotation = Quaternion.Euler(90f, 0, 0);

                MeshFilter dragHeadMeshFilter = dragHeadMeshObject.AddComponent<MeshFilter>();
                MeshRenderer dragHeadMeshRenderer = dragHeadMeshObject.AddComponent<MeshRenderer>();

                Mesh mesh = new();

                // 정삼각형의 높이 (피타고라스)
                float headHeight = Mathf.Sqrt(3f) * 0.5f * _dragHeadSideLength;

                // 정삼각형 정점 좌표 (2D 평면에 배치)
                mesh.vertices = new Vector3[]
                {
                    new(-_dragHeadSideLength * 0.5f, 0, 0), // 왼쪽 아래
                    new(_dragHeadSideLength * 0.5f, 0, 0), // 오른쪽 아래
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


            public void Draw(Vector3 dragStartPosition, Vector3 draggingPosition, Color color, float tilingFactor = 1f)
            {
                Vector3 dragDirection = Vector3.Normalize(draggingPosition - dragStartPosition);
                if (dragDirection == Vector3.zero)
                {
                    return;
                }
                
                _dragLineTransform.position = dragStartPosition;

                Vector3 dragLineScale = _dragLineTransform.localScale;
                float dragHeadHeight = Mathf.Sqrt(3f) * 0.5f * _dragHeadSideLength;
                dragLineScale.z = Vector3.Distance(dragStartPosition, draggingPosition) - dragHeadHeight * 0.9f;
                _dragLineTransform.localScale = dragLineScale;
                
                _dragLineTransform.forward = dragDirection;

                _dragLineMaterial.color = color;
                _dragLineMaterial.mainTextureScale = new Vector2(dragLineScale.z * tilingFactor, 1f);

                _dragLine.SetActive(true);

                _dragHeadTransform.position = draggingPosition - dragDirection * dragHeadHeight;
                _dragHeadTransform.forward = dragDirection;

                _dragHeadMaterial.color = color;

                _dragHead.SetActive(true);
            }

            public void Hide()
            {
                _dragLine.SetActive(false);
                _dragHead.SetActive(false);
            }

            public void Dispose()
            {
                Destroy(_dragLine);
                Destroy(_dragHead);
            }
        }

        private class ActiveTroopVisual : IDisposable
        {
            public int FrameCountUpdated;

            public SplineContainer SplineContainer;
            public LineRenderer LineRenderer;
            public TroopLine TroopLine;
            public NativeList<float2> SampledPositions;
            public Vector3 TroopPosition;


            public void Dispose()
            {
                if (LineRenderer)
                {
                    lineRendererPool.Release(LineRenderer);
                }

                if (SplineContainer)
                {
                    splineContainerPool.Release(SplineContainer);
                }

                if (TroopLine is not null)
                {
                    troopLinePool.Release(TroopLine);
                }

                if (SampledPositions.IsCreated)
                {
                    SampledPositions.Dispose();
                }
            }
        }


        private static ObjectPool<LineRenderer> lineRendererPool;
        private static ObjectPool<SplineContainer> splineContainerPool;
        private static ObjectPool<TroopLine> troopLinePool;


        private EntityManager _entityManager;

        private Dictionary<Entity, ActiveTroopVisual> _activeSelectedTroops;

        private TroopLine _dragTroopLine;
        private GameObject _dragLine;
        private Material _dragLineMaterial;
        private GameObject _dragHead;
        private Material _dragHeadMaterial;

    #region buffer for TrySelectTroop

        private NativeList<Entity> _unselectedTroopEntities;
        private NativeList<float2> _hullBuffer;
        private NativeList<float3> _knotBuffer;
        private NativeList<Vector3> _lineRendererPositionBuffer;

    #endregion

        private void Awake()
        {
            _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

            lineRendererPool ??=
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

            splineContainerPool ??=
                new ObjectPool<SplineContainer>(
                    createFunc: () => new GameObject().AddComponent<SplineContainer>(),
                    actionOnGet: splineContainer => splineContainer.gameObject.SetActive(true),
                    actionOnRelease: splineContainer => splineContainer.gameObject.SetActive(false),
                    actionOnDestroy: splineContainer => Destroy(splineContainer.gameObject),
                    collectionCheck: true, // An Editor-only check that determines if an instance is returned back to the pool. Throws an exception if the instance is already in the pool.
                    defaultCapacity: 1);

            troopLinePool ??=
                new ObjectPool<TroopLine>(
                    createFunc: () => new TroopLine(moveLinePrefab, lineHeadSideLength, lineHeadMaterial),
                    actionOnRelease: line => line.Hide(),
                    actionOnDestroy: line => line.Dispose(),
                    collectionCheck: true, // An Editor-only check that determines if an instance is returned back to the pool. Throws an exception if the instance is already in the pool.
                    defaultCapacity: 1);

            _activeSelectedTroops = new Dictionary<Entity, ActiveTroopVisual>();

            _dragTroopLine = new TroopLine(dragLinePrefab, lineHeadSideLength, lineHeadMaterial);

            _unselectedTroopEntities = new NativeList<Entity>(Allocator.Persistent);
            _hullBuffer = new NativeList<float2>(Allocator.Persistent);
            _knotBuffer = new NativeList<float3>(Allocator.Persistent);
            _lineRendererPositionBuffer = new NativeList<Vector3>(Allocator.Persistent);
        }

        private void OnDestroy()
        {
            if (_unselectedTroopEntities.IsCreated)
            {
                _unselectedTroopEntities.Dispose();
            }

            if (_hullBuffer.IsCreated)
            {
                _hullBuffer.Dispose();
            }

            if (_knotBuffer.IsCreated)
            {
                _knotBuffer.Dispose();
            }

            if (_lineRendererPositionBuffer.IsCreated)
            {
                _lineRendererPositionBuffer.Dispose();
            }
        }

        private void Update()
        {
            Entity selectedByInputTroopEntity = BattleInputSystem.CurrentSelectedEntity;

            // 쿼리: Selected Troop with hull buffer
            EntityQuery selectedTroopQuery =
                _entityManager.CreateEntityQuery(
                    typeof(TroopSelected),
                    typeof(TroopHullPoint),
                    typeof(Team),
                    typeof(TroopAABB));

            _unselectedTroopEntities.Clear();

            if (!selectedTroopQuery.IsEmpty)
            {
                using NativeArray<Entity> currentSelectedTroops = selectedTroopQuery.ToEntityArray(Allocator.Temp);
                using NativeHashSet<Entity> selectedTroopEntities = new(currentSelectedTroops.Length, Allocator.Temp);

                for (int i = 0, count = currentSelectedTroops.Length; i < count; ++i)
                {
                    Entity selectedTroopEntity = currentSelectedTroops[i];

                    do
                    {
                        if (TrySelectTroop(
                                selectedTroopEntity,
                                selectedByInputTroopEntity == selectedTroopEntity
                                    ? selectedColor
                                    : _entityManager.GetComponentData<Team>(selectedTroopEntity).Color == TeamColor.Red
                                        ? redTeamColor
                                        : blueTeamColor))
                        {
                            selectedTroopEntities.Add(selectedTroopEntity);
                        }

                        if (!_entityManager.IsComponentEnabled<TroopStateMoveToTarget>(selectedTroopEntity))
                        {
                            break;
                        }

                        selectedTroopEntity = _entityManager.GetComponentData<TroopTargetForAttack>(selectedTroopEntity).TargetTroop;
                        if (selectedTroopEntity == Entity.Null ||
                            !_entityManager.Exists(selectedTroopEntity) ||
                            selectedTroopEntities.Contains(selectedTroopEntity))
                        {
                            break;
                        }
                    } while (selectedTroopEntity != Entity.Null);
                }

                foreach (Entity currentSelectedEntity in _activeSelectedTroops.AsValueEnumerable().Select(kvp => kvp.Key))
                {
                    if (!selectedTroopEntities.Contains(currentSelectedEntity))
                    {
                        _unselectedTroopEntities.Add(currentSelectedEntity);
                    }
                }
            }
            else
            {
                foreach (Entity currentSelectedEntity in _activeSelectedTroops.AsValueEnumerable().Select(kvp => kvp.Key))
                {
                    _unselectedTroopEntities.Add(currentSelectedEntity);
                }
            }

            foreach (Entity unselectedEntity in _unselectedTroopEntities)
            {
                RestoreTroopSelected(unselectedEntity);
            }

            foreach (Entity selectedTroopEntity in _activeSelectedTroops.AsValueEnumerable().Select(kvp => kvp.Key))
            {
                Color lineColor =
                    _entityManager.GetComponentData<Team>(selectedTroopEntity).Color == TeamColor.Red
                        ? redTeamColor
                        : blueTeamColor;

                ActiveTroopVisual currentSelectedTroopVisual = _activeSelectedTroops[selectedTroopEntity];

                TroopTargetForAttack targetForAttack = _entityManager.GetComponentData<TroopTargetForAttack>(selectedTroopEntity);
                if (_activeSelectedTroops.TryGetValue(targetForAttack.TargetTroop, out ActiveTroopVisual targetTroopVisual))
                {
                    currentSelectedTroopVisual.TroopLine ??= troopLinePool.Get();

                    DrawTroopLine(currentSelectedTroopVisual.TroopLine, targetTroopVisual.TroopPosition, currentSelectedTroopVisual, targetTroopVisual, lineColor);
                }
                else
                {
                    LocalTransform destination = _entityManager.GetComponentData<LocalTransform>(selectedTroopEntity);
                    Vector3 destinationPosition = destination.Position;
                    destinationPosition.y = height;

                    currentSelectedTroopVisual.TroopLine ??= troopLinePool.Get();

                    DrawTroopLine(currentSelectedTroopVisual.TroopLine, destinationPosition, currentSelectedTroopVisual, null, lineColor);
                }
            }

            if (selectedByInputTroopEntity != Entity.Null &&
                _activeSelectedTroops.TryGetValue(selectedByInputTroopEntity, out ActiveTroopVisual currentActiveTroopVisual) &&
                _entityManager.IsComponentEnabled<DragStartWorldPosition>(BattleInputSystem.PointInput) &&
                _entityManager.IsComponentEnabled<DraggingWorldPosition>(BattleInputSystem.PointInput))
            {
                Vector3 draggingPosition;

                if (BattleInputSystem.CurrentTargetCandidateEntity != Entity.Null &&
                    _activeSelectedTroops.TryGetValue(BattleInputSystem.CurrentTargetCandidateEntity, out ActiveTroopVisual currentTargetCandidateActiveTroopVisual))
                {
                    draggingPosition = currentTargetCandidateActiveTroopVisual.TroopPosition;
                }
                else
                {
                    currentTargetCandidateActiveTroopVisual = null;
                    draggingPosition = _entityManager.GetComponentData<DraggingWorldPosition>(BattleInputSystem.PointInput).Position;
                }

                draggingPosition.y = height;

                DrawTroopLine(_dragTroopLine, draggingPosition, currentActiveTroopVisual, currentTargetCandidateActiveTroopVisual, selectedColor);
            }
            else
            {
                _dragTroopLine.Hide();
            }
        }

        private void RestoreTroopSelected(Entity troopEntity)
        {
            if (!_activeSelectedTroops.TryGetValue(troopEntity, out ActiveTroopVisual activeTroopVisual))
            {
                return;
            }

            activeTroopVisual.Dispose();

            _activeSelectedTroops.Remove(troopEntity);
        }

        private bool TrySelectTroop(Entity selectedTroopEntity, Color lineColor)
        {
            DynamicBuffer<TroopHullPoint> troopHullPoints = _entityManager.GetBuffer<TroopHullPoint>(selectedTroopEntity);
            int hullPointCount = troopHullPoints.Length;
            if (hullPointCount < 2)
            {
                return false;
            }

            // get or create SplineContainer
            if (!_activeSelectedTroops.TryGetValue(selectedTroopEntity, out ActiveTroopVisual activeTroopVisual))
            {
                activeTroopVisual = new ActiveTroopVisual();

                _activeSelectedTroops[selectedTroopEntity] = activeTroopVisual;
            }

            int frameCount = Time.frameCount;

            if (activeTroopVisual.FrameCountUpdated == frameCount)
            {
                return true;
            }

            activeTroopVisual.FrameCountUpdated = frameCount;

            // 1) hull 점들을 float2 리스트로 수집 (XZ)
            _hullBuffer.Clear();
            for (int i = 0; i < hullPointCount; ++i)
            {
                _hullBuffer.Add(troopHullPoints[i].Position);
            }

            // 2) centroid 계산 (노멀 방향 판정용)
            float2 centroid = float2.zero;
            for (int i = 0; i < hullPointCount; ++i)
            {
                centroid += _hullBuffer[i];
            }

            centroid /= hullPointCount;

            if (!activeTroopVisual.SampledPositions.IsCreated)
            {
                activeTroopVisual.SampledPositions = new NativeList<float2>(Allocator.Persistent);
            }
            else
            {
                activeTroopVisual.SampledPositions.Clear();
            }

            // 3) 각 정점에 대해 vertex normal 계산 -> outward 보정 -> padding 적용
            _knotBuffer.Clear();
            for (int i = 0; i < hullPointCount; ++i)
            {
                float2 prev = _hullBuffer[(i - 1 + hullPointCount) % hullPointCount];
                float2 curr = _hullBuffer[i];
                float2 next = _hullBuffer[(i + 1) % hullPointCount];

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

                activeTroopVisual.SampledPositions.Add(padded);
                _knotBuffer.Add(new float3(padded.x, height, padded.y));
            }

            activeTroopVisual.LineRenderer ??= lineRendererPool.Get();
            activeTroopVisual.SplineContainer ??= splineContainerPool.Get();
            activeTroopVisual.TroopPosition = _entityManager.GetComponentData<TroopAABB>(selectedTroopEntity).Center.xxy;
            activeTroopVisual.TroopPosition.y = height;

            // build spline (closed loop)
            Spline spline = activeTroopVisual.SplineContainer.Spline ??= new Spline();
            spline.Clear();
            spline.Closed = true;

            foreach (float3 knot in _knotBuffer)
            {
                spline.Add(knot);
            }

            // Optional: tweak tangents/tension for Catmull-Rom feel
            // SplineUtility provides helpers (you can set tangent modes or call smoothing ops if needed)

            // compute approximate spline length (or rely on points count), then sample positions
            float approxLength = EstimateApproxLength(_knotBuffer.AsReadOnly());
            int totalSamples = math.max(4, (int)(approxLength * samplesPerUnit));

            _lineRendererPositionBuffer.Clear();
            for (int j = 0; j <= totalSamples; ++j)
            {
                float t = j / (float)totalSamples;

                // EvaluatePosition returns a local position; container.TransformPoint -> world
                float3 localPos = activeTroopVisual.SplineContainer.Spline.EvaluatePosition(t);
                Vector3 worldPos = activeTroopVisual.SplineContainer.transform.TransformPoint(new Vector3(localPos.x, localPos.y, localPos.z));

                _lineRendererPositionBuffer.Add(worldPos);
            }

            // assign to LineRenderer
            activeTroopVisual.LineRenderer.positionCount = _lineRendererPositionBuffer.Length;
            activeTroopVisual.LineRenderer.SetPositions(_lineRendererPositionBuffer.AsArray());

            activeTroopVisual.LineRenderer.startColor = lineColor;
            activeTroopVisual.LineRenderer.endColor = lineColor;

            return true;
        }

        private void DrawTroopLine(TroopLine troopLine, Vector3 endPosition, ActiveTroopVisual troopVisual, ActiveTroopVisual targetTroopVisual, Color lineColor)
        {
            Vector3 startPosition = troopVisual.TroopPosition;

            float2 endPosition2D = new(endPosition.x, endPosition.z);
            if (IsPointInPolygon(endPosition2D, troopVisual.SampledPositions.AsReadOnly()))
            {
                troopLine.Hide();
            }
            else
            {
                float2 startPosition2D = new(startPosition.x, startPosition.z);

                int vertexCount = troopVisual.LineRenderer.positionCount;
                using NativeArray<Vector3> vertices = new(vertexCount, Allocator.Temp);
                troopVisual.LineRenderer.GetPositions(vertices);

                if (TryGetIntersectionPoint(startPosition2D, endPosition2D, vertices, out float2 startIntersectionPoint))
                {
                    startPosition = new Vector3(startIntersectionPoint.x, startPosition.y, startIntersectionPoint.y);
                }

                if (targetTroopVisual is not null)
                {
                    int currentTargetVertexCount = targetTroopVisual.LineRenderer.positionCount;
                    using NativeArray<Vector3> currentTargetVertices = new(currentTargetVertexCount, Allocator.Temp);
                    targetTroopVisual.LineRenderer.GetPositions(currentTargetVertices);

                    if (TryGetIntersectionPoint(endPosition2D, startPosition2D, currentTargetVertices, out float2 endIntersectionPoint))
                    {
                        endPosition = new Vector3(endIntersectionPoint.x, endPosition.y, endIntersectionPoint.y);
                    }
                }

                troopLine.Draw(startPosition, endPosition, lineColor, lineTilingFactor);
            }
        }

        private static float EstimateApproxLength(NativeArray<float3>.ReadOnly pts)
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

        private static bool TryGetIntersectionPoint(float2 pointA, float2 pointB, NativeArray<Vector3> vertices, out float2 bestIntersectionPoint)
        {
            bool isFound = false;
            float minRateOnDragLine = float.MaxValue;
            bestIntersectionPoint = default;

            int vertexCount = vertices.Length;

            for (int i = 0; i < vertexCount; ++i)
            {
                Vector3 vertex13d = vertices[i];
                Vector3 vertex23d = vertices[(i + 1) % vertexCount];

                float2 vertex1 = new(vertex13d.x, vertex13d.z);
                float2 vertex2 = new(vertex23d.x, vertex23d.z);

                if (IntersectionSegments(pointA, pointB, vertex1, vertex2, out float2 inter, out float rateOnDragLine))
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

            return isFound;
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
        private static bool IsPointInPolygon(float2 point, NativeArray<float2>.ReadOnly polygonInWorld)
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