using Unity.Entities;

namespace War.Game.Systems.Group
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(TroopInitializeSystemGroup))]
    [UpdateBefore(typeof(VAT.Systems.Group.VertexAnimationInitializeSystemGroup))]
    public partial class SoldierInitializeSystemGroup : ComponentSystemGroup
    {
    }
}