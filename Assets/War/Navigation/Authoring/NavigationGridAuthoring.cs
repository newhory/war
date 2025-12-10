using Unity.Entities;
using UnityEngine;


namespace War.Navigation.Authoring
{
    public class NavigationGridAuthoring : MonoBehaviour
    {
        [Header("Neighbor Setting")]
        [SerializeField] private float neighborHashCellSize = 1.2f;
        [SerializeField] private int maxMaxNeighborCount = 12;

        [Header("Flow Field Setting")]
        [SerializeField] private float flowFieldCellSize = 1.2f;
        [SerializeField] private int flowFieldMinGridCellCount = 8;
        [SerializeField] [Range(0f, 1f)] private float flowFieldWalkableToleranceForDivide = 0.9f;
        [SerializeField] [Range(0f, 1f)] private float flowFieldValidWalkableRatio = 0.4f;


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

                        FlowFieldCellSize = authoring.flowFieldCellSize,
                        FlowFieldMinGridCellCount = authoring.flowFieldMinGridCellCount,
                        FlowFieldWalkableToleranceForDivide = authoring.flowFieldWalkableToleranceForDivide,
                        FlowFieldValidWalkableRatio = authoring.flowFieldValidWalkableRatio,
                    });

                AddComponent<UpdateNavigationGrid>(entity);
            }
        }
    }
}