using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine.InputSystem;
#endif

namespace War
{
    using Dots.Component;
#if UNITY_EDITOR
    using Navigation;
#endif

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

            int2 gridSize = FlowFieldProvider.GridSize;
            float cellSize = FlowFieldProvider.CellSize;
            float3 minWorldPositionInGrid = FlowFieldProvider.MinWorldPositionInGrid;
            int navMeshMaskCount = FlowFieldProvider.NavMeshMask.Length;
            for (int index = 0; index < navMeshMaskCount; index++)
            {
                if (FlowFieldProvider.NavMeshMask[index] == 0)
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

                Gizmos.color = FlowFieldProvider.NavMeshMask[index] != 0 ? Color.red : Color.blue;
                Gizmos.DrawCube(center, size);
            }

            BlobAssetReference<FlowFieldBlobRoot> fieldFlowBlobAssetReference = FlowFieldProvider.FlowFieldFlowBlobAssetReference;

            for (int index = 0; index < fieldFlowBlobAssetReference.Value.FlowFieldTargets.Length; index++)
            {
                ref FlowFieldTarget flowFieldTarget = ref fieldFlowBlobAssetReference.Value.FlowFieldTargets[index];

                float3 size = flowFieldTarget.AreaBounds.Size;
                size.y = 30f;
                float3 center = flowFieldTarget.AreaBounds.Center;
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
            }

            if (Keyboard.current.aKey.wasReleasedThisFrame)
            {
                _selectedFlowFieldTargetIndex = Mathf.Min(++_selectedFlowFieldTargetIndex, fieldFlowBlobAssetReference.Value.FlowFieldTargets.Length - 1);
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
                _selectedFlowFieldTargetIndex = fieldFlowBlobAssetReference.Value.FlowFieldTargets.Length - 1;
            }

            if (_selectedFlowFieldTargetIndex >= 0 && _selectedFlowFieldTargetIndex < fieldFlowBlobAssetReference.Value.FlowFieldTargets.Length)
            {
                ref FlowFieldTarget selectedFlowFieldTarget = ref fieldFlowBlobAssetReference.Value.FlowFieldTargets[_selectedFlowFieldTargetIndex];

                Gizmos.color = Color.blue;
                Gizmos.DrawCube(selectedFlowFieldTarget.Position, selectedFlowFieldTarget.AreaBounds.Size);

                Gizmos.color = Color.crimson;

                for (int y = 0; y < gridSize.y; y++)
                {
                    for (int x = 0; x < gridSize.x; x++)
                    {
                        int index = y * gridSize.x + x;
                        Vector2 dir = selectedFlowFieldTarget.DirectionField[index];
                        if (dir.sqrMagnitude < 1e-6f)
                        {
                            continue;
                        }

                        Vector3 cellCenter = minWorldPositionInGrid + new float3(x + 0.5f, 0, y + 0.5f) * cellSize;

                        Vector3 end = cellCenter + new Vector3(dir.x, 0, dir.y) * (cellSize * 0.4f);
                        Gizmos.DrawLine(cellCenter, end);
                        Gizmos.DrawSphere(end, 0.05f);
                    }
                }
            }

            Gizmos.color = gizmosColor;
        }
#endif
    }
}