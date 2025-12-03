using Unity.Entities;


namespace War.Navigation.Systems.Group
{
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    public partial class NavigationSystemGroup : ComponentSystemGroup
    {
    }
}