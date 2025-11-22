using Unity.Entities;


namespace War.Dots.Component.ComponentSystem.Group
{
    [UpdateAfter(typeof(FirstUpdateGroup))]
    public partial class AISystemGroup : ComponentSystemGroup
    {
    }
}