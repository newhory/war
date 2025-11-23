using Unity.Entities;

namespace War.Dots.Component.ComponentSystem.Group
{
    [UpdateInGroup(typeof(InitializationSystemGroup), OrderFirst = true)]
    public partial class JustSpawnedInitializeSystemGroup : ComponentSystemGroup
    {
    }
}