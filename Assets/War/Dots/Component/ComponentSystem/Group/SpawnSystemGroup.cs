using Unity.Entities;
using Unity.Transforms;


namespace War.Dots.Component.ComponentSystem.Group
{
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    [UpdateAfter(typeof(InputUpdateGroup))]
    public partial class SpawnSystemGroup : ComponentSystemGroup
    {
    }
}