using System;
using UnityEngine;


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

        [Header("Soldier")]
        public SoldierPrefabs spear;
        public SoldierPrefabs archer;
        public SoldierPrefabs shield;
        public SoldierPrefabs cavalry;
        public int spawnSoldierCount = 16;
        public int troopHorizonSoldierCount = 4;

        [Header("Arrow")]
        [Tooltip("For Hybrid Render")]
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
    }
}