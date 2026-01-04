using Unity.Entities;


namespace War.Dots.Component.ComponentSystem.Group
{
    [UpdateAfter(typeof(FirstUpdateGroup))]
    [UpdateAfter(typeof(Navigation.Systems.Group.NavigationSystemGroup))]
    public partial class AISystemGroup : ComponentSystemGroup
    {
    }
}