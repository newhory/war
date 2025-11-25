using Unity.Entities;
using UnityEngine;


namespace War.Dots.Component.Authoring
{
    public class PointInputAuthoring : MonoBehaviour
    {
        [SerializeField] private float dragOffset = 5f;


        private class Baker : Baker<PointInputAuthoring>
        {
            public override void Bake(PointInputAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.None);

                AddComponent(entity, new PointInput { DragOffset = authoring.dragOffset });

                AddComponent(entity, new OnPointerPressStart());
                AddComponent(entity, new OnPointerPressEnd());
                AddComponent(entity, new OnPointerMove());

                AddComponent(entity, new OnPointerDragStart());
                AddComponent(entity, new OnPointerDragging());
                AddComponent(entity, new OnPointerDragEnd());
                
                AddComponent(entity, new DragStartWorldPosition());
                AddComponent(entity, new DraggingWorldPosition());
                AddComponent(entity, new DragEndWorldPosition());

                SetComponentEnabled<OnPointerPressStart>(entity, false);
                SetComponentEnabled<OnPointerPressEnd>(entity, false);
                
                SetComponentEnabled<DragStartWorldPosition>(entity, false);
                SetComponentEnabled<DraggingWorldPosition>(entity, false);
                SetComponentEnabled<DragEndWorldPosition>(entity, false);
                
                AddComponent(entity, new ComponentSystem.SpawnInput());
                AddComponent(entity, new ComponentSystem.SetSpawnData());
                AddComponent(entity, new ComponentSystem.UnsetSpawnData());
                AddComponent(entity, new ComponentSystem.SpawnDecalPosition());
                
                SetComponentEnabled<ComponentSystem.SetSpawnData>(entity, false);
                SetComponentEnabled<ComponentSystem.UnsetSpawnData>(entity, false);
            }
        }
    }
}