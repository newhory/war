using Unity.Entities;

namespace War.Dots.Component
{
    public enum TeamColor
    {
        None = 0,
        Red,
        Blue,
        
        Count,
    }

    public struct Team : IComponentData
    {
        public TeamColor Color;
    }
}