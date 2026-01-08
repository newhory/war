using Unity.Entities;
using UnityEngine;

namespace War.Game.Authoring
{
    public class MoveSpeedAuthoring : MonoBehaviour
    {
        [SerializeField] private float maxSpeed;


        private class Baker : Baker<MoveSpeedAuthoring>
        {
            public override void Bake(MoveSpeedAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(entity, new MoveSpeed { Max = authoring.maxSpeed, CurrentMax = authoring.maxSpeed });
            }
        }
    }
}