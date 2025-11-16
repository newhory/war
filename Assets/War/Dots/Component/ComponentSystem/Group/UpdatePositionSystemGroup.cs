using Unity.Entities;
using Unity.Transforms;


namespace War.Dots.Component.ComponentSystem.Group
{
    [UpdateAfter(typeof(FirstUpdateGroup))]
    [UpdateAfter(typeof(MoveToDestinationSystemGroup))]
    [UpdateBefore(typeof(LastUpdateGroup))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial class UpdatePositionSystemGroup : ComponentSystemGroup
    {
    }
    
    [UpdateInGroup(typeof(UpdatePositionSystemGroup))]
    public partial class MoveSystemGroup : ComponentSystemGroup
    {
    }
}