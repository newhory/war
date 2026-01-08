using Unity.Entities;
using UnityEngine;

namespace War.Game.Authoring
{
    public class SearchTargetRangeAuthoring : MonoBehaviour
    {
        [SerializeField] private float value;


        private class Baker : Baker<SearchTargetRangeAuthoring>
        {
            public override void Bake(SearchTargetRangeAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(entity, new SearchTargetRange { Value = authoring.value });
            }
        }
    }
}