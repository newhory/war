using Unity.Entities;
using UnityEngine;


namespace War.Dots.Component.Authoring
{
    public class SoldierAnimationAuthoring : MonoBehaviour
    {
        [SerializeField] private SoldierAnimation.State defaultAnimationState = SoldierAnimation.State.None;


        private class Baker : Baker<SoldierAnimationAuthoring>
        {
            public override void Bake(SoldierAnimationAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(
                    entity,
                    new SoldierAnimation
                    {
                        Current = SoldierAnimation.State.None,
                        Next = authoring.defaultAnimationState
                    });
            }
        }
    }
}