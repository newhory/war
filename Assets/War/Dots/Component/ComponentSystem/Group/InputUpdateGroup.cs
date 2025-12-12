using Unity.Entities;

namespace War.Dots.Component.ComponentSystem.Group
{
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    public partial class InputUpdateGroup : ComponentSystemGroup
    {
    }

    [UpdateInGroup(typeof(InputUpdateGroup), OrderLast = true)]
    public partial class TroopSystemGroup : ComponentSystemGroup
    {
    }
}