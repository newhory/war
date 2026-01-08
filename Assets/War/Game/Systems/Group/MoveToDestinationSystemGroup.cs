using Unity.Entities;

namespace War.Game.Systems.Group
{
    [UpdateAfter(typeof(FirstUpdateGroup))]
    [UpdateBefore(typeof(LastUpdateGroup))]
    [UpdateAfter(typeof(StateSystemGroup))]
    public partial class MoveToDestinationSystemGroup : ComponentSystemGroup
    {
    }
}