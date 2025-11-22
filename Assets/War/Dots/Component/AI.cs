using Unity.Entities;


namespace War.Dots.Component
{
    public struct SoldierAI : IComponentData
    {
        public float CheckTargetInterval;
        public double LastCheckTargetTime;
    }
    
    public struct SoldierAISearchTarget : IComponentData, IEnableableComponent
    {
    }

    public struct SoldierAICheckTargetValid : IComponentData, IEnableableComponent
    {
    }
    
    public struct TroopAISearchTarget : IComponentData, IEnableableComponent
    {
    }

    public struct TroopAICheckTargetValid : IComponentData, IEnableableComponent
    {
    }
}