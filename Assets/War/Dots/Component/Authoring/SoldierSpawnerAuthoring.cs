using Unity.Entities;
using UnityEngine;


namespace War.Dots.Component.Authoring
{
    public class SoldierSpawnerAuthoring : MonoBehaviour
    {
        [SerializeField] private GameObject soldierPrefab;
        [SerializeField] private GameObject troopPrefab;

        [Header("Spear")] [SerializeField] private GameObject blueTeamSpearPrefab;
        [SerializeField] private GameObject redTeamSpearPrefab;
        [SerializeField] private SoldierData spear;

        [Header("Archer")] [SerializeField] private GameObject blueTeamArcherPrefab;
        [SerializeField] private GameObject redTeamArcherPrefab;
        [SerializeField] private SoldierData archer;

        [Header("Shield")] [SerializeField] private GameObject blueTeamShieldPrefab;
        [SerializeField] private GameObject redTeamShieldPrefab;
        [SerializeField] private SoldierData shield;

        [Header("Cavalry")] [SerializeField] private GameObject blueTeamCavalryPrefab;
        [SerializeField] private GameObject redTeamCavalryPrefab;
        [SerializeField] private SoldierData cavalry;


        private class Baker : Baker<SoldierSpawnerAuthoring>
        {
            public override void Bake(SoldierSpawnerAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.None);

                SoldierSpawner soldierSpawner = new()
                {
                    SoldierProtoType = GetEntity(authoring.soldierPrefab, TransformUsageFlags.Dynamic),
                    TroopProtoType = GetEntity(authoring.troopPrefab, TransformUsageFlags.Dynamic),

                    SpearSoldierData = authoring.spear,
                    ArcherSoldierData = authoring.archer,
                    ShieldSoldierData = authoring.shield,
                    CavalrySoldierData = authoring.cavalry
                };

                soldierSpawner.SpearSoldierData.BlueTeamProtoType = GetEntity(authoring.blueTeamSpearPrefab, TransformUsageFlags.Dynamic);
                soldierSpawner.SpearSoldierData.RedTeamProtoType = GetEntity(authoring.redTeamSpearPrefab, TransformUsageFlags.Dynamic);

                soldierSpawner.ArcherSoldierData.BlueTeamProtoType = GetEntity(authoring.blueTeamArcherPrefab, TransformUsageFlags.Dynamic);
                soldierSpawner.ArcherSoldierData.RedTeamProtoType = GetEntity(authoring.redTeamArcherPrefab, TransformUsageFlags.Dynamic);

                soldierSpawner.ShieldSoldierData.BlueTeamProtoType = GetEntity(authoring.blueTeamShieldPrefab, TransformUsageFlags.Dynamic);
                soldierSpawner.ShieldSoldierData.RedTeamProtoType = GetEntity(authoring.redTeamShieldPrefab, TransformUsageFlags.Dynamic);

                soldierSpawner.CavalrySoldierData.BlueTeamProtoType = GetEntity(authoring.blueTeamCavalryPrefab, TransformUsageFlags.Dynamic);
                soldierSpawner.CavalrySoldierData.RedTeamProtoType = GetEntity(authoring.redTeamCavalryPrefab, TransformUsageFlags.Dynamic);

                AddComponent(entity, soldierSpawner);

                AddBuffer<SpawnSoldierData>(entity);
            }
        }
    }
}