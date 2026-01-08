using Unity.Entities;

namespace War.Game.Systems.Group
{
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    [UpdateAfter(typeof(InputUpdateGroup))]
    public partial class SpawnSystemGroup : ComponentSystemGroup
    {
    }
}