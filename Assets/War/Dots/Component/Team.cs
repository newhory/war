using Unity.Entities;

namespace War.Dots.Component
{
    public enum TeamColor
    {
        Red,
        Blue,
    }

    public struct Team : IComponentData
    {
        public TeamColor Color;
    }
}