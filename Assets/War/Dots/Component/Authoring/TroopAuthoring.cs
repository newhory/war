using Unity.Entities;
using UnityEngine;


namespace War.Dots.Component.Authoring
{
    public class TroopAuthoring : MonoBehaviour
    {
        [SerializeField] private int id;
        
        
        private class  Baker : Baker<TroopAuthoring>
        {
            public override void Bake(TroopAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);
                
                AddSharedComponent(entity, new Troop { Id = authoring.id });
            }
        }
    }
}