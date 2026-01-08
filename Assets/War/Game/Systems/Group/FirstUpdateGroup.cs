using Unity.Entities;

namespace War.Game.Systems.Group
{
    [UpdateAfter(typeof(VariableRateSimulationSystemGroup))]
    [UpdateBefore(typeof(Navigation.Systems.Group.NavigationSystemGroup))]
    public partial class FirstUpdateGroup : ComponentSystemGroup
    {
    }
}