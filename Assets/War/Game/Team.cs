using Unity.Entities;

namespace War.Game
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