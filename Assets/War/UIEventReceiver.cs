using UnityEngine;
using UnityEngine.UI;


namespace War
{
    using Dots.Component;
    using Dots.Component.ComponentSystem;


    public class UIEventReceiver : MonoBehaviour
    {
    #region serialized fields

        [SerializeField] private Transform blueRespawn;
        [SerializeField] private Transform redRespawn;

        [SerializeField] private Button startBattleButton;
        [SerializeField] private Button resetButton;

        [SerializeField] private GameObject blueTeamButtonRoot;
        [SerializeField] private GameObject redTeamButtonRoot;

        [SerializeField] private ToggleGroup spawnToggleGroup;

        [SerializeField] private TMPro.TextMeshProUGUI soldierCountText;

    #endregion


        private void Start()
        {
            startBattleButton.gameObject.SetActive(true);
            resetButton.gameObject.SetActive(false);
        }

        private void Update() => soldierCountText.text = PlayerInputSystem.SoldierCount.ToString();

        public void SetRespawnBlueSpear(bool isToggle)
        {
            if (!isToggle)
            {
                SpawnInputSystem.UnsetCurrentSpawnSoldierData();
                return;
            }

            SpawnInputSystem.SetCurrentSpawnSoldierData(
                new SpawnSoldierData
                {
                    TeamColor = TeamColor.Blue,
                    SoldierType = SoldierType.Spear,
                    Position = blueRespawn.position,
                    Rotation = blueRespawn.rotation,
                    Count = Setting.Instance.spawnSoldierCount
                });
        }

        public void SetRespawnBlueArcher(bool isToggle)
        {
            if (!isToggle)
            {
                SpawnInputSystem.UnsetCurrentSpawnSoldierData();
                return;
            }

            SpawnInputSystem.SetCurrentSpawnSoldierData(
                new SpawnSoldierData
                {
                    TeamColor = TeamColor.Blue,
                    SoldierType = SoldierType.Archer,
                    Position = blueRespawn.position,
                    Rotation = blueRespawn.rotation,
                    Count = Setting.Instance.spawnSoldierCount
                });
        }

        public void SetRespawnBlueShield(bool isToggle)
        {
            if (!isToggle)
            {
                SpawnInputSystem.UnsetCurrentSpawnSoldierData();
                return;
            }

            SpawnInputSystem.SetCurrentSpawnSoldierData(
                new SpawnSoldierData
                {
                    TeamColor = TeamColor.Blue,
                    SoldierType = SoldierType.Shield,
                    Position = blueRespawn.position,
                    Rotation = blueRespawn.rotation,
                    Count = Setting.Instance.spawnSoldierCount
                });
        }

        public void SetRespawnBlueCavalry(bool isToggle)
        {
            if (!isToggle)
            {
                SpawnInputSystem.UnsetCurrentSpawnSoldierData();
                return;
            }

            SpawnInputSystem.SetCurrentSpawnSoldierData(
                new SpawnSoldierData
                {
                    TeamColor = TeamColor.Blue,
                    SoldierType = SoldierType.Cavalry,
                    Position = blueRespawn.position,
                    Rotation = blueRespawn.rotation,
                    Count = Setting.Instance.spawnSoldierCount
                });
        }

        public void SetRespawnRedSpear(bool isToggle)
        {
            if (!isToggle)
            {
                SpawnInputSystem.UnsetCurrentSpawnSoldierData();
                return;
            }

            SpawnInputSystem.SetCurrentSpawnSoldierData(
                new SpawnSoldierData
                {
                    TeamColor = TeamColor.Red,
                    SoldierType = SoldierType.Spear,
                    Position = redRespawn.position,
                    Rotation = redRespawn.rotation,
                    Count = Setting.Instance.spawnSoldierCount
                });
        }

        public void SetRespawnRedArcher(bool isToggle)
        {
            if (!isToggle)
            {
                SpawnInputSystem.UnsetCurrentSpawnSoldierData();
                return;
            }

            SpawnInputSystem.SetCurrentSpawnSoldierData(
                new SpawnSoldierData
                {
                    TeamColor = TeamColor.Red,
                    SoldierType = SoldierType.Archer,
                    Position = redRespawn.position,
                    Rotation = redRespawn.rotation,
                    Count = Setting.Instance.spawnSoldierCount
                });
        }

        public void SetRespawnRedShield(bool isToggle)
        {
            if (!isToggle)
            {
                SpawnInputSystem.UnsetCurrentSpawnSoldierData();
                return;
            }

            SpawnInputSystem.SetCurrentSpawnSoldierData(
                new SpawnSoldierData
                {
                    TeamColor = TeamColor.Red,
                    SoldierType = SoldierType.Shield,
                    Position = redRespawn.position,
                    Rotation = redRespawn.rotation,
                    Count = Setting.Instance.spawnSoldierCount
                });
        }

        public void SetRespawnRedCavalry(bool isToggle)
        {
            if (!isToggle)
            {
                SpawnInputSystem.UnsetCurrentSpawnSoldierData();
                return;
            }

            SpawnInputSystem.SetCurrentSpawnSoldierData(
                new SpawnSoldierData
                {
                    TeamColor = TeamColor.Red,
                    SoldierType = SoldierType.Cavalry,
                    Position = redRespawn.position,
                    Rotation = redRespawn.rotation,
                    Count = Setting.Instance.spawnSoldierCount
                });
        }

        public void StartBattle()
        {
            PlayerInputSystem.StartBattle();

            startBattleButton.gameObject.SetActive(false);
            resetButton.gameObject.SetActive(true);

            spawnToggleGroup.SetAllTogglesOff();

            blueTeamButtonRoot.SetActive(false);
            redTeamButtonRoot.SetActive(false);
        }

        public void ResetBattle()
        {
            PlayerInputSystem.ResetBattle();

            startBattleButton.gameObject.SetActive(true);
            resetButton.gameObject.SetActive(false);

            blueTeamButtonRoot.SetActive(true);
            redTeamButtonRoot.SetActive(true);

            spawnToggleGroup.SetAllTogglesOff();
        }
    }
}