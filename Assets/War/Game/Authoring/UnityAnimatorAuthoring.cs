using Unity.Entities;
using UnityEngine;

namespace War.Game.Authoring
{
    public class UnityAnimatorAuthoring : MonoBehaviour
    {
        [SerializeField] private Animator animator;


        private class Baker : Baker<UnityAnimatorAuthoring>
        {
            public override void Bake(UnityAnimatorAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(entity, new UnityAnimator { Animator = authoring.animator });
            }
        }
    }
}