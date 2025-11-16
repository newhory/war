using Unity.Entities;
using UnityEngine;


namespace War.Dots.Component.Authoring
{
    public class AttackPowerAuthoring : MonoBehaviour
    {
        [SerializeField] private float value;
        

        private class AttackPowerBaker : Baker<AttackPowerAuthoring>
        {
            public override void Bake(AttackPowerAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(entity, new AttackPower { Value = authoring.value });
            }
        }
    }
}