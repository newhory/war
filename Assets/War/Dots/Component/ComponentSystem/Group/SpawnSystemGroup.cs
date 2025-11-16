using Unity.Entities;
using Unity.Transforms;


namespace War.Dots.Component.ComponentSystem.Group
{
    [UpdateBefore(typeof(FirstUpdateGroup))]
    [UpdateAfter(typeof(PlayerInputSystem))]
    public partial class SpawnSystemGroup : ComponentSystemGroup
    {
    }
    
    [UpdateAfter(typeof(SpawnSystemGroup))]
    public partial class PostSpawnSystemGroup : ComponentSystemGroup
    {
    }
}