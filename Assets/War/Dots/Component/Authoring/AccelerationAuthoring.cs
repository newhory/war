using Unity.Entities;
using UnityEngine;


namespace War.Dots.Component.Authoring
{
    public class AccelerationAuthoring : MonoBehaviour
    {
        [SerializeField] private float maxAcceleration = 1f;


        private class Baker : Baker<AccelerationAuthoring>
        {
            public override void Bake(AccelerationAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(entity, new Acceleration { Max = authoring.maxAcceleration });
            }
        }
    }
}