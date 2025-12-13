using Unity.Entities;

namespace War.Dots.Component.ComponentSystem.Group
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(TroopInitializeSystemGroup))]
    public partial class SoldierInitializeSystemGroup : ComponentSystemGroup
    {
    }
}