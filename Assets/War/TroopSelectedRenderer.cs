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
        [Header(nameof(LineRenderer))]
        [SerializeField] private LineRenderer lineRendererPrefab;

        [SerializeField] private float widthMultiplier = 0.05f;
        [SerializeField] private int numCapVertices = 8;
        [SerializeField] private int numCornerVertices = 8;
        [SerializeField] private Color redTeamColor = Color.red;
        [SerializeField] private Color blueTeamColor = Color.blue;

        [Header("Outline Padding")]
        [SerializeField] private float padding = 0.7f; // 병사들을 감싸는 여유 거리 (월드 단위)

        [SerializeField] private float miterLimit = 4f; // 너무 긴 miter(모서리 확장)를 제한

        [SerializeField] private int samplesPerUnit = 8; // 샘플 밀도 조절
        [SerializeField] private float height = 0.05f; // 지면 Y offset

        [Header("Drag Line")]
        [SerializeField] private GameObject dragLinePrefab;
        [SerializeField] private Material dragHeadMaterial;
        [SerializeField] private float dragHeadSideLength = 1f;


        private class ActiveTroopVisual
        {
            public SplineContainer SplineContainer;
            public LineRenderer LineRenderer;
            public Vector3[] SampledPositions;
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

            using NativeArray<Entity> selectedTroopEntities = selectedTroopQuery.ToEntityArray(Allocator.Temp);
            using NativeArray<TroopAABB> selectedTroopAABB = selectedTroopQuery.ToComponentDataArray<TroopAABB>(Allocator.Temp);
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

            if (selectedTroopEntities.Length > 0)
            {
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
                        float3 p = troopHullPoints[j].Position;
                        hull[j] = new float2(p.x, p.z);
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
                        if (math.abs(appliedOffset) > maxOffset) appliedOffset = math.sign(appliedOffset) * maxOffset;

                        float2 padded = curr + vnormal * appliedOffset;

                        knotArray[j] = new float3(padded.x, height, padded.y);
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

                        // EvaluatePosition returns local position; container.TransformPoint -> world
                        float3 localPos = activeTroopVisual.SplineContainer.Spline.EvaluatePosition(t);
                        Vector3 worldPos = activeTroopVisual.SplineContainer.transform.TransformPoint(new Vector3(localPos.x, localPos.y, localPos.z));

                        positions[j] = worldPos;
                    }

                    // assign to LineRenderer
                    activeTroopVisual.LineRenderer.positionCount = positions.Length;
                    activeTroopVisual.LineRenderer.SetPositions(positions);

                    activeTroopVisual.SampledPositions = positions;

                    Color teamColor = selectedTroopTeam.Color == TeamColor.Red ? redTeamColor : blueTeamColor;

                    activeTroopVisual.LineRenderer.startColor = teamColor;
                    activeTroopVisual.LineRenderer.endColor = teamColor;
                }
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
                
                // todo : calc dragStartPosition for cull drag line
                
                Transform dragLineTransform = _dragLine.transform;

                dragLineTransform.position = dragStartPosition;
                
                Vector3 dragLineScale = dragLineTransform.localScale;
                float dragHeadHeight = Mathf.Sqrt(3f) * 0.5f * dragHeadSideLength;
                dragLineScale.z = Vector3.Distance(dragStartPosition, draggingPosition) - dragHeadHeight * 0.9f;
                dragLineTransform.localScale = dragLineScale;
                
                Vector3 dragDirection = Vector3.Normalize(draggingPosition - dragStartPosition);
                dragLineTransform.forward = dragDirection;

                _dragLineMaterial.color = currentActiveTroopVisual.LineRenderer.startColor;

                _dragLine.SetActive(true);

                Transform dragHeadTransform = _dragHead.transform;
                dragHeadTransform.position = draggingPosition - dragDirection * dragHeadHeight;
                dragHeadTransform.forward = dragDirection;

                _dragHeadMaterial.color = currentActiveTroopVisual.LineRenderer.startColor;

                _dragHead.SetActive(true);
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
    }
}