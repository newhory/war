using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;


namespace War
{
    using Dots.Component;
    using Dots.Component.ComponentSystem;


    public class PlayerInput : MonoBehaviour
    {
    #region serialized fields

        [SerializeField] private Transform blueRespawn;
        [SerializeField] private Transform redRespawn;

        [SerializeField] private Button startBattleButton;
        [SerializeField] private Button resetButton;

        [SerializeField] private GameObject blueTeamButtonRoot;
        [SerializeField] private GameObject redTeamButtonRoot;

    #endregion


        private void Start()
        {
            startBattleButton.gameObject.SetActive(true);
            resetButton.gameObject.SetActive(false);
        }

        public void RespawnBlueSpear()
        {
            SpawnSoldier(
                new SpawnSoldierData
                {
                    TroopId = 1,
                    TeamColor = TeamColor.Blue,
                    SoldierType = SoldierType.Spear,
                    Position = blueRespawn.position,
                    Rotation = blueRespawn.rotation,
                    Count = 10
                });
        }

        public void RespawnBlueArcher()
        {
            SpawnSoldier(
                new SpawnSoldierData
                {
                    TroopId = 1,
                    TeamColor = TeamColor.Blue,
                    SoldierType = SoldierType.Archer,
                    Position = blueRespawn.position,
                    Rotation = blueRespawn.rotation,
                    Count = 10
                });
        }

        public void RespawnBlueShield()
        {
            SpawnSoldier(
                new SpawnSoldierData
                {
                    TroopId = 1,
                    TeamColor = TeamColor.Blue,
                    SoldierType = SoldierType.Shield,
                    Position = blueRespawn.position,
                    Rotation = blueRespawn.rotation,
                    Count = 10
                });
        }

        public void RespawnBlueCavalry()
        {
            SpawnSoldier(
                new SpawnSoldierData
                {
                    TroopId = 1,
                    TeamColor = TeamColor.Blue,
                    SoldierType = SoldierType.Cavalry,
                    Position = blueRespawn.position,
                    Rotation = blueRespawn.rotation,
                    Count = 10
                });
        }

        public void RespawnRedSpear()
        {
            SpawnSoldier(
                new SpawnSoldierData
                {
                    TroopId = 2,
                    TeamColor = TeamColor.Red,
                    SoldierType = SoldierType.Spear,
                    Position = redRespawn.position,
                    Rotation = redRespawn.rotation,
                    Count = 10
                });
        }

        public void RespawnRedArcher()
        {
            SpawnSoldier(
                new SpawnSoldierData
                {
                    TroopId = 2,
                    TeamColor = TeamColor.Red,
                    SoldierType = SoldierType.Archer,
                    Position = redRespawn.position,
                    Rotation = redRespawn.rotation,
                    Count = 10
                });
        }

        public void RespawnRedShield()
        {
            SpawnSoldier(
                new SpawnSoldierData
                {
                    TroopId = 2,
                    TeamColor = TeamColor.Red,
                    SoldierType = SoldierType.Shield,
                    Position = redRespawn.position,
                    Rotation = redRespawn.rotation,
                    Count = 10
                });
        }

        public void RespawnRedCavalry()
        {
            SpawnSoldier(
                new SpawnSoldierData
                {
                    TroopId = 2,
                    TeamColor = TeamColor.Red,
                    SoldierType = SoldierType.Cavalry,
                    Position = redRespawn.position,
                    Rotation = redRespawn.rotation,
                    Count = 10
                });
        }

        public void StartBattle()
        {
            PlayerInputSystem.StartBattle();

            blueTeamButtonRoot.SetActive(false);
            redTeamButtonRoot.SetActive(false);

            startBattleButton.gameObject.SetActive(false);
            resetButton.gameObject.SetActive(true);
        }

        public void ResetBattle()
        {
            PlayerInputSystem.ResetBattle();

            blueTeamButtonRoot.SetActive(true);
            redTeamButtonRoot.SetActive(true);

            startBattleButton.gameObject.SetActive(true);
            resetButton.gameObject.SetActive(false);
        }

        private static void SpawnSoldier(SpawnSoldierData spawn)
        {
            SpawnSoldierSystem.SpawnSoldier(World.DefaultGameObjectInjectionWorld.EntityManager, spawn);
        }
    }
}