using Unity.Entities;


namespace War.Dots.Component.ComponentSystem.Group
{
    [UpdateAfter(typeof(VariableRateSimulationSystemGroup))]
    [UpdateBefore(typeof(Navigation.Systems.Group.NavigationSystemGroup))]
    public partial class FirstUpdateGroup : ComponentSystemGroup
    {
    }
}