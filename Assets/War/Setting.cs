using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine.InputSystem;
#endif

namespace War
{
    using Dots.Component;


    public class Setting : SingletonSceneObject<Setting>
    {
    #region serialized fields

        [Serializable]
        public struct SoldierPrefabs
        {
            public GameObject blueTeamPrefab;
            public GameObject redTeamPrefab;
        }

        [Header("Soldier")] public SoldierPrefabs spear;
        public SoldierPrefabs archer;
        public SoldierPrefabs shield;
        public SoldierPrefabs cavalry;
        public int spawnSoldierCount = 16;
        public int troopHorizonSoldierCount = 4;

        [Header("Arrow")] [Tooltip("For Hybrid Render")]
        public GameObject arrowRenderMeshPrefab;

    #endregion


        public static int WorldLayer { get; private set; }
        public static int RedTeamLayer { get; private set; }
        public static int BlueTeamLayer { get; private set; }
        public static int ArrowLayer { get; private set; }


        public static int GetMyTeamLayer(TeamColor teamColor) =>
            teamColor switch
            {
                TeamColor.Red => RedTeamLayer,
                TeamColor.Blue => BlueTeamLayer,
                _ => throw new Exception($"Invalid team color: {teamColor}")
            };

        public static int GetEnemyLayer(TeamColor teamColor) =>
            teamColor switch
            {
                TeamColor.Red => BlueTeamLayer,
                TeamColor.Blue => RedTeamLayer,
                _ => throw new Exception($"Invalid team color: {teamColor}")
            };

        protected override void Init()
        {
            WorldLayer = LayerMask.NameToLayer("World");
            RedTeamLayer = LayerMask.NameToLayer("Red Team");
            BlueTeamLayer = LayerMask.NameToLayer("Blue Team");
            ArrowLayer = LayerMask.NameToLayer("Arrow");
        }

#if UNITY_EDITOR
        private int _frameCount;
        private int _selectedFlowFieldTargetIndex;

        private void OnDrawGizmos()
        {
            if (!EditorApplication.isPlaying)
            {
                return;
            }

            if (_frameCount < 10)
            {
                ++_frameCount;
                return;
            }

            Color gizmosColor = Gizmos.color;

            int2 gridSize = Navigation.FlowFieldProvider.GridSize;
            float cellSize = Navigation.FlowFieldProvider.CellSize;
            float3 minWorldPositionInGrid = Navigation.FlowFieldProvider.MinWorldPositionInGrid;
            int navMeshMaskCount = Navigation.FlowFieldProvider.NavMeshMask.Length;
            for (int index = 0; index < navMeshMaskCount; index++)
            {
                if (Navigation.FlowFieldProvider.NavMeshMask[index] == 0)
                {
                    continue;
                }

                Vector3 size = new(cellSize, 0f, cellSize);

                int2 cell = new(index % gridSize.x, index / gridSize.x);

                float3 center = new(
                    minWorldPositionInGrid.x + cell.x * cellSize + cellSize * 0.5f,
                    size.y * 0.5f,
                    minWorldPositionInGrid.z + cell.y * cellSize + cellSize * 0.5f
                );

                Gizmos.color = Navigation.FlowFieldProvider.NavMeshMask[index] != 0 ? Color.red : Color.blue;
                Gizmos.DrawCube(center, size);
            }

            var flowFieldTargets = Navigation.FlowFieldProvider.FlowFieldFlowBlobAssetReference;

            for (int index = 0; index < flowFieldTargets.Value.Targets.Length; index++)
            {
                ref var flowFieldTarget = ref flowFieldTargets.Value.Targets[index];

                var size = flowFieldTarget.AreaBounds.Size;
                size.y = 30f;
                var center = flowFieldTarget.AreaBounds.Center;
                center.y = size.y * 0.5f;

                Gizmos.color = Color.blue;
                Gizmos.DrawWireCube(center, size);
                Gizmos.color = Color.chocolate;
                Gizmos.matrix = Matrix4x4.TRS(center, Quaternion.identity, Vector3.one);
                Handles.Label(
                    center,
                    $"<color=#{ColorUtility.ToHtmlStringRGBA(Gizmos.color)}><b>{flowFieldTarget.FlowId}</b></color>",
                    new GUIStyle
                    {
                        richText = true,
                        alignment = TextAnchor.MiddleCenter
                    });
                Gizmos.matrix = Matrix4x4.identity;
                //Gizmos.color = (index % 5) switch
                //{
                //    0 => Color.burlywood,
                //    1 => Color.yellow,
                //    2 => Color.cornflowerBlue,
                //    3 => Color.cyan,
                //    4 => Color.green,
                //    _ => Color.red
                //};
                //Gizmos.DrawCube(center, size);
            }
            
            if (Keyboard.current.aKey.wasReleasedThisFrame)
            {
                _selectedFlowFieldTargetIndex = Mathf.Min(++_selectedFlowFieldTargetIndex, flowFieldTargets.Value.Targets.Length - 1);
            }

            if (Keyboard.current.sKey.wasReleasedThisFrame)
            {
                _selectedFlowFieldTargetIndex = Mathf.Max(--_selectedFlowFieldTargetIndex, 0);
            }

            if (Keyboard.current.dKey.wasReleasedThisFrame)
            {
                _selectedFlowFieldTargetIndex = 0;
            }

            if (Keyboard.current.fKey.wasReleasedThisFrame)
            {
                _selectedFlowFieldTargetIndex = flowFieldTargets.Value.Targets.Length - 1;
            }

            if (_selectedFlowFieldTargetIndex >= 0 && _selectedFlowFieldTargetIndex < flowFieldTargets.Value.Targets.Length)
            {
                ref var selectedFlowFieldTarget = ref flowFieldTargets.Value.Targets[_selectedFlowFieldTargetIndex];
                
                Gizmos.color = Color.blue;
                Gizmos.DrawCube(selectedFlowFieldTarget.Position, selectedFlowFieldTarget.AreaBounds.Size);
                
                Gizmos.color = Color.crimson;

                for (int y = 0; y < gridSize.y; y++)
                {
                    for (int x = 0; x < gridSize.x; x++)
                    {
                        int index = y * gridSize.x + x;
                        Vector2 dir = selectedFlowFieldTarget.DirectionField[index];
                        if (dir.sqrMagnitude < 1e-6f) continue; // 0벡터는 스킵

                        // 셀 중심 위치
                        Vector3 cellCenter = minWorldPositionInGrid + new float3(x + 0.5f, 0, y + 0.5f) * cellSize;

                        // 방향 벡터 그리기
                        Vector3 end = cellCenter + new Vector3(dir.x, 0, dir.y) * (cellSize * 0.4f);
                        Gizmos.DrawLine(cellCenter, end);
                        Gizmos.DrawSphere(end, 0.05f); // 화살표 끝 표시
                    }
                }

                
            }

            Gizmos.color = gizmosColor;
        }
#endif
    }
}