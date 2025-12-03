using Unity.Entities;
using UnityEngine;


namespace War.Dots.Component.Authoring
{
    public class NavigationAPIAuthoring : MonoBehaviour
    {
        [SerializeField] private NavigationType navigationType = NavigationType.NavMesh;


        private class NavigationAPIAuthoringBaker : Baker<NavigationAPIAuthoring>
        {
            public override void Bake(NavigationAPIAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);

                AddSharedComponent(entity, new NavigationAPI { Type = authoring.navigationType });
            }
        }
    }
}