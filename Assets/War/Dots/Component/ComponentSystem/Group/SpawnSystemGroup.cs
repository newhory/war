using Unity.Entities;


namespace War.Dots.Component.ComponentSystem.Group
{
    [UpdateBefore(typeof(FirstUpdateGroup))]
    public partial class SpawnSystemGroup : ComponentSystemGroup
    {
    }

    [UpdateAfter(typeof(SpawnSystemGroup))]
    public partial class PostSpawnSystemGroup : ComponentSystemGroup
    {
    }
}