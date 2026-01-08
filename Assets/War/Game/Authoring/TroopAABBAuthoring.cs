using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace War.Game.Authoring
{
    public class TroopAABBAuthoring : MonoBehaviour
    {
        [SerializeField] private float padding = 0.2f;


        private class Baker : Baker<TroopAABBAuthoring>
        {
            public override void Bake(TroopAABBAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(entity, new TroopAABB { Min = float2.zero, Max = float2.zero, Padding = authoring.padding });
            }
        }
    }
}