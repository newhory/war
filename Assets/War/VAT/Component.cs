using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;


namespace War.VAT
{
    public struct ClipData : IBufferElementData
    {
        public FixedString64Bytes Keyword;
        public readonly int StartFrame;
        public readonly int EndFrame;
        public readonly float Fps;
        public readonly bool Loop;

        public int FrameCount => EndFrame - StartFrame + 1;


        public ClipData(VatClipData vatClipData)
        {
            Keyword = vatClipData.keyword;
            StartFrame = vatClipData.startFrame;
            EndFrame = vatClipData.endFrame;
            Fps = vatClipData.fps;
            Loop = vatClipData.loop;
        }
    }

    public struct CurrentClipProperty : IComponentData
    {
        public FixedString64Bytes ClipKeyword;
        public FixedString64Bytes NextClipKeyword;
        public float AccumulatedTime;
        public int FrameIndex;
    }

    [MaterialProperty("_FrameIndex")]
    public struct FrameIndexProperty : IComponentData
    {
        public float Value;
    }

    public struct InitializeVertexAnimation : IComponentData
    {
    }

    public struct UnityVertexAnimationBehaviour : IComponentData
    {
        public UnityObjectRef<VertexAnimationBehaviour> Behaviour;
    }
}