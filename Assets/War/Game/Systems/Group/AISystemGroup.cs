using Unity.Entities;

namespace War.Game.Systems.Group
{
    [UpdateAfter(typeof(FirstUpdateGroup))]
    [UpdateAfter(typeof(Navigation.Systems.Group.NavigationSystemGroup))]
    public partial class AISystemGroup : ComponentSystemGroup
    {
    }
}