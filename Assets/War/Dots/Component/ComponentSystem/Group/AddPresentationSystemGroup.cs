using Unity.Entities;

namespace War.Dots.Component.ComponentSystem.Group
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(SoldierInitializeSystemGroup))]
    public partial class AddPresentationSystemGroup : ComponentSystemGroup
    {
    }
}