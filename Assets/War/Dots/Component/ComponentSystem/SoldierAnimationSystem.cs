using Unity.Entities;
using UnityEngine;


namespace War.Dots.Component.ComponentSystem
{
    [UpdateInGroup(typeof(Group.ViewSystemGroup))]
    public partial class SoldierAnimationSystem : SystemBase
    {
        private static int s_speed;
        private static int s_attack;
        private static int s_hit;
        private static int s_dead;


        protected override void OnCreate()
        {
            RequireForUpdate<SoldierAnimation>();
            RequireForUpdate<UnityAnimator>();

            s_speed = Animator.StringToHash("Speed");
            s_attack = Animator.StringToHash("Attack");
            s_hit = Animator.StringToHash("Hit");
            s_dead = Animator.StringToHash("Dead");
        }

        protected override void OnUpdate()
        {
            foreach (
                var (animationState, moveSpeed, viewAnimator)
                in
                SystemAPI.Query<RefRW<SoldierAnimation>, RefRO<MoveSpeed>, RefRO<UnityAnimator>>())
            {
                Animator animator = viewAnimator.ValueRO.Animator;
                if (!animator)
                {
                    continue;
                }

                SoldierAnimation.State current = animationState.ValueRO.Current;
                SoldierAnimation.State next = animationState.ValueRO.Next;

                if (current == next)
                {
                    if (current == SoldierAnimation.State.Default)
                    {
                        animator.SetFloat(s_speed, moveSpeed.ValueRO.Current / moveSpeed.ValueRO.Max);
                    }

                    continue;
                }

                switch (current)
                {
                    case SoldierAnimation.State.Attack:
                        animator.SetBool(s_attack, false);
                        break;

                    case SoldierAnimation.State.Hit:
                        animator.SetBool(s_hit, false);
                        break;
                }

                switch (next)
                {
                    case SoldierAnimation.State.Default:
                        animator.SetBool(s_attack, false);
                        animator.SetBool(s_hit, false);
                        break;

                    case SoldierAnimation.State.Attack:
                        animator.SetBool(s_attack, true);
                        break;

                    case SoldierAnimation.State.Hit:
                        animator.SetBool(s_hit, true);
                        break;

                    case SoldierAnimation.State.Dead:
                        animator.CrossFade(s_dead, 0.1f);
                        break;
                }

                animationState.ValueRW.Current = next;
                animationState.ValueRW.Next = next;

                if (next == SoldierAnimation.State.Default)
                {
                    animator.SetFloat(s_speed, moveSpeed.ValueRO.Current / moveSpeed.ValueRO.Max);
                }
            }
        }
    }
}