using Unity.Entities;


namespace War.Dots.Component.ComponentSystem.Group
{
    [UpdateAfter(typeof(FirstUpdateGroup))]
    [UpdateBefore(typeof(LastUpdateGroup))]
    [UpdateAfter(typeof(PostSpawnSystemGroup))]
    public partial class AISystemGroup : ComponentSystemGroup
    {
    }
    
    [UpdateInGroup(typeof(AISystemGroup))]
    public partial class TroopAISystemGroup : ComponentSystemGroup
    {
    }
    
    [UpdateInGroup(typeof(AISystemGroup))]
    [UpdateAfter(typeof(TroopAISystemGroup))]
    public partial class SoldierAISystemGroup : ComponentSystemGroup
    {
    }
}