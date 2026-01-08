#if DO_NOT_USE_ENTITIES_GRAPHICS
using Unity.Entities;
using UnityEngine;


namespace War.Game.Systems
{
    using VAT;


    [UpdateInGroup(typeof(Group.ViewSystemGroup))]
    public partial class SoldierVertexAnimationBehaviourSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<SoldierAnimation>();
            RequireForUpdate<UnityVertexAnimationBehaviour>();
        }

        protected override void OnUpdate()
        {
            foreach (
                var (animationState, moveSpeed, viewAnimator)
                in
                SystemAPI.Query<RefRW<SoldierAnimation>, RefRW<MoveSpeed>, RefRO<UnityVertexAnimationBehaviour>>())
            {
                VertexAnimationBehaviour animator = viewAnimator.ValueRO.Behaviour;
                if (!animator)
                {
                    continue;
                }

                SoldierAnimation.State current = animationState.ValueRO.Current;
                SoldierAnimation.State next = animationState.ValueRO.Next;

                MoveSpeed moveSpeedValue = moveSpeed.ValueRO;

                if (current == next)
                {
                    if (current == SoldierAnimation.State.Default)
                    {
                        PlayDefault();
                    }

                    continue;
                }

                switch (next)
                {
                    case SoldierAnimation.State.Default:
                        PlayDefault();
                        break;

                    case SoldierAnimation.State.Attack:
                        animator.PlayClip("attack", true);
                        break;

                    case SoldierAnimation.State.Hit:
                        animator.PlayClip("hit", true);
                        break;

                    case SoldierAnimation.State.Dead:
                        animator.PlayClip("die", true);
                        break;
                }

                animationState.ValueRW.Current = next;
                animationState.ValueRW.Next = next;

                if (next == SoldierAnimation.State.Default)
                {
                    PlayDefault();
                }

                continue;

                void PlayDefault()
                {
                    if (!Mathf.Approximately(moveSpeedValue.Current, moveSpeedValue.OldCurrent))
                    {
                        animator.PlayClip(moveSpeedValue.Current / moveSpeedValue.Max < 0.1f ? "idle" : "run");

                        moveSpeed.ValueRW.OldCurrent = moveSpeedValue.Current;
                    }
                }
            }
        }
    }
}
#endif