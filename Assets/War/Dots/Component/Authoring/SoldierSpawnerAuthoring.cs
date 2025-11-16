using Unity.Entities;
using UnityEngine;


namespace War.Dots.Component.Authoring
{
    public class SoldierSpawnerAuthoring : MonoBehaviour
    {
        [SerializeField] private GameObject soldierPrefab;

        [SerializeField] private SoldierData spear;
        [SerializeField] private SoldierData archer;
        [SerializeField] private SoldierData shield;
        [SerializeField] private SoldierData cavalry;


        private class Baker : Baker<SoldierSpawnerAuthoring>
        {
            public override void Bake(SoldierSpawnerAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.None);

                AddComponent(
                    entity,
                    new SoldierSpawner
                    {
                        SoldierProtoType = GetEntity(authoring.soldierPrefab, TransformUsageFlags.Dynamic),

                        SpearSoldierData = authoring.spear,
                        ArcherSoldierData = authoring.archer,
                        ShieldSoldierData = authoring.shield,
                        CavalrySoldierData = authoring.cavalry
                    });

                AddBuffer<SpawnSoldierData>(entity);
            }
        }
    }
}