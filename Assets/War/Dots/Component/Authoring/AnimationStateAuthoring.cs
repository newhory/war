using Unity.Entities;
using UnityEngine;


namespace War.Dots.Component.Authoring
{
    public class AnimationStateAuthoring : MonoBehaviour
    {
        [SerializeField] private SoldierAnimation.State defaultAnimationState = SoldierAnimation.State.None;


        private class Baker : Baker<AnimationStateAuthoring>
        {
            public override void Bake(AnimationStateAuthoring authoring)
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