using Unity.Entities;


namespace War.Dots.Component.ComponentSystem.Group
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(TroopInitializeSystemGroup))]
    [UpdateBefore(typeof(VAT.Systems.Group.VertexAnimationInitializeSystemGroup))]
    public partial class SoldierInitializeSystemGroup : ComponentSystemGroup
    {
    }
}