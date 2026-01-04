using Unity.Entities;
using Unity.Transforms;


namespace War.Navigation.Systems.Group
{
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial class NavigationSystemGroup : ComponentSystemGroup
    {
    }
}