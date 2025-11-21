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
        public int Id;
        public Entity Entity;
        public int HorizontalUnitCount;
    }

    public struct FormationUnit : IComponentData
    {
        public int FormationId;
        public Entity FormationEntity;
        
        public float Radius;
        public float3 LocalPositionInFormation;
    }
}