using Unity.Entities;

namespace War.Game.Systems.Group
{
    [UpdateInGroup(typeof(InitializationSystemGroup), OrderFirst = true)]
    public partial class TroopInitializeSystemGroup : ComponentSystemGroup
    {
    }
}