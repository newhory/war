using Unity.Entities;
using Unity.Transforms;


namespace War.Dots.Component.ComponentSystem.Group
{
    [UpdateAfter(typeof(StateSystemGroup))]
    [UpdateBefore(typeof(MoveToDestinationSystemGroup))]
    public partial class SpawnSystemGroup : ComponentSystemGroup
    {
    }

    [UpdateInGroup(typeof(SpawnSystemGroup), OrderFirst = true)]
    public partial class PreSpawnSystemGroup : ComponentSystemGroup
    {
    }

    [UpdateInGroup(typeof(PreSpawnSystemGroup), OrderFirst = true)]
    public partial class InputUpdateGroup : ComponentSystemGroup
    {
    }

    [UpdateBefore(typeof(AddPresentationSystemGroup))]
    public partial class PostSpawnSystemGroup : ComponentSystemGroup
    {
    }

    [UpdateInGroup(typeof(PostSpawnSystemGroup), OrderFirst = true)]
    public partial class JustSpawnSystemGroup : ComponentSystemGroup
    {
    }
}