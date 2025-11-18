using Unity.Entities;
using Unity.Physics.Systems;


namespace War.Dots.Component.ComponentSystem.Group
{
    [UpdateAfter(typeof(PhysicsSimulationGroup))]
    [UpdateBefore(typeof(LastUpdateGroup))]
    public partial class FirstUpdateGroup : ComponentSystemGroup
    {
    }
}