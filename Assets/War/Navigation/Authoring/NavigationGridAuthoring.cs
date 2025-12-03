using Unity.Entities;
using UnityEngine;


namespace War.Navigation.Authoring
{
    public class NavigationGridAuthoring : MonoBehaviour
    {
        [SerializeField] private float neighborHashCellSize = 1.2f;
        [SerializeField] private int maxMaxNeighborCount = 12;
        [SerializeField] private float flowFieldCellSize = 1.2f;


        private class NavigationGridAuthoringBaker : Baker<NavigationGridAuthoring>
        {
            public override void Bake(NavigationGridAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.None);

                AddComponent(
                    entity,
                    new NavigationGrid
                    {
                        NeighborHashCellSize = authoring.neighborHashCellSize,
                        MaxMaxNeighborCount = authoring.maxMaxNeighborCount,
                        FlowFieldCellSize = authoring.flowFieldCellSize
                    });

                AddComponent<UpdateNavigationGrid>(entity);
            }
        }
    }
}