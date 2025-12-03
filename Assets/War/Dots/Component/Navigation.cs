using Unity.Entities;


namespace War.Dots.Component
{
    public enum NavigationType
    {
        NavMesh,
        Custom,
    }

    public struct NavigationAPI : ISharedComponentData
    {
        public NavigationType Type;
    }
}