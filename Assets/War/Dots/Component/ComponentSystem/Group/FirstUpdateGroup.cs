using Unity.Entities;


namespace War.Dots.Component.ComponentSystem.Group
{
    [UpdateAfter(typeof(VariableRateSimulationSystemGroup))]
    [UpdateBefore(typeof(LastUpdateGroup))]
    public partial class FirstUpdateGroup : ComponentSystemGroup
    {
    }
}