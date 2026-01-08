using Unity.Entities;
using UnityEngine;

namespace War.Game.Authoring
{
    public class TroopAuthoring : MonoBehaviour
    {
        private class Baker : Baker<TroopAuthoring>
        {
            public override void Bake(TroopAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(entity, new TroopEntity { Entity = entity });
            }
        }
    }
}