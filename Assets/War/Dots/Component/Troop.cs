using Unity.Entities;


namespace War.Dots.Component
{
    public struct Troop : ISharedComponentData
    {
        public int Id;
    }

    public struct TroopEntity : IComponentData
    {
        public Entity Entity;
    }
}