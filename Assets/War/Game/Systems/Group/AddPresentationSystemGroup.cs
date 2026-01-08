using Unity.Entities;

namespace War.Game.Systems.Group
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(SoldierInitializeSystemGroup))]
    public partial class AddPresentationSystemGroup : ComponentSystemGroup
    {
    }
}