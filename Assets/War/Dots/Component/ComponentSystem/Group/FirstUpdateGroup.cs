using Unity.Entities;


namespace War.Dots.Component.ComponentSystem.Group
{
    [UpdateBefore(typeof(LastUpdateGroup))]
    public partial class FirstUpdateGroup : ComponentSystemGroup
    {
    }
}