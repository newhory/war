using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;
using Unity.Mathematics;


namespace War.VAT.Systems
{
    [UpdateAfter(typeof(TransformSystemGroup))]
    public partial struct VertexAnimationSystem : ISystem
    {
        [BurstCompile]
        private partial struct UpdateClipJob : IJobEntity
        {
            [ReadOnly] public float DeltaTime;


            private void Execute(ref CurrentClipProperty currentClipProperty, DynamicBuffer<ClipData> clipDataBuffer)
            {
                bool foundClip = false;
                ClipData clipData = default;

                for (int i = 0, count = clipDataBuffer.Length; i < count; ++i)
                {
                    if (clipDataBuffer[i].Keyword == currentClipProperty.ClipKeyword)
                    {
                        foundClip = true;
                        clipData = clipDataBuffer[i];

                        break;
                    }
                }

                if (!foundClip)
                {
                    return;
                }

                currentClipProperty.AccumulatedTime += DeltaTime * clipData.Fps;

                int currentFrame = (int)math.floor(currentClipProperty.AccumulatedTime);
                int frameIndex = clipData.StartFrame;

                if (clipData.Loop)
                {
                    currentFrame %= clipData.FrameCount;
                    frameIndex += currentFrame;
                }
                else
                {
                    frameIndex += currentFrame;
                    if (frameIndex >= clipData.EndFrame)
                    {
                        frameIndex = clipData.EndFrame;

                        if (!currentClipProperty.NextClipKeyword.IsEmpty)
                        {
                            currentClipProperty.ClipKeyword = currentClipProperty.NextClipKeyword;
                            currentClipProperty.NextClipKeyword = default;
                            currentClipProperty.AccumulatedTime = 0f;
                        }
                    }
                }

                currentClipProperty.FrameIndex = frameIndex;
            }
        }

        [BurstCompile]
        private partial struct UpdateFrameIndexPropertyJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<CurrentClipProperty> CurrentClipPropertyLookup;


            private void Execute(ref FrameIndexProperty frameIndexProperty, in Parent parent) =>
                frameIndexProperty.Value = CurrentClipPropertyLookup[parent.Value].FrameIndex;
        }


        private ComponentLookup<CurrentClipProperty> _currentClipPropertyLookup;


        [BurstCompile]
        public void OnCreate(ref SystemState state) =>
            _currentClipPropertyLookup = SystemAPI.GetComponentLookup<CurrentClipProperty>(true);

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _currentClipPropertyLookup.Update(ref state);

            JobHandle dependency = state.Dependency;

            dependency = new UpdateClipJob { DeltaTime = SystemAPI.Time.DeltaTime }.ScheduleParallel(dependency);
            dependency = new UpdateFrameIndexPropertyJob { CurrentClipPropertyLookup = _currentClipPropertyLookup }.ScheduleParallel(dependency);

            state.Dependency = dependency;
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }
}