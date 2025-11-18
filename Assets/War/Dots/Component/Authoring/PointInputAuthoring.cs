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

                AddComponent(entity, new OnPressStart());
                AddComponent(entity, new OnPressEnd());

                AddComponent(entity, new OnDragStart());
                AddComponent(entity, new OnDragging());
                AddComponent(entity, new OnDragEnd());
                AddComponent(entity, new DragStartPosition());

                SetComponentEnabled<OnPressStart>(entity, false);
                SetComponentEnabled<OnPressEnd>(entity, false);
                SetComponentEnabled<DragStartPosition>(entity, false);
            }
        }
    }
}