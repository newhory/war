using Unity.Entities;


namespace War.Dots.Component.ComponentSystem.Group
{
    [UpdateAfter(typeof(FirstUpdateGroup))]
    [UpdateBefore(typeof(LastUpdateGroup))]
    [UpdateAfter(typeof(StateSystemGroup))]
    public partial class MoveToDestinationSystemGroup : ComponentSystemGroup
    {
    }
}