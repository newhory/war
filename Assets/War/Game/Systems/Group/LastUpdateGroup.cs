using Unity.Entities;

namespace War.Game.Systems.Group
{
    [UpdateAfter(typeof(FirstUpdateGroup))]
    public partial class LastUpdateGroup : ComponentSystemGroup
    {
    }

    [UpdateInGroup(typeof(LastUpdateGroup))]
    public partial class DamagedSystemGroup : ComponentSystemGroup
    {
    }

    [UpdateInGroup(typeof(LastUpdateGroup))]
    [UpdateAfter(typeof(DamagedSystemGroup))]
    public partial class HealthSystemGroup : ComponentSystemGroup
    {
    }

    [UpdateInGroup(typeof(LastUpdateGroup))]
    [UpdateAfter(typeof(HealthSystemGroup))]
    public partial class DestroyOnSystemGroup : ComponentSystemGroup
    {
    }
}