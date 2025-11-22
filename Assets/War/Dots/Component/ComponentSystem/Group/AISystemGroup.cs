using Unity.Entities;


namespace War.Dots.Component.ComponentSystem.Group
{
    [UpdateAfter(typeof(FirstUpdateGroup))]
    [UpdateBefore(typeof(LastUpdateGroup))]
    [UpdateAfter(typeof(PostSpawnSystemGroup))]
    public partial class AISystemGroup : ComponentSystemGroup
    {
    }
}