using Unity.Entities;


namespace War.Dots.Component.ComponentSystem.Group
{
    [UpdateAfter(typeof(AISystemGroup))]
    public partial class StateSystemGroup : ComponentSystemGroup
    {
    }

    [UpdateInGroup(typeof(StateSystemGroup))]
    public partial class TroopStateSystemGroup : ComponentSystemGroup
    {
    }

    [UpdateInGroup(typeof(StateSystemGroup))]
    [UpdateAfter(typeof(TroopStateSystemGroup))]
    public partial class SoldierStateSystemGroup : ComponentSystemGroup
    {
    }
}