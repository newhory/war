using Unity.Entities;


namespace War.Dots.Component
{
    public struct AI : IComponentData
    {
        public float CheckTargetInterval;
        public double LastCheckTargetTime;
    }
    
    public struct AISearchTarget : IComponentData, IEnableableComponent
    {
    }

    public struct AICheckTargetValid : IComponentData, IEnableableComponent
    {
    }
}