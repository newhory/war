using Unity.Entities;
using Unity.Transforms;


namespace War.Dots.Component.ComponentSystem.Group
{
    [UpdateAfter(typeof(FirstUpdateGroup))]
    [UpdateAfter(typeof(LastUpdateGroup))]
    [UpdateAfter(typeof(TransformSystemGroup))]
    public partial class ViewSystemGroup : ComponentSystemGroup
    {
    }
}