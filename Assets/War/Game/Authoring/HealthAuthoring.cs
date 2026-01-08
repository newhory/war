using Unity.Entities;
using UnityEngine;

namespace War.Game.Authoring
{
    public class HealthAuthoring : MonoBehaviour
    {
        [SerializeField] private float value;


        private class Baker : Baker<HealthAuthoring>
        {
            public override void Bake(HealthAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(entity, new Health { Value = authoring.value });
            }
        }
    }
}