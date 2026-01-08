using Unity.Entities;

namespace War.Game
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