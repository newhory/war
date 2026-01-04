using Unity.Entities;


namespace War.VAT.Systems.Group
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class VertexAnimationInitializeSystemGroup : ComponentSystemGroup
    {
    }
}