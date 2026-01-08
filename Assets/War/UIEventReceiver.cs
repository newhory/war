using UnityEngine;
using UnityEngine.UI;
using War.Game;


namespace War
{
    using Game.Systems;


    public class UIEventReceiver : MonoBehaviour
    {
    #region serialized fields

        [SerializeField] private Button startBattleButton;
        [SerializeField] private Button resetButton;

        [SerializeField] private GameObject blueTeamButtonRoot;
        [SerializeField] private GameObject redTeamButtonRoot;

        [SerializeField] private ToggleGroup spawnToggleGroup;

        [SerializeField] private TMPro.TextMeshProUGUI soldierCountText;

    #endregion

        private static void SetRespawn(bool isToggle, TeamColor teamColor, SoldierType soldierType)
        {
            if (!isToggle)
            {
                PointInput.IsSetCurrentSpawnSoldierData = false;
                return;
            }

            PointInput.IsSetCurrentSpawnSoldierData = true;
            PointInput.CurrentSpawnSoldierData = new SpawnSoldierData
            {
                TeamColor = teamColor,
                SoldierType = soldierType,
                Count = Setting.Instance.spawnSoldierCount
            };
        }


        private void Start()
        {
            startBattleButton.gameObject.SetActive(true);
            resetButton.gameObject.SetActive(false);
        }

        private void Update() => soldierCountText.text = Game.Systems.PlayerInputSystem.SoldierCount.ToString();

        public void SetRespawnBlueSpear(bool isToggle) => SetRespawn(isToggle, TeamColor.Blue, SoldierType.Spear);
        public void SetRespawnBlueArcher(bool isToggle) => SetRespawn(isToggle, TeamColor.Blue, SoldierType.Archer);
        public void SetRespawnBlueShield(bool isToggle) => SetRespawn(isToggle, TeamColor.Blue, SoldierType.Shield);
        public void SetRespawnBlueCavalry(bool isToggle) => SetRespawn(isToggle, TeamColor.Blue, SoldierType.Cavalry);

        public void SetRespawnRedSpear(bool isToggle) => SetRespawn(isToggle, TeamColor.Red, SoldierType.Spear);
        public void SetRespawnRedArcher(bool isToggle) => SetRespawn(isToggle, TeamColor.Red, SoldierType.Archer);
        public void SetRespawnRedShield(bool isToggle) => SetRespawn(isToggle, TeamColor.Red, SoldierType.Shield);
        public void SetRespawnRedCavalry(bool isToggle) => SetRespawn(isToggle, TeamColor.Red, SoldierType.Cavalry);

        public void StartBattle()
        {
            Game.Systems.PlayerInputSystem.StartBattle();

            startBattleButton.gameObject.SetActive(false);
            resetButton.gameObject.SetActive(true);

            spawnToggleGroup.SetAllTogglesOff();

            blueTeamButtonRoot.SetActive(false);
            redTeamButtonRoot.SetActive(false);
        }

        public void ResetBattle()
        {
            Game.Systems.PlayerInputSystem.ResetBattle();

            startBattleButton.gameObject.SetActive(true);
            resetButton.gameObject.SetActive(false);

            blueTeamButtonRoot.SetActive(true);
            redTeamButtonRoot.SetActive(true);

            spawnToggleGroup.SetAllTogglesOff();
        }
    }
}