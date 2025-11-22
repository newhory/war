using Unity.Entities;


namespace War.Dots.Component.ComponentSystem.Group
{
    [UpdateBefore(typeof(FirstUpdateGroup))]
    public partial class AddPresentationSystemGroup : ComponentSystemGroup
    {
    }
}