using Unity.Entities;
using UnityEngine;


namespace War.Dots.Component.Authoring
{
    public class AttackRangeAuthoring : MonoBehaviour
    {
        [SerializeField] private float value;


        private class Baker : Baker<AttackRangeAuthoring>
        {
            public override void Bake(AttackRangeAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(entity, new AttackRange { Value = authoring.value });
            }
        }
    }
}