using Unity.Entities;

namespace War.VAT
{
    public struct UnityVertexAnimationBehaviour : IComponentData
    {
        public UnityObjectRef<VertexAnimationBehaviour> Behaviour;
    }
}