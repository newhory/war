using Unity.Burst;
using Unity.Entities;

namespace War.Game.Systems
{
    using VAT;

    [UpdateInGroup(typeof(Group.ViewSystemGroup))]
    public partial struct SoldierVertexAnimationSystem : ISystem
    {
        [BurstCompile]
        private partial struct UpdateAnimationJob : IJobEntity
        {
            private static void Execute(ref CurrentClipProperty currentClipProperty, ref SoldierAnimation soldierAnimation, ref MoveSpeed moveSpeed)
            {
                SoldierAnimation.State current = soldierAnimation.Current;
                SoldierAnimation.State next = soldierAnimation.Next;

                if (current == next)
                {
                    if (current == SoldierAnimation.State.Default)
                    {
                        if (!mathf.Approximately(moveSpeed.Current, moveSpeed.OldCurrent))
                        {
                            currentClipProperty.ClipKeyword = moveSpeed.Current / moveSpeed.Max < 0.1f ? "idle" : "run";

                            moveSpeed.OldCurrent = moveSpeed.Current;
                        }
                    }

                    return;
                }

                switch (next)
                {
                    case SoldierAnimation.State.Default:
                        currentClipProperty.ClipKeyword = moveSpeed.Current / moveSpeed.Max < 0.1f ? "idle" : "run";
                        currentClipProperty.AccumulatedTime = 0f;
                        moveSpeed.OldCurrent = moveSpeed.Current;
                        break;

                    case SoldierAnimation.State.Attack:
                        currentClipProperty.ClipKeyword = "attack";
                        currentClipProperty.NextClipKeyword = default;
                        currentClipProperty.AccumulatedTime = 0f;
                        break;

                    case SoldierAnimation.State.Hit:
                        currentClipProperty.ClipKeyword = "hit";
                        currentClipProperty.NextClipKeyword = "idle";
                        currentClipProperty.AccumulatedTime = 0f;
                        break;

                    case SoldierAnimation.State.Dead:
                        currentClipProperty.ClipKeyword = "die";
                        currentClipProperty.NextClipKeyword = default;
                        currentClipProperty.AccumulatedTime = 0f;
                        break;
                }

                soldierAnimation.Current = next;
                soldierAnimation.Next = next;
            }
        }

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SoldierAnimation>();
            state.RequireForUpdate<CurrentClipProperty>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state) => state.Dependency = new UpdateAnimationJob().ScheduleParallel(state.Dependency);

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }
}