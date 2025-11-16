using Unity.Entities;
using Unity.Mathematics;


namespace War.Dots.Component
{
    public struct Formation : ISharedComponentData
    {
        public int Id;
    }

    public struct FormationEntity : IComponentData
    {
        public Entity Entity;
        public int HorizontalUnitCount;
    }

    public struct FormationUnit : IComponentData
    {
        public float Radius;
        public float3 LocalPositionInFormation;
    }
}