using Unity.Entities;

namespace War.Game.Systems.Group
{
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    public partial class InputUpdateGroup : ComponentSystemGroup
    {
    }

    [UpdateInGroup(typeof(InputUpdateGroup), OrderFirst = true)]
    public partial class TroopSystemGroup : ComponentSystemGroup
    {
    }
}